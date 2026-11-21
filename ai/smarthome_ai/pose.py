"""Mô hình pose ONNX (YOLOv8, YOLO26, RTMO) chạy bằng onnxruntime trên CPU. Ra hộp + 17 khớp COCO theo từng người, tọa độ chuẩn hóa.

Loại mô hình tự nhận theo đầu ra: YOLOv8 [1,56,N] (cx,cy,w,h,score,17x3; NMS làm ở đây), YOLO26 [1,300,57], RTMO (dets + keypoints).
"""
from pathlib import Path

import numpy as np

from .detector import Detection

MODEL = Path(__file__).resolve().parents[1] / "models" / "yolov8m-pose.onnx"


class PoseDetector:
    # conf 0.3: cam soi trên cao làm người nằm/ngồi chỉ đạt 0.3-0.5, ngưỡng 0.45 mất nửa số khung
    def __init__(self, model=MODEL, conf=0.3, iou=0.5, imgsz=640, threads=4):
        import cv2
        import onnxruntime as ort
        if not Path(model).exists():
            raise FileNotFoundError(f"thiếu {model}: chạy python tools/fetch_model.py")
        so = ort.SessionOptions()
        so.intra_op_num_threads = threads
        self.cv2, self.conf, self.iou, self.sz = cv2, conf, iou, imgsz
        self.sess = ort.InferenceSession(str(model), so, providers=["CPUExecutionProvider"])
        self.inp = self.sess.get_inputs()[0].name
        self.rtmo = len(self.sess.get_outputs()) == 2

    def detect(self, frame) -> list[Detection]:
        h, w = frame.shape[:2]
        r = self.sz / max(h, w)
        nw, nh = round(w * r), round(h * r)
        px, py = (self.sz - nw) // 2, (self.sz - nh) // 2
        img = np.full((self.sz, self.sz, 3), 114, np.uint8)
        img[py:py + nh, px:px + nw] = self.cv2.resize(frame, (nw, nh))
        if self.rtmo:  # RTMO: BGR 0-255; ra dets [N,5] xyxy+score và keypoints [N,17,3], NMS nằm trong mô hình
            outs = self.sess.run(None, {self.inp: np.ascontiguousarray(img.transpose(2, 0, 1)[None].astype(np.float32))})
            d = outs[0][0]
            return self._emit(np.hstack([d, outs[1][0].reshape(len(d), -1)]), w, h, r, px, py)
        blob = img[:, :, ::-1].transpose(2, 0, 1)[None].astype(np.float32) / 255
        out = self.sess.run(None, {self.inp: np.ascontiguousarray(blob)})[0][0]
        if out.shape[-1] == 57:  # YOLO26 [300,57]: xyxy, score, lớp, 17x(x,y,vis); không cần NMS
            out = np.delete(out, 5, 1)
            if out[:, :4].max() <= 2:  # bản xuất onnx-community chia mọi tọa độ (cả vis) cho imgsz; bản Ultralytics tự xuất thì để pixel
                out[:, :4] *= self.sz
                out[:, 5:] *= self.sz
            return self._emit(out, w, h, r, px, py)
        return self._decode(out.T, w, h, r, px, py)  # YOLOv8 [N,56]

    def _decode(self, out, w, h, r, px, py) -> list[Detection]:
        out = out[out[:, 4] >= self.conf]
        if not len(out):
            return []
        cx, cy, bw, bh = out[:, 0], out[:, 1], out[:, 2], out[:, 3]
        boxes = np.stack([cx - bw / 2, cy - bh / 2, bw, bh], 1)
        keep = np.array(self.cv2.dnn.NMSBoxes(boxes.tolist(), out[:, 4].tolist(), self.conf, self.iou)).flatten()
        xyxy = np.stack([cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2], 1)
        return self._emit(np.hstack([xyxy, out[:, 4:]])[keep], w, h, r, px, py)

    def _emit(self, rows, w, h, r, px, py) -> list[Detection]:
        """rows [N,56]: x1,y1,x2,y2,score, 17x(x,y,vis) trên ảnh letterbox -> Detection chuẩn hóa theo khung gốc."""
        res = []
        for row in rows[rows[:, 4] >= self.conf]:
            x1, y1, x2, y2 = (row[0] - px) / r / w, (row[1] - py) / r / h, (row[2] - px) / r / w, (row[3] - py) / r / h
            kp = [(float((x - px) / r / w), float((y - py) / r / h), float(c)) for x, y, c in row[5:].reshape(17, 3)]
            box = tuple(float(min(1, max(0, v))) for v in (x1, y1, x2, y2))
            res.append(Detection(box, float(row[4]), kp))
        return res
