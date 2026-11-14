"""YOLOv8n-pose (ONNX) chạy bằng onnxruntime trên CPU. Ra hộp + 17 khớp COCO theo từng người, tọa độ chuẩn hóa.

Đầu ra mô hình [1,56,N]: cx,cy,w,h,score, 17x(x,y,vis) trên ảnh letterbox imgsz x imgsz; NMS làm ở đây.
"""
from pathlib import Path

import numpy as np

from .detector import Detection

MODEL = Path(__file__).resolve().parents[1] / "models" / "yolov8n-pose.onnx"


class PoseDetector:
    def __init__(self, model=MODEL, conf=0.45, iou=0.5, imgsz=640, threads=4):
        import cv2
        import onnxruntime as ort
        if not Path(model).exists():
            raise FileNotFoundError(f"thiếu {model}: chạy python tools/fetch_model.py")
        so = ort.SessionOptions()
        so.intra_op_num_threads = threads
        self.cv2, self.conf, self.iou, self.sz = cv2, conf, iou, imgsz
        self.sess = ort.InferenceSession(str(model), so, providers=["CPUExecutionProvider"])
        self.inp = self.sess.get_inputs()[0].name

    def detect(self, frame) -> list[Detection]:
        h, w = frame.shape[:2]
        r = self.sz / max(h, w)
        nw, nh = round(w * r), round(h * r)
        px, py = (self.sz - nw) // 2, (self.sz - nh) // 2
        img = np.full((self.sz, self.sz, 3), 114, np.uint8)
        img[py:py + nh, px:px + nw] = self.cv2.resize(frame, (nw, nh))
        blob = img[:, :, ::-1].transpose(2, 0, 1)[None].astype(np.float32) / 255
        out = self.sess.run(None, {self.inp: np.ascontiguousarray(blob)})[0][0].T  # [N,56]
        return self._decode(out, w, h, r, px, py)

    def _decode(self, out, w, h, r, px, py) -> list[Detection]:
        out = out[out[:, 4] >= self.conf]
        if not len(out):
            return []
        cx, cy, bw, bh = out[:, 0], out[:, 1], out[:, 2], out[:, 3]
        boxes = np.stack([cx - bw / 2, cy - bh / 2, bw, bh], 1)
        keep = self.cv2.dnn.NMSBoxes(boxes.tolist(), out[:, 4].tolist(), self.conf, self.iou)
        res = []
        for i in np.array(keep).flatten():
            x1, y1 = (cx[i] - bw[i] / 2 - px) / r / w, (cy[i] - bh[i] / 2 - py) / r / h
            x2, y2 = (cx[i] + bw[i] / 2 - px) / r / w, (cy[i] + bh[i] / 2 - py) / r / h
            k = out[i, 5:].reshape(17, 3)
            kp = [(float((x - px) / r / w), float((y - py) / r / h), float(c)) for x, y, c in k]
            box = tuple(float(min(1, max(0, v))) for v in (x1, y1, x2, y2))
            res.append(Detection(box, float(out[i, 4]), kp))
        return res
