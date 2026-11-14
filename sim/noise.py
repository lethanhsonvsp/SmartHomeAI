"""Tiêm nhiễu xác định theo seed vào danh sách sự kiện kịch bản (sau khi đã có ts trong payload)."""
import copy, random
from datetime import datetime, timedelta

LEVELS = {
    "mild": dict(dropout=0.02, burst=1, posture_flip=0.02, zone_jitter=0.02, track_id_switch=0.0,
                 temp_jitter=0.2, lux_jitter=3.0, duplicate=0.02, late=0.02, late_s=0.5),
    "moderate": dict(dropout=0.08, burst=2, posture_flip=0.08, zone_jitter=0.08, track_id_switch=0.01,
                     temp_jitter=0.5, lux_jitter=10.0, duplicate=0.05, late=0.05, late_s=1.0),
    "heavy": dict(dropout=0.2, burst=3, posture_flip=0.2, zone_jitter=0.2, track_id_switch=0.03,
                  temp_jitter=1.0, lux_jitter=25.0, duplicate=0.1, late=0.1, late_s=2.0),
}
POSTURES, ZONES = ("lying", "sitting", "standing"), ("sofa", "livingroom")


def _shift_ts(payload, seconds):
    dt = datetime.strptime(payload["ts"], "%Y-%m-%dT%H:%M:%SZ") + timedelta(seconds=seconds)
    payload["ts"] = dt.strftime("%Y-%m-%dT%H:%M:%SZ")


def inject(events, level="mild", seed=0, **override):
    """Trả danh sách mới (không sửa đầu vào). level: mild|moderate|heavy|dict; override ghi đè từng tham số."""
    cfg = {**(LEVELS[level] if isinstance(level, str) else level), **override}
    rng, ids, out, drop = random.Random(seed), {}, [], 0
    for src in events:
        e = copy.deepcopy(src)
        p, is_state = e.payload, e.topic.endswith("/state") and "persons" in e.payload
        if is_state:
            if drop:
                drop -= 1
                continue
            if rng.random() < cfg["dropout"]:
                drop = rng.randint(1, cfg["burst"]) - 1
                continue
            for q in p["persons"]:
                if rng.random() < cfg["track_id_switch"]:
                    ids[q["track_id"]] = ids.get(q["track_id"], q["track_id"]) + 10
                q["track_id"] = ids.get(q["track_id"], q["track_id"])
                if q["posture"] in POSTURES and rng.random() < cfg["posture_flip"]:
                    q["posture"] = rng.choice([x for x in POSTURES if x != q["posture"]])
                if q["zone"] in ZONES and rng.random() < cfg["zone_jitter"]:
                    q["zone"] = ZONES[1 - ZONES.index(q["zone"])]
        elif e.topic.endswith("/transition"):
            for k in ("track_id",):
                p[k] = ids.get(p[k], p[k])
        elif e.topic.endswith("/env"):
            p["temp_c"] = round(p["temp_c"] + rng.gauss(0, cfg["temp_jitter"]), 1)
            p["lux"] = round(max(0.0, p["lux"] + rng.gauss(0, cfg["lux_jitter"])), 1)
        if "ts" in p and rng.random() < cfg["late"]:
            _shift_ts(p, -rng.uniform(0, cfg["late_s"]))
            if out and out[-1].t <= e.t and rng.random() < 0.3:  # sai thứ tự nhẹ: đổi chỗ với tin trước, giữ t tăng dần
                out[-1], e = e, out[-1]
                out[-1].t, e.t = e.t, out[-1].t
        out.append(e)
        if rng.random() < cfg["duplicate"]:
            out.append(copy.deepcopy(e))
    return out
