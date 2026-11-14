"""Điều khiển ESP32 giả từ xa: GET /state, POST /set (chỉ nghe 127.0.0.1)."""
import json, threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

# khoảng hợp lệ của từng trường số; lux nhận số nguyên hoặc số thực, doorOpen phải bool
RANGES = {"tempC": (-20, 60), "humidity": (0, 100), "lux": (0, 100000)}


class State:
    def __init__(self, temp=28.0, humidity=60.0, lux=150, door=False):
        self.lock = threading.Lock()
        self.v = {"tempC": temp, "humidity": humidity, "lux": lux, "doorOpen": door, "buzzer": False}

    def snapshot(self):
        with self.lock:
            return dict(self.v)

    def apply(self, body):
        """Trả (state mới, tập khóa đã đổi); ném ValueError nếu sai, khi đó không đổi gì."""
        if not isinstance(body, dict):
            raise ValueError("body phải là object JSON")
        for k, x in body.items():
            if k in RANGES:
                lo, hi = RANGES[k]
                if isinstance(x, bool) or not isinstance(x, (int, float)) or not lo <= x <= hi:
                    raise ValueError(f"{k}: số trong [{lo}, {hi}]")
            elif k == "doorOpen":
                if not isinstance(x, bool):
                    raise ValueError("doorOpen: bool")
            else:
                raise ValueError(f"trường lạ: {k}")
        with self.lock:
            changed = {k for k, x in body.items() if self.v[k] != x}
            self.v.update(body)
            return dict(self.v), changed


def serve(state, port, on_change=lambda changed: None):
    """Mở server ở luồng nền; on_change(changed) gọi sau POST /set thành công. Trả server (shutdown() để tắt)."""
    class H(BaseHTTPRequestHandler):
        def _send(self, code, obj):
            data = json.dumps(obj).encode()
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            self._send(200, state.snapshot()) if self.path == "/state" else self._send(404, {"error": "not found"})

        def do_POST(self):
            if self.path != "/set":
                return self._send(404, {"error": "not found"})
            try:
                body = json.loads(self.rfile.read(int(self.headers.get("Content-Length") or 0)))
                new, changed = state.apply(body)
            except ValueError as e:  # gồm JSONDecodeError
                return self._send(400, {"error": str(e)})
            on_change(changed)
            self._send(200, new)

        def log_message(self, *_):
            pass

    srv = ThreadingHTTPServer(("127.0.0.1", port), H)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return srv
