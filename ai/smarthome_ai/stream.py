"""Máy chủ HTTP trong tiến trình: /stream.mjpg, /snapshot.jpg, /frame.jpg (không lớp phủ), /config, /zones, CORS *.

Pipeline gọi update(frame, items); một thread riêng vẽ lớp phủ + nén JPEG ~fps lần/giây.
Chữ ASCII (font Hershey của OpenCV không có dấu).
"""
import json
import threading
import time
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import cv2
import numpy as np

from .posture import LABELS, Samples
from .zones import Zones, EXAMPLE_PATH, parse_zones, save_atomic

# Cặp khớp COCO-17 để vẽ xương
BONES = [(5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12), (11, 12), (11, 13), (13, 15), (12, 14), (14, 16),
         (0, 1), (0, 2), (1, 3), (2, 4)]
COLORS = [(80, 200, 255), (120, 255, 120), (255, 160, 90), (200, 120, 255), (90, 255, 255)]
STALE_S = 5.0
MAX_BODY = 64 * 1024
CAPTURE_DELAY, CAPTURE_FRAMES, CAPTURE_GAP = 5, 8, 1.0  # giây chờ vào tư thế, số mẫu mỗi lần ghi, giây giữa hai mẫu
CONFIG_KEYS = ("skeleton", "boxes", "zones", "labels", "sofa_lying")


def placeholder(text="Dang cho camera") -> np.ndarray:
    img = np.full((360, 640, 3), 40, np.uint8)
    (w, _), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 1.0, 2)
    cv2.putText(img, text, ((640 - w) // 2, 190), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (220, 220, 220), 2, cv2.LINE_AA)
    return img


def draw_overlay(frame, items, zones=None, cfg=None):
    """Trả bản sao khung; cfg {skeleton, boxes, zones, labels} bật/tắt từng lớp (mặc định bật hết)."""
    cfg = cfg or {}
    on = lambda k: cfg.get(k, True)
    img = frame.copy()
    h, w = img.shape[:2]
    pt = lambda x, y: (int(x * w), int(y * h))
    if zones and on("zones"):
        layer = img.copy()
        for name, poly in zones.items():
            if name == "livingroom":  # đa giác phủ cả khung, chỉ vẽ viền cho đỡ rối
                continue
            cv2.fillPoly(layer, [np.array([pt(*p) for p in poly])], (255, 180, 60))
        img = cv2.addWeighted(layer, 0.2, img, 0.8, 0)
        for name, poly in zones.items():
            pts = np.array([pt(*p) for p in poly])
            cv2.polylines(img, [pts], True, (255, 200, 100), 1, cv2.LINE_AA)
            cv2.putText(img, name, (pts[0][0] + 4, pts[0][1] + 16), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 220, 150), 1, cv2.LINE_AA)
    for it in items:
        col = COLORS[it["id"] % len(COLORS)]
        x1, y1 = pt(*it["box"][:2])
        x2, y2 = pt(*it["box"][2:])
        if on("boxes"):
            cv2.rectangle(img, (x1, y1), (x2, y2), col, 2)
        kp = it.get("kp")
        if kp and on("skeleton"):
            for a, b in BONES:
                if kp[a][2] >= 0.3 and kp[b][2] >= 0.3:
                    cv2.line(img, pt(*kp[a][:2]), pt(*kp[b][:2]), col, 2, cv2.LINE_AA)
            for x, y, c in kp:
                if c >= 0.3:
                    cv2.circle(img, pt(x, y), 3, (255, 255, 255), -1, cv2.LINE_AA)
        if on("labels"):
            label = f"#{it['id']} {it['posture']} {it['conf']:.2f}" + (f" [{it['zone']}]" if it.get("zone") else "")
            (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
            ty = max(y1, th + 6)
            cv2.rectangle(img, (x1, ty - th - 6), (x1 + tw + 6, ty), col, -1)
            cv2.putText(img, label, (x1 + 3, ty - 4), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 1, cv2.LINE_AA)
    return img


def encode(img, quality=70) -> bytes:
    return cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, quality])[1].tobytes()


