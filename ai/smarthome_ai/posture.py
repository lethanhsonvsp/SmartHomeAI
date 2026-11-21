"""Posture bằng quy tắc hình học. CHỈ LÀ BASELINE, ngưỡng đoán, cần đo và chỉnh bằng video thật.

Hộp: tỉ lệ rộng/cao (đã quy về pixel bằng aspect khung) -> standing / sitting / lying.
moving: tâm dịch nhiều (đơn vị chiều cao hộp mỗi giây) so với khung trước.
Có keypoints COCO-17 thì dùng góc thân (vai->hông) và khoảng hông-gối.

Cam trần soi chéo làm quy tắc trên lẫn ngồi/nằm. Samples: người dùng ghi mẫu khớp xương từng tư thế ngay trên
camera của mình (Dashboard > Cài đặt), đủ mẫu thì phân loại bằng k láng giềng gần nhất thay cho quy tắc.
"""
import json
import os
import time
from pathlib import Path

LABELS = ("standing", "sitting", "lying")
MIN_SAMPLES = 5    # mỗi tư thế, và cần ít nhất 2 tư thế thì mới dùng mẫu

MOVE_SPEED = 0.5   # chiều cao hộp / giây
LYING_AR = 1.1     # rộng/cao
STAND_AR = 0.55


def _kp_posture(kp, aspect=16 / 9):
    """Tọa độ chuẩn hóa nên x nhân aspect để so với y theo pixel."""
    if not kp or len(kp) < 17:
        return None
    ok = lambda i: kp[i][2] >= 0.3
    mid = lambda a, b: ((kp[a][0] + kp[b][0]) / 2 * aspect, (kp[a][1] + kp[b][1]) / 2)
    hips = [i for i in (11, 12) if ok(i)]
    sh = [i for i in (5, 6) if ok(i)]
    if not hips or not sh:
        return None
    hx, hy = mid(hips[0], hips[-1])
    sx, sy = mid(sh[0], sh[-1])
    dx, dy = abs(hx - sx), abs(hy - sy)
    if dx > dy:  # thân gần nằm ngang
        return "lying"
    if ok(0) and abs(kp[0][1] - hy) < dx * 0.5 and abs(kp[0][1] - sy) < dx * 0.5:  # đầu-vai-hông cùng độ cao
        return "lying"
    knees = [i for i in (13, 14) if ok(i)]
    if knees:
        ky = sum(kp[i][1] for i in knees) / len(knees)
        # hông gần mức đầu gối (đùi nằm ngang) -> ngồi; chân duỗi thẳng xuống -> đứng
        return "sitting" if abs(ky - hy) < 0.6 * dy else "standing"
    return None


def features(kp, aspect=16 / 9):
    """17 khớp quy về tâm và chia cho độ trải lớn nhất: không phụ thuộc vị trí và cỡ người trong khung. None nếu thiếu khớp."""
    if not kp or len(kp) < 17:
        return None
    pts = [(x * aspect, y) for x, y, _ in kp[:17]]
    cx, cy = sum(p[0] for p in pts) / 17, sum(p[1] for p in pts) / 17
    s = max(max(abs(x - cx), abs(y - cy)) for x, y in pts) or 1.0
    return [round((v - c) / s, 3) for x, y in pts for v, c in ((x, cx), (y, cy))]


class Samples:
    """Mẫu tư thế đã ghi: {label: [{"id", "f": features}]}, lưu JSON cạnh zones.json; ảnh xem lại ở thư mục <tên tệp bỏ .json>/<id>.jpg."""

    def __init__(self, path=None):
        self.path, self.items = path and Path(path), {}
        self.dir = self.path and self.path.with_suffix("")
        try:
            data = json.loads(self.path.read_text()) if path else {}
            for k in LABELS:
                # bản cũ lưu thẳng danh sách đặc trưng (không id, không ảnh)
                rows = [e if isinstance(e, dict) else {"id": f"{LABELS.index(k)}{i:04d}", "f": e} for i, e in enumerate(data.get(k, []))]
                self.items[k] = [e for e in rows if len(e["f"]) == 34 and str(e["id"]).isdigit()]
        except (OSError, ValueError, TypeError, AttributeError, KeyError):
            self.items = {}  # chưa có tệp hoặc tệp hỏng: coi như chưa ghi mẫu

    def counts(self):
        return {k: len(self.items.get(k, [])) for k in LABELS}

    def ids(self):
        return {k: [e["id"] for e in self.items.get(k, [])] for k in LABELS}

    @property
    def trained(self):
        return sum(n >= MIN_SAMPLES for n in self.counts().values()) >= 2

    def add(self, label, feat, jpeg=None):
        sid = str(time.time_ns())
        self.items.setdefault(label, []).append({"id": sid, "f": feat})
        if jpeg and self.dir:
            self.dir.mkdir(exist_ok=True)
            (self.dir / f"{sid}.jpg").write_bytes(jpeg)

    def image(self, sid):
        """Ảnh xem lại của mẫu; None nếu id lạ hoặc không có ảnh. Chỉ nhận id toàn chữ số: không thoát được khỏi thư mục mẫu."""
        try:
            return (self.dir / f"{sid}.jpg").read_bytes() if self.dir and str(sid).isdigit() else None
        except OSError:
            return None

    def remove(self, sid):
        for rows in self.items.values():
            rows[:] = [e for e in rows if e["id"] != sid]
        if self.dir and str(sid).isdigit():
            (self.dir / f"{sid}.jpg").unlink(missing_ok=True)
        self.save()

    def clear(self, label=None):
        """Xóa mọi mẫu, hoặc chỉ mẫu của một tư thế."""
        for lab in [label] if label else list(self.items):
            for e in self.items.pop(lab, []):
                if self.dir:
                    (self.dir / f"{e['id']}.jpg").unlink(missing_ok=True)
        self.save()

    def save(self):
        if self.path:
            tmp = self.path.with_name(self.path.name + ".tmp")
            tmp.write_text(json.dumps(self.items))
            os.replace(tmp, self.path)

    def classify(self, feat, k=5):
        near = sorted((sum((a - b) ** 2 for a, b in zip(feat, e["f"])), lab) for lab, rows in self.items.items() for e in rows)[:k]
        votes = [lab for _, lab in near]
        return max(LABELS, key=votes.count)


def classify(box, speed: float, aspect: float = 16 / 9, keypoints=None, samples=None) -> str:
    """speed: chiều cao hộp/giây (0 nếu chưa biết). aspect = rộng/cao khung hình."""
    if speed > MOVE_SPEED:
        return "moving"
    if samples and samples.trained and (f := features(keypoints, aspect)):
        return samples.classify(f)
    kp = _kp_posture(keypoints, aspect)
    if kp:
        return kp
    w, h = (box[2] - box[0]) * aspect, box[3] - box[1]
    if h <= 0:
        return "unknown"
    ar = w / h
    return "lying" if ar > LYING_AR else "standing" if ar < STAND_AR else "sitting"


def smooth(history: list, new: str, n=8, prev: str | None = None, min_frames=4) -> str:
    """Bỏ phiếu số đông n khung gần nhất. Chưa đủ min_frames -> unknown; hòa phiếu -> giữ tư thế đang phát (prev), chưa có -> unknown."""
    history.append(new)
    del history[:-n]
    if len(history) < min_frames:
        return "unknown"
    top = max(history.count(x) for x in set(history))
    win = {x for x in history if history.count(x) == top}
    return win.pop() if len(win) == 1 else (prev or "unknown")
