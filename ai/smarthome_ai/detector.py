"""Detector cắm được. Hộp chuẩn hóa 0-1 (x1,y1,x2,y2); keypoints COCO-17 [(x,y,c)] chuẩn hóa, tùy chọn.

Chọn HOG của OpenCV làm bản chạy thật: không thêm phụ thuộc nặng (ultralytics kéo torch ~GB),
chạy CPU. Độ chính xác kém YOLO, nhất là người nằm; thay bằng Detector khác khi có video thật để đo.
"""
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol


@dataclass
class Detection:
    box: tuple  # x1, y1, x2, y2 chuẩn hóa
    conf: float
    keypoints: list | None = None


class Detector(Protocol):
    def detect(self, frame) -> list[Detection]: ...


class HogDetector:
    def __init__(self, width=640):
        import cv2
        self.cv2, self.width = cv2, width
        self.hog = cv2.HOGDescriptor()
        self.hog.setSVMDetector(cv2.HOGDescriptor_getDefaultPeopleDetector())

    def detect(self, frame) -> list[Detection]:
        h, w = frame.shape[:2]
        s = self.width / w
        small = self.cv2.resize(frame, (self.width, int(h * s)))
        sh, sw = small.shape[:2]
        rects, weights = self.hog.detectMultiScale(small, winStride=(8, 8), scale=1.05)
        return [Detection((x / sw, y / sh, (x + rw) / sw, (y + rh) / sh), min(1.0, max(0.0, float(wt) / 3)))
                for (x, y, rw, rh), wt in zip(rects, weights)]


class FakeDetector:
    """Kịch bản: danh sách khung, mỗi khung là danh sách {"box":[..],"conf":..,"keypoints":..}."""

    def __init__(self, script: list):
        self.script, self.i = script, 0

    @classmethod
    def load(cls, path) -> "FakeDetector":
        return cls(json.loads(Path(path).read_text()))

    def detect(self, frame) -> list[Detection]:
        items = self.script[self.i] if self.i < len(self.script) else []
        self.i += 1
        return [Detection(tuple(d["box"]), d.get("conf", 0.9), d.get("keypoints")) for d in items]