class StreamServer:
    def __init__(self, port=8090, zones=None, fps=8.0, quality=70, host="0.0.0.0", zones_path=None, skeleton=True, sofa_lying=False):
        # zones: Zones (dùng chung với pipeline để nạp lại tức thì) hoặc dict đa giác
        self.zones = zones if isinstance(zones, Zones) else Zones(zones or {})
        self.zones_path, self.fps, self.quality = zones_path, fps, quality
        self.cfg = {"skeleton": skeleton, "boxes": True, "zones": True, "labels": True, "sofa_lying": sofa_lying}
        self.cond = threading.Condition()
        # mẫu tư thế nằm cạnh zones.json; capture = {"label","start","left"} khi đang ghi (pipeline đọc và tự xóa)
        self.samples = Samples(Path(zones_path).with_name("posture-samples.json") if zones_path else None)
        self.capture = None
        self.latest = (None, [], 0.0)  # frame, items, thời điểm nhận
        self.jpeg, self.n = encode(placeholder(), quality), 0
        self._stop = threading.Event()
        owner = self

        class H(BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def do_GET(self):
                path = self.path.split("?")[0]
                if path == "/snapshot.jpg":
                    self._send(owner.jpeg, "image/jpeg")
                elif path == "/frame.jpg":
                    self._send(owner.raw_jpeg(), "image/jpeg")
                elif path == "/config":
                    self._json(owner.cfg)
                elif path == "/posture":
                    self._json(owner.posture_status())
                elif path.startswith("/posture/img/"):
                    img = owner.samples.image(path[13:].removesuffix(".jpg"))
                    self._send(img, "image/jpeg") if img else self.send_error(404)
                elif path == "/zones":
                    self._json({"zones": owner.zones.polys})
                elif path == "/zones/default":
                    self._json(json.loads(EXAMPLE_PATH.read_text()))
                elif path == "/stream.mjpg":
                    self._stream()
                else:
                    self.send_error(404)

            def do_OPTIONS(self):
                self.send_response(204)
                self.send_header("Access-Control-Allow-Origin", "*")
                self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS")
                self.send_header("Access-Control-Allow-Headers", "Content-Type")
                self.send_header("Access-Control-Max-Age", "600")
                self.send_header("Content-Length", "0")
                self.end_headers()

            def _body(self):
                """JSON thân yêu cầu (<=64 KB) hoặc None sau khi đã trả 400/413."""
                try:
                    n = int(self.headers.get("Content-Length", ""))
                except ValueError:
                    self._json({"error": "thiếu Content-Length"}, 400)
                    return None
                if n > MAX_BODY:
                    self._json({"error": "thân quá 64 KB"}, 413)
                    self.close_connection = True
                    return None
                try:
                    return json.loads(self.rfile.read(n))
                except ValueError:
                    self._json({"error": "JSON không hợp lệ"}, 400)

            def do_POST(self):
                path = self.path.split("?")[0]
                if path not in ("/config", "/posture"):
                    return self.send_error(404)
                body = self._body()
                if body is None:
                    return
                if path == "/posture":  # {"label": tư thế} bắt đầu ghi sau 5 giây; {"label": "clear"} xóa hết mẫu
                    label = body.get("label") if isinstance(body, dict) else None
                    if isinstance(body, dict) and "delete" in body:  # {"delete": id} bỏ một mẫu đã ghi
                        owner.samples.remove(str(body["delete"]))
                    elif label == "clear":  # kèm "of": tư thế thì chỉ xóa mẫu của tư thế đó
                        of = body.get("of")
                        if of is not None and of not in LABELS:
                            return self._json({"error": f"of phải là một trong {list(LABELS)}"}, 400)
                        owner.capture = None
                        owner.samples.clear(of)
                    elif label in LABELS:
                        owner.capture = {"label": label, "start": time.time() + CAPTURE_DELAY, "left": CAPTURE_FRAMES, "gap": CAPTURE_GAP}
                    else:
                        return self._json({"error": f"label phải là một trong {list(LABELS)} hoặc clear"}, 400)
                    return self._json(owner.posture_status())
                if not isinstance(body, dict) or any(k not in CONFIG_KEYS or not isinstance(v, bool) for k, v in body.items()):
                    return self._json({"error": f"chỉ nhận khóa bool trong {list(CONFIG_KEYS)}"}, 400)
                owner.cfg = {**owner.cfg, **body}
                owner.rerender()
                self._json(owner.cfg)

            def do_PUT(self):
                if self.path.split("?")[0] != "/zones":
                    return self.send_error(404)
                body = self._body()
                if body is None:
                    return
                try:
                    polys = parse_zones(body)
                    if owner.zones_path:
                        save_atomic(owner.zones_path, polys)
                except ValueError as e:
                    return self._json({"error": str(e)}, 400)
                except OSError as e:
                    return self._json({"error": f"không ghi được tệp: {e}"}, 500)
                owner.zones.polys = polys
                owner.rerender()
                self._json({"zones": polys})

            def _json(self, obj, code=200):
                self._send(json.dumps(obj).encode(), "application/json", code)

            def _send(self, body, ctype, code=200):
                self.send_response(code)
                self.send_header("Content-Type", ctype)
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Cache-Control", "no-store")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.end_headers()
                self.wfile.write(body)

            def _stream(self):
                self.send_response(200)
                self.send_header("Content-Type", "multipart/x-mixed-replace; boundary=frame")
                self.send_header("Cache-Control", "no-store")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.end_headers()
                seen = -1
                try:
                    while not owner._stop.is_set():
                        with owner.cond:
                            owner.cond.wait_for(lambda: owner.n != seen or owner._stop.is_set(), timeout=5)
                            seen, jpg = owner.n, owner.jpeg
                        self.wfile.write(b"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: %d\r\n\r\n" % len(jpg) + jpg + b"\r\n")
                except (BrokenPipeError, ConnectionResetError):
                    pass

        self.httpd = ThreadingHTTPServer((host, port), H)
        self.httpd.daemon_threads = True
        self.port = self.httpd.server_address[1]
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()
        threading.Thread(target=self._render, daemon=True).start()

    def update(self, frame, items):
        self.latest = (frame, items, time.monotonic())

    def raw_jpeg(self):
        frame, _, t = self.latest
        fresh = frame is not None and time.monotonic() - t <= STALE_S
        return encode(frame if fresh else placeholder(), self.quality)

    def thumb(self, frame, box, kp):
        """Ảnh nhỏ của một người (kèm khung xương) để xem lại mẫu; None nếu không có khung hình."""
        if frame is None:
            return None
        img = draw_overlay(frame, [{"box": box, "kp": kp, "id": 0, "zone": None, "posture": "", "conf": 0}], None, {"labels": False, "zones": False})
        h, w = img.shape[:2]
        mx, my = (box[2] - box[0]) * .2, (box[3] - box[1]) * .2
        x1, y1, x2, y2 = max(0, int((box[0] - mx) * w)), max(0, int((box[1] - my) * h)), int((box[2] + mx) * w), int((box[3] + my) * h)
        crop = img[y1:y2, x1:x2]
        if crop.size == 0:
            return None
        return encode(cv2.resize(crop, (max(1, crop.shape[1] * 160 // crop.shape[0]), 160)), 75)

    def posture_status(self):
        cap = self.capture
        return {"counts": self.samples.counts(), "samples": self.samples.ids(), "trained": self.samples.trained,
                "capture": cap and {"label": cap["label"], "left": cap["left"], "wait": max(0, round(cap["start"] - time.time()))}}

    def rerender(self):
        frame, items, t = self.latest
        if frame is None or time.monotonic() - t > STALE_S:
            img = placeholder()
        else:
            img = draw_overlay(frame, items, self.zones.polys, self.cfg)
        jpg = encode(img, self.quality)
        with self.cond:
            self.jpeg, self.n = jpg, self.n + 1
            self.cond.notify_all()

    def _render(self):
        while not self._stop.wait(1 / self.fps):
            self.rerender()

    def close(self):
        self._stop.set()
        with self.cond:
            self.cond.notify_all()
        self.httpd.shutdown()
        self.httpd.server_close()
