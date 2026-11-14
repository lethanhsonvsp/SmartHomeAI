"""Máy chủ MJPEG trong tiến trình: GET /stream.mjpg, GET /snapshot.jpg, CORS *.

Pipeline gọi update(frame, items); một thread riêng vẽ lớp phủ + nén JPEG ~fps lần/giây.
Chữ ASCII (font Hershey của OpenCV không có dấu).
"""
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import cv2
import numpy as np

# Cặp khớp COCO-17 để vẽ xương
BONES = [(5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12), (11, 12), (11, 13), (13, 15), (12, 14), (14, 16),
         (0, 1), (0, 2), (1, 3), (2, 4)]
COLORS = [(80, 200, 255), (120, 255, 120), (255, 160, 90), (200, 120, 255), (90, 255, 255)]
STALE_S = 5.0


def placeholder(text="Dang cho camera") -> np.ndarray:
    img = np.full((360, 640, 3), 40, np.uint8)
    (w, _), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 1.0, 2)
    cv2.putText(img, text, ((640 - w) // 2, 190), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (220, 220, 220), 2, cv2.LINE_AA)
    return img


def draw_overlay(frame, items, zones=None):
    """Trả bản sao khung có vùng (tô nhạt), hộp, xương, nhãn id/posture/conf."""
    img = frame.copy()
    h, w = img.shape[:2]
    pt = lambda x, y: (int(x * w), int(y * h))
    if zones:
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
        cv2.rectangle(img, (x1, y1), (x2, y2), col, 2)
        kp = it.get("kp")
        if kp:
            for a, b in BONES:
                if kp[a][2] >= 0.3 and kp[b][2] >= 0.3:
                    cv2.line(img, pt(*kp[a][:2]), pt(*kp[b][:2]), col, 2, cv2.LINE_AA)
            for x, y, c in kp:
                if c >= 0.3:
                    cv2.circle(img, pt(x, y), 3, (255, 255, 255), -1, cv2.LINE_AA)
        label = f"#{it['id']} {it['posture']} {it['conf']:.2f}" + (f" [{it['zone']}]" if it.get("zone") else "")
        (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        ty = max(y1, th + 6)
        cv2.rectangle(img, (x1, ty - th - 6), (x1 + tw + 6, ty), col, -1)
        cv2.putText(img, label, (x1 + 3, ty - 4), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 1, cv2.LINE_AA)
    return img


def encode(img, quality=70) -> bytes:
    return cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, quality])[1].tobytes()


class StreamServer:
    def __init__(self, port=8090, zones=None, fps=8.0, quality=70, host="0.0.0.0"):
        self.zones, self.fps, self.quality = zones, fps, quality
        self.cond = threading.Condition()
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
                elif path == "/stream.mjpg":
                    self._stream()
                else:
                    self.send_error(404)

            def _send(self, body, ctype):
                self.send_response(200)
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

    def _render(self):
        while not self._stop.wait(1 / self.fps):
            frame, items, t = self.latest
            if frame is None or time.monotonic() - t > STALE_S:
                img = placeholder()
            else:
                img = draw_overlay(frame, items, self.zones)
            jpg = encode(img, self.quality)
            with self.cond:
                self.jpeg, self.n = jpg, self.n + 1
                self.cond.notify_all()

    def close(self):
        self._stop.set()
        with self.cond:
            self.cond.notify_all()
        self.httpd.shutdown()
        self.httpd.server_close()
