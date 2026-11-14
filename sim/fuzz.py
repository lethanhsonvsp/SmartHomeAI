"""Chuỗi sự kiện ngẫu nhiên (theo seed) trộn mọi loại đầu vào ở mức nhiễu nặng, để kiểm bất biến an toàn rơ-le.

Event.topic: topic MQTT thường; "API:<đường dẫn>" = POST tới Hub (payload là body); "RAW:<topic>" = gói sai (payload {"raw": chuỗi}).
"""
import json, random, time, urllib.request
from datetime import datetime, timedelta, timezone

from .common import connect, publish
from .noise import inject
from .scenarios import AI, DOOR, ENV, Event

ZONES = ["livingroom", "sofa", "door", "other_exit"]
POSTURES = ["standing", "sitting", "lying", "moving", "unknown"]


def gen(seed: int, duration: float | None = None) -> list[Event]:
    rng = random.Random(seed)
    dur = duration or rng.uniform(180, 300)
    ev, seq = [Event(0, "home/ai/status", {"online": True}, True)], 0
    people, tid = [], 1
    t = 1.0
    while t < dur:  # ai/state 1 Hz (đôi khi 2 Hz); người, tư thế, zone, track_id, camera_ok nhảy
        if rng.random() < 0.06 and len(people) < 2:
            people.append([tid, rng.choice(ZONES), rng.choice(POSTURES)]); tid += 1
        if people and rng.random() < 0.05:
            people.pop(rng.randrange(len(people)))
        for q in people:
            if rng.random() < 0.15: q[2] = rng.choice(POSTURES)
            if rng.random() < 0.10: q[1] = rng.choice(ZONES) if rng.random() < 0.5 else rng.choice(["sofa", "livingroom"])
            if rng.random() < 0.03: q[0] = tid; tid += 1
        if rng.random() > 0.08:  # mất khung
            seq += 1
            cam = rng.random() > 0.04
            ev.append(Event(t, f"{AI}/state", {"seq": seq, "camera_ok": cam, "persons": [
                {"track_id": q[0], "zone": q[1], "posture": q[2], "conf": round(rng.uniform(.3, .99), 2)} for q in people]}))
        if rng.random() < 0.04 and people:
            ev.append(Event(t, f"{AI}/transition", {"track_id": people[0][0], "from_zone": rng.choice(ZONES), "to_zone": rng.choice(ZONES),
                                                    "direction": rng.choice(["in", "out", "unknown"]), "conf": 0.7}))
        t += rng.choice([0.5, 1, 1, 1, 1.5])
    t = 0.0
    while t < dur:  # AI status chớp (kèm đoạn chớp dày)
        t += rng.expovariate(1 / 25)
        for _ in range(rng.choice([1, 1, 4])):
            ev.append(Event(t, "home/ai/status", {"online": rng.random() < 0.5}, True)); t += rng.uniform(1, 4)
        ev.append(Event(t, "home/ai/status", {"online": True}, True))
    for k in range(0, int(dur), 5):  # env quanh ngưỡng 28 °C và 50 lux, mất mẫu
        if rng.random() > 0.15:
            ev.append(Event(k + rng.random(), ENV, {"temp_c": round(rng.gauss(28, 1.2), 1), "humidity": 60.0, "lux": round(max(0, rng.gauss(50, 15)), 1)}))
    for _ in range(rng.randint(2, 5)):  # cửa đóng mở dồn dập
        t0 = rng.uniform(0, dur)
        for i in range(rng.randint(2, 8)):
            ev.append(Event(t0 + i * rng.uniform(.3, 2), DOOR, {"open": i % 2 == 0}, True))
    for _ in range(rng.randint(8, 20)):  # thiết bị báo state bất thường (tắt ngoài, báo sai/trễ)
        ev.append(Event(rng.uniform(0, dur), f"home/device/{rng.choice(['light', 'fan'])}/state", {"on": rng.random() < 0.5}, True))
    for _ in range(3):  # gói sai: phải bị bỏ qua
        ev.append(Event(rng.uniform(0, dur), "RAW:" + rng.choice([f"{AI}/state", ENV, "home/device/fan/state"]),
                        {"raw": rng.choice(["{không phải json", '{"ts":"x","on":"maybe"}', "[]", ""])}))
    api = []
    for _ in range(rng.randint(3, 8)):  # bấm tay dồn dập
        t0, dev = rng.uniform(0, dur), rng.choice(["light", "fan"])
        for i in range(rng.randint(3, 20)):
            api.append(Event(t0 + i * rng.uniform(.1, .5), f"API:/api/device/{dev}", {"on": rng.random() < 0.5}))
    for _ in range(rng.randint(1, 4)):
        api.append(Event(rng.uniform(0, dur), "API:/api/mode", {"auto": rng.random() < 0.5}))
    return sorted(ev + api, key=lambda e: e.t)


def stamp(events, speed, noise="heavy", seed=0):
    """Gán ts (giờ thật nén theo speed) cho gói MQTT thường, rồi tiêm nhiễu nặng (trùng, trễ, sai thứ tự)."""
    base = datetime.now(timezone.utc)
    ts = lambda e: (base + timedelta(seconds=e.t / speed)).strftime("%Y-%m-%dT%H:%M:%SZ")
    plain = [e for e in events if e.topic.startswith("home/")]
    for e in plain:
        e.payload = {"ts": ts(e), **e.payload}
    other = [e for e in events if not e.topic.startswith("home/")]
    return sorted(inject(plain, noise, seed) + other, key=lambda e: e.t)


def describe(events, t, n=20):
    """n sự kiện gần thời điểm t nhất (sắp theo thời gian), để tái hiện."""
    near = sorted(sorted(events, key=lambda e: abs(e.t - t))[:n], key=lambda e: e.t)
    return "\n".join(f"  {e.t:7.1f}s {e.topic} {json.dumps({k: v for k, v in e.payload.items() if k != 'ts'}, ensure_ascii=False)}" for e in near)


def play(events, speed=8.0, host="localhost", port=1883, api_base="http://127.0.0.1:5098"):
    """Phát chuỗi đã stamp. Trả mốc time.monotonic() lúc t=0."""
    c = connect("sim-fuzz", None, host, port)
    for _ in range(100):
        if c.is_connected(): break
        time.sleep(0.05)
    t0 = time.monotonic()
    for e in events:
        time.sleep(max(0, e.t / speed - (time.monotonic() - t0)))
        try:
            if e.topic.startswith("API:"):
                req = urllib.request.Request(api_base + e.topic[4:], data=json.dumps(e.payload).encode(), method="POST",
                                             headers={"Content-Type": "application/json"})
                urllib.request.urlopen(req, timeout=5).read()
            elif e.topic.startswith("RAW:"):
                c.publish(e.topic[4:], e.payload["raw"], qos=1)
            else:
                publish(c, e.topic, e.payload, e.retain)
        except OSError:
            pass  # Hub từ chối/đứt tạm thời: bất biến 5 kiểm Hub còn sống
    c.disconnect()
    return t0
