"""Nguồn khung hình: file video hoặc RTSP (OpenCV). Trả (t_giây, frame)."""
import time


class CvSource:
    def __init__(self, src: str):
        import cv2
        self.cv2, self.src = cv2, src
        self.live = src.lower().startswith(("rtsp://", "rtsps://", "http"))
        self.cap = cv2.VideoCapture(src)

    def read(self):
        """(t, frame) hoặc None khi hết file / mất luồng."""
        ok, frame = self.cap.read()
        if not ok:
            return None
        t = time.monotonic() if self.live else self.cap.get(self.cv2.CAP_PROP_POS_MSEC) / 1000
        return t, frame

    def reopen(self) -> bool:
        self.cap.release()
        self.cap = self.cv2.VideoCapture(self.src)
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
