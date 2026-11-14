"""Vùng đa giác chuẩn hóa 0-1; điểm -> tên vùng."""
import json
from pathlib import Path

ZONE_NAMES = ("livingroom", "sofa", "door", "other_exit")


def _inside(pt, poly) -> bool:
    x, y = pt
    ok = False
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[i - 1]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            ok = not ok
    return ok


class Zones:
    def __init__(self, polys: dict):
        bad = set(polys) - set(ZONE_NAMES)
        if bad:
            raise ValueError(f"vùng lạ: {bad}")
        self.polys = polys  # dict giữ thứ tự = ưu tiên

    @classmethod
    def load(cls, path) -> "Zones":
        return cls(json.loads(Path(path).read_text())["zones"])

    def zone_of(self, pt) -> str | None:
        for name, poly in self.polys.items():
            if _inside(pt, poly):
                return name
        return None
