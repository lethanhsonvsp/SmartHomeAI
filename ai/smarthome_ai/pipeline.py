"""Khung hình -> detect -> track -> posture/zone -> state + transition. Không giữ trạng thái hiện diện."""
import time

from . import posture as pos
from . import transitions
from .tracker import Tracker, center


def run(source, detector, zones, pub, aspect=16 / 9, detect_fps=4.0, state_hz=1.0,
        stable_frames=3, now_iso=None, retry_s=2.0):
    from .publisher import now_iso as default_now
    now_iso = now_iso or default_now
    tracker, seq = Tracker(), 0
    last_t = last_state = None
    while True:
        item = source.read()
        if item is None:
            if not source.live:
                return seq
            seq += 1  # luồng mất: báo camera_ok=false, thử mở lại
            pub.send("state", {"ts": now_iso(), "seq": seq, "camera_ok": False, "persons": []})
            time.sleep(retry_s)
            source.reopen()
            continue
        t, frame = item
        if last_t is not None and t - last_t < 1 / detect_fps - 1e-6:
            continue
        dt, last_t = (t - last_t if last_t is not None else None), t
        persons = []
        for tr in tracker.update(detector.detect(frame), t):
            c = center(tr.box)
            speed = 0.0
            if tr.prev_center and tr.prev_t is not None and t > tr.prev_t:
                h = max(tr.box[3] - tr.box[1], 1e-6)
                speed = ((c[0] - tr.prev_center[0]) ** 2 + (c[1] - tr.prev_center[1]) ** 2) ** 0.5 / h / (t - tr.prev_t)
            raw = pos.classify(tr.box, speed, aspect, tr.det.keypoints)
            posture = pos.smooth(tr.postures, raw)
            foot = ((tr.box[0] + tr.box[2]) / 2, tr.box[3])
            zone = zones.zone_of(foot) or zones.zone_of(c)
            ch = transitions.step(tr, zone, stable_frames)
            if ch:
                pub.send("transition", {"ts": now_iso(), "track_id": tr.id, "from_zone": ch[0], "to_zone": ch[1],
                                        "direction": transitions.direction(*ch), "conf": round(tr.conf, 2)})
            if tr.zone:
                persons.append({"track_id": tr.id, "zone": tr.zone, "posture": posture, "conf": round(tr.conf, 2)})
        if last_state is None or t - last_state >= 1 / state_hz - 1e-6:
            last_state, seq = t, seq + 1
            pub.send("state", {"ts": now_iso(), "seq": seq, "camera_ok": True, "persons": persons})
