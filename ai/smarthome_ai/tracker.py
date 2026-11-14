"""Tracker đơn giản: ghép theo IoU rồi theo khoảng cách tâm; track mất sau max_missed khung."""
from dataclasses import dataclass, field


def iou(a, b) -> float:
    iw = min(a[2], b[2]) - max(a[0], b[0])
    ih = min(a[3], b[3]) - max(a[1], b[1])
    if iw <= 0 or ih <= 0:
        return 0.0
    i = iw * ih
    return i / ((a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - i)


def center(b):
    return (b[0] + b[2]) / 2, (b[1] + b[3]) / 2


@dataclass
class Track:
    id: int
    box: tuple
    conf: float
    t: float
    prev_center: tuple | None = None
    prev_t: float | None = None
    missed: int = 0
    # trạng thái do pipeline gán
    zone: str | None = None
    pending_zone: str | None = None
    pending_n: int = 0
    postures: list = field(default_factory=list)


class Tracker:
    def __init__(self, max_missed=8, min_iou=0.2, max_dist=0.15):
        self.max_missed, self.min_iou, self.max_dist = max_missed, min_iou, max_dist
        self.tracks: list[Track] = []
        self.next_id = 1

    def update(self, dets, t: float) -> list[Track]:
        """Trả các track vừa được ghép với detection ở khung này."""
        free = list(range(len(dets)))
        matched = []
        for tr in sorted(self.tracks, key=lambda x: x.missed):
            best, best_s = None, 0.0
            for i in free:
                s = iou(tr.box, dets[i].box)
                if s < self.min_iou:  # dự phòng theo khoảng cách tâm
                    (ax, ay), (bx, by) = center(tr.box), center(dets[i].box)
                    d = ((ax - bx) ** 2 + (ay - by) ** 2) ** 0.5
                    s = (1 - d / self.max_dist) * self.min_iou if d < self.max_dist else 0.0
                if s > best_s:
                    best, best_s = i, s
            if best is None:
                tr.missed += 1
                continue
            free.remove(best)
            d = dets[best]
            tr.prev_center, tr.prev_t = center(tr.box), tr.t
            tr.box, tr.conf, tr.t, tr.missed = d.box, d.conf, t, 0
            tr.det = d
            matched.append(tr)
        for i in free:
            tr = Track(self.next_id, dets[i].box, dets[i].conf, t)
            tr.det = dets[i]
            self.next_id += 1
            self.tracks.append(tr)
            matched.append(tr)
        self.tracks = [x for x in self.tracks if x.missed <= self.max_missed]
        return matched
