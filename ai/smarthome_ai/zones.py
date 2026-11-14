"""Vùng đa giác chuẩn hóa 0-1; điểm -> tên vùng."""
import json
import os
from pathlib import Path

ZONE_NAMES = ("livingroom", "sofa", "door", "other_exit")
CONFIG_DIR = Path(__file__).resolve().parent.parent / "config"
EXAMPLE_PATH = CONFIG_DIR / "zones.example.json"
MAX_POINTS = 20


def parse_zones(obj) -> dict:
    """Kiểm chặt {"zones": {...}}; trả dict vùng (livingroom đứng cuối = dự phòng), sai thì ValueError."""
    if not isinstance(obj, dict) or not isinstance(obj.get("zones"), dict):
        raise ValueError('cần {"zones": {...}}')
    out = {}
    for name, poly in obj["zones"].items():
        if name not in ZONE_NAMES:
            raise ValueError(f"vùng lạ: {name}")
        if not isinstance(poly, list) or not 3 <= len(poly) <= MAX_POINTS:
            raise ValueError(f"{name}: cần 3..{MAX_POINTS} điểm")
        for p in poly:
            if (not isinstance(p, list) or len(p) != 2
                    or any(isinstance(v, bool) or not isinstance(v, (int, float)) or not 0 <= v <= 1 for v in p)):
                raise ValueError(f"{name}: mỗi điểm là [x, y] số trong 0..1")
        out[name] = [[float(x), float(y)] for x, y in poly]
    if "livingroom" not in out:
        raise ValueError("thiếu vùng bắt buộc livingroom")
    out["livingroom"] = out.pop("livingroom")  # đưa xuống cuối: thứ tự = ưu tiên
    return out


def save_atomic(path, polys: dict):
    path = Path(path)
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(json.dumps({"zones": polys}, indent=2) + "\n")
    os.replace(tmp, path)


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
        self.polys = polys  # dict giữ thứ tự = ưu tiên; thay nguyên dict để nạp lại (an toàn giữa các thread)

    @classmethod
    def load(cls, path) -> "Zones":
        return cls(json.loads(Path(path).read_text())["zones"])

    def zone_of(self, pt) -> str | None:
        for name, poly in self.polys.items():
            if _inside(pt, poly):
                return name
        return None

    def zone_for(self, box, posture: str) -> str | None:
        """lying/sitting: tâm hộp (chân người nằm chạm đáy khung, trượt khỏi sofa); còn lại: điểm chân, dự phòng tâm."""
        c = ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2)
        if posture in ("lying", "sitting"):
            return self.zone_of(c)
        return self.zone_of(((box[0] + box[2]) / 2, box[3])) or self.zone_of(c)
