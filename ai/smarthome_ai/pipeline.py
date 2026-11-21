"""Khung hình -> detect -> track -> posture/zone -> state + transition. Không giữ trạng thái hiện diện."""
import time

from . import posture as pos
from . import transitions
from .tracker import Tracker, center


def run(source, detector, zones, pub, aspect=16 / 9, detect_fps=4.0, state_hz=1.0,
        stable_frames=6, posture_window=8, now_iso=None, retry_s=2.0, viewer=None, sofa_lying=False):
    from .publisher import now_iso as default_now
    now_iso = now_iso or default_now
    tracker, seq = Tracker(), 0
    samples = viewer.samples if viewer else None
    last_t = last_state = None
    shown = []  # lớp phủ gần nhất cho viewer
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
        if viewer:
            viewer.update(frame, shown)
        if last_t is not None and t - last_t < 1 / detect_fps - 1e-6:
            continue
        dt, last_t = (t - last_t if last_t is not None else None), t
        persons, shown = [], []
        matched = tracker.update(detector.detect(frame), t)
        cap = viewer.capture if viewer else None
        if cap and time.time() >= cap["start"]:  # đang ghi mẫu tư thế: người rõ nhất, cách nhau cap["gap"] giây cho mẫu khác nhau
            best = max(matched, key=lambda tr: tr.conf) if matched else None
            f = pos.features(best.det.keypoints, aspect) if best else None
            if f and time.time() - cap.get("last", 0) >= cap.get("gap", 0):
                samples.add(cap["label"], f, viewer.thumb(frame, best.box, best.det.keypoints))
                cap["last"], cap["left"] = time.time(), cap["left"] - 1
            if cap["left"] <= 0 or time.time() > cap["start"] + 60:  # 60 giây không đủ khung thì dừng
                samples.save()
                viewer.capture = None
        for tr in matched:
            c = center(tr.box)
            speed = 0.0
            if tr.prev_center and tr.prev_t is not None and t > tr.prev_t:
                h = max(tr.box[3] - tr.box[1], 1e-6)
                speed = ((c[0] - tr.prev_center[0]) ** 2 + (c[1] - tr.prev_center[1]) ** 2) ** 0.5 / h / (t - tr.prev_t)
            raw = pos.classify(tr.box, speed, aspect, tr.det.keypoints, samples)
            # cam góc trần soi xuống: hình học không tách được đứng/ngồi/nằm, nên tâm hộp trong sofa mà đứng yên thì coi là nằm.
            # Đã ghi đủ mẫu tư thế thì tin mẫu, không ép nữa.
            if not (samples and samples.trained) and (viewer.cfg["sofa_lying"] if viewer else sofa_lying) and raw in ("sitting", "standing") \
                    and zones.zone_for(tr.box, "lying") == "sofa":
                raw = "lying"
            posture = pos.smooth(tr.postures, raw, posture_window, tr.posture)
            tr.posture = posture
            zone = zones.zone_for(tr.box, raw if posture == "unknown" else posture)
            ch = transitions.step(tr, zone, stable_frames)
            if ch:
                pub.send("transition", {"ts": now_iso(), "track_id": tr.id, "from_zone": ch[0], "to_zone": ch[1],
                                        "direction": transitions.direction(*ch), "conf": round(tr.conf, 2)})
            shown.append({"box": tr.box, "kp": tr.det.keypoints, "id": tr.id, "zone": zone or tr.zone,
                          "posture": posture, "conf": tr.conf})
            if tr.zone:
                persons.append({"track_id": tr.id, "zone": tr.zone, "posture": posture, "conf": round(tr.conf, 2)})
        # track mất tạm (missed <= max_missed): vẫn phát với tư thế/vùng cuối, tránh hiện diện nhảy
        persons += [{"track_id": tr.id, "zone": tr.zone, "posture": tr.posture or "unknown", "conf": round(tr.conf, 2)}
                    for tr in tracker.tracks if tr.missed > 0 and tr.zone]
        if viewer:
            viewer.update(frame, shown)
        if last_state is None or t - last_state >= 1 / state_hz - 1e-6:
            last_state, seq = t, seq + 1
            pub.send("state", {"ts": now_iso(), "seq": seq, "camera_ok": True, "persons": persons})
