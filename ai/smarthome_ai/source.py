"""Nguồn khung hình: file video hoặc RTSP (OpenCV). Trả (t_giây, frame)."""
import time


class CvSource:
    def __init__(self, src, width=None, height=None):
        """src: đường dẫn/URL, hoặc chỉ số webcam (int hoặc chuỗi toàn chữ số, vd "0")."""
        import cv2
        if isinstance(src, str) and src.isdigit():
            src = int(src)
        self.cv2, self.src, self.size = cv2, src, (width, height)
        self.live = isinstance(src, int) or src.lower().startswith(("rtsp://", "rtsps://", "http"))
        self.cap = self._open()

    def _open(self):
        cap = self.cv2.VideoCapture(self.src)
        if isinstance(self.src, int):
            cap.set(self.cv2.CAP_PROP_BUFFERSIZE, 1)  # tránh khung cũ dồn lại khi detect chậm
            for prop, v in zip((self.cv2.CAP_PROP_FRAME_WIDTH, self.cv2.CAP_PROP_FRAME_HEIGHT), self.size):
                if v:
                    cap.set(prop, v)
        return cap

    def read(self):
        """(t, frame) hoặc None khi hết file / mất luồng."""
        ok, frame = self.cap.read()
        if not ok:
            return None
        t = time.monotonic() if self.live else self.cap.get(self.cv2.CAP_PROP_POS_MSEC) / 1000
        return t, frame

    def reopen(self) -> bool:
        self.cap.release()
        self.cap = self._open()
        return self.cap.isOpened()


class FakeSource:
    """n khung rỗng, fps cố định, cho FakeDetector."""
    live = False

    def __init__(self, n: int, fps: float = 4.0):
        self.n, self.fps, self.i = n, fps, 0

    def read(self):
        if self.i >= self.n:
            return None
        self.i += 1
        return (self.i - 1) / self.fps, None
