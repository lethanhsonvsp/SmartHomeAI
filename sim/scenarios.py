"""Kịch bản SC01–SC11 dạng dữ liệu: Event(t giây, topic, payload không ts, retain).

Mỗi kịch bản sinh chuỗi sự kiện AI/cảm biến; Hub phải phản ứng (đèn, quạt, cảnh báo).
"""
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

AI, ENV, DOOR = "home/ai/livingroom", "home/sensor/esp1/env", "home/sensor/esp1/door"


@dataclass
class Event:
    t: float
    topic: str
    payload: dict
    retain: bool = False


class _B:
    """Dựng danh sách sự kiện; tự tăng seq của ai/state."""

    def __init__(self):
        self.ev, self.seq = [], 0
        self.ev.append(Event(0, "home/ai/status", {"online": True}, True))
        self.ev.append(Event(0, DOOR, {"open": False}, True))

    def env(self, t, temp, lux):
        self.ev.append(Event(t, ENV, {"temp_c": temp, "humidity": 60.0, "lux": lux}))

    def door(self, t, open_):
        self.ev.append(Event(t, DOOR, {"open": open_}, True))

    def state(self, t0, t1, persons, camera_ok=True, hz=1):
        """ai/state mỗi 1/hz giây trong [t0, t1]; persons: [(id, zone, posture)]."""
        n = int((t1 - t0) * hz)
        for i in range(n + 1):
            self.seq += 1
            self.ev.append(Event(t0 + i / hz, f"{AI}/state", {"seq": self.seq, "camera_ok": camera_ok, "persons": [
                {"track_id": p[0], "zone": p[1], "posture": p[2], "conf": 0.85} for p in persons]}))

    def move(self, t, tid, a, b, direction):
        self.ev.append(Event(t, f"{AI}/transition", {"track_id": tid, "from_zone": a, "to_zone": b,
                                                     "direction": direction, "conf": 0.8}))

    def done(self):
        return sorted(self.ev, key=lambda e: e.t)  # sort ổn định: giữ thứ tự cùng t


def _enter(b, lux, temp, posture="standing", zone="livingroom"):
    b.env(0, temp, lux)
    b.state(1, 3, [])
    b.door(4, True); b.door(7, False)
    b.move(5, 1, "door", "livingroom", "in")
    b.state(5, 20, [(1, zone, posture)])


def sc01(b): _enter(b, 5, 28.0)                                  # tối: đèn bật
def sc02(b): _enter(b, 400, 28.0)                                # sáng: đèn giữ
def sc03(b): _enter(b, 150, 33.0, "lying", "sofa")               # nóng: quạt bật
def sc04(b): _enter(b, 150, 24.0, "lying", "sofa")               # mát: xét thói quen


def sc05(b):                                                     # sang khu khác, cửa không mở
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "sofa", "lying")])
    b.move(11, 1, "sofa", "other_exit", "out")
    b.state(11, 60, [])


def _leave(b, t):                                                # ra cửa chính
    b.move(t, 1, "livingroom", "door", "out")
    b.door(t + 1, True); b.door(t + 4, False)


def sc06(b):                                                     # ra cửa rồi quay lại sau 20 s
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "livingroom", "standing")])
    _leave(b, 11)
    b.state(11, 30, [])
    b.door(31, True); b.move(32, 1, "door", "livingroom", "in"); b.door(35, False)
    b.state(32, 60, [(1, "livingroom", "standing")])


def sc07(b):                                                     # ra ngoài, nhà vắng 3 phút
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "livingroom", "standing")])
    _leave(b, 11)
    b.state(11, 190, [])


def sc08(b):                                                     # hai người, một người ra
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "livingroom", "standing"), (2, "sofa", "sitting")])
    _leave(b, 11)
    b.state(11, 190, [(2, "sofa", "sitting")])


def sc09(b):                                                     # nằm sofa nóng, người dùng tắt quạt tay
    b.env(0, 33.0, 150)
    b.state(1, 60, [(1, "sofa", "lying")])
    b.ev.append(Event(20, "home/device/fan/set", {"on": False, "source": "manual"}))
    b.env(30, 33.2, 150)


def sc10(b):                                                     # camera mất kết nối
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "livingroom", "standing")])
    b.state(11, 20, [], camera_ok=False)
    b.ev.append(Event(21, "home/ai/status", {"online": False}, True))


def sc11(b):                                                     # ra ngoài rồi trở về sau khi cảnh báo
    b.env(0, 28.0, 150)
    b.state(1, 10, [(1, "livingroom", "standing")])
    _leave(b, 11)
    b.state(11, 160, [])
    b.door(161, True); b.move(162, 1, "door", "livingroom", "in"); b.door(165, False)
    b.state(162, 200, [(1, "livingroom", "standing")])


SCENARIOS = {f"SC{i:02d}": f for i, f in enumerate(
    [sc01, sc02, sc03, sc04, sc05, sc06, sc07, sc08, sc09, sc10, sc11], 1)}


def build(code: str, base: datetime | None = None, scale: float = 1.0) -> list[Event]:
    """Sự kiện của kịch bản; thêm ts = base + t*scale (base mặc định: bây giờ UTC)."""
    base = base or datetime.now(timezone.utc)
    b = _B()
    SCENARIOS[code.upper()](b)
    out = b.done()
    for e in out:
        e.payload = {"ts": (base + timedelta(seconds=e.t * scale)).strftime("%Y-%m-%dT%H:%M:%SZ"), **e.payload}
    return out
