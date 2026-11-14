"""Kịch bản thực tế R01..R20: tình huống đời thường dài 5-50 phút mô phỏng, kèm kỳ vọng hành vi hệ thống.

Dùng chung SCENARIOS với SC/N (đăng ký ở cuối sim/scenarios.py). Chạy nén bằng --speed; Hub e2e dùng speed 40-50.
Kỳ vọng phản ánh LUẬT HIỆN HÀNH (AutomationEngine/OccupancyEngine/AlertManager), kiểm đo ở tests/e2e/test_realistic.py.
"""
import random
from dataclasses import dataclass

from .scenarios import AI, DOOR, SCENARIOS, Event, _B, _leave

LIGHT, FAN = "home/device/light", "home/device/fan"
LV, SO = "livingroom", "sofa"


@dataclass
class Meta:
    title: str
    description: str
    expect: list
    requires: list


def _dev(b, t, dev, on):                                          # trạng thái thiết bị do ổ cắm báo (retained)
    b.ev.append(Event(t, f"home/device/{dev}/state", {"on": on}, True))


def _enter(b, t, tid=1, zone=LV, posture="standing"):
    b.door(t, True); b.door(t + 3, False)
    b.move(t + 1, tid, "door", zone, "in")


def _env_ramp(b, t0, t1, a, c, lux, step=20):
    n = max(1, int((t1 - t0) / step))
    for i in range(n + 1):
        b.env(t0 + i * step, round(a + (c - a) * i / n, 1), lux)


def _hold(b, t0, t1, persons, **kw):
    b.state(t0, t1, persons, hz=0.5, **kw)


def r01(b):                                                       # sáng sớm, rèm kín rồi mở rèm
    b.env(0, 24.0, 3)
    _hold(b, 1, 20, [])
    _enter(b, 20)
    _hold(b, 21, 300, [(1, LV, "standing")])
    for t, lux in [(100, 3), (120, 20), (140, 80), (160, 200), (200, 350)]: b.env(t, 24.5, lux)


def r02(b):                                                       # tối về nhà, vào cửa rồi ra sofa ngồi
    b.env(0, 27.0, 5)
    _hold(b, 1, 20, [])
    _enter(b, 20)
    _hold(b, 21, 80, [(1, LV, "moving")])
    _hold(b, 81, 300, [(1, SO, "sitting")])


def r03(b):                                                       # trưa nóng, xem TV rồi ngủ quên
    _env_ramp(b, 0, 900, 30.0, 33.0, 400)
    _hold(b, 1, 120, [(1, SO, "sitting")])
    _hold(b, 121, 900, [(1, SO, "lying")])


def r04(b):                                                       # đêm ngủ quên ở sofa, camera hồng ngoại nhiễu tư thế
    rng = random.Random(4)
    _env_ramp(b, 0, 900, 27.0, 24.0, 3)
    _hold(b, 1, 60, [(1, LV, "standing")])
    for t in range(61, 900, 2):
        b.state(t, t, [(1, SO, rng.choice(["lying", "lying", "sitting", "unknown", "moving"]))])


def r05(b):                                                       # đi vệ sinh 2 phút (thấy ở lối sang khu khác) rồi về
    b.env(0, 26.0, 150)
    _hold(b, 1, 120, [(1, SO, "sitting")])
    b.move(120, 1, SO, "other_exit", "out")
    _hold(b, 120, 240, [(1, "other_exit", "standing")])
    b.move(240, 1, "other_exit", LV, "in")
    _hold(b, 241, 600, [(1, SO, "sitting")])


def r06(b):                                                       # ra cửa nhận hàng 1 phút rồi quay vào
    b.env(0, 26.0, 150)
    _hold(b, 1, 60, [(1, LV, "standing")])
    _leave(b, 60)
    _hold(b, 60, 120, [])
    b.door(121, True); b.move(122, 1, "door", LV, "in"); b.door(125, False)
    _hold(b, 122, 300, [(1, LV, "standing")])


def _away_devices(b, fan=True, light=True):
    b.env(0, 29.0, 150)
    if light: _dev(b, 0, "light", True)
    if fan: _dev(b, 0, "fan", True)


def r07(b):                                                       # đi làm quên quạt và đèn; về sớm
    _away_devices(b)
    _hold(b, 1, 60, [(1, LV, "standing")])
    _leave(b, 60)
    _hold(b, 60, 800, [])
    b.door(801, True); b.move(802, 1, "door", LV, "in"); b.door(805, False)
    _hold(b, 802, 900, [(1, LV, "standing")])


def r08(b):                                                       # đi vắng cả ngày để đèn bật
    _away_devices(b, fan=False)
    _hold(b, 1, 60, [(1, LV, "standing")])
    _leave(b, 60)
    _hold(b, 60, 1500, [])


def r09(b):                                                       # khách đến (2 người), một người ra về
    b.env(0, 26.0, 150)
    _hold(b, 1, 60, [(1, LV, "sitting")])
    _enter(b, 60, tid=2)
    _hold(b, 62, 240, [(1, LV, "sitting"), (2, LV, "standing")])
    b.move(240, 2, LV, "door", "out"); b.door(241, True); b.door(244, False)
    _hold(b, 240, 600, [(1, LV, "sitting")])


def r10(b):                                                       # cả nhà ra ngoài: vắng thật
    _away_devices(b, fan=False)
    _hold(b, 1, 60, [(1, LV, "standing"), (2, LV, "standing")])
    b.move(60, 1, LV, "door", "out"); b.door(61, True)
    _hold(b, 60, 70, [(2, LV, "standing")])
    b.move(70, 2, LV, "door", "out"); b.door(71, False)
    _hold(b, 70, 700, [])


def r11(b):                                                       # mất internet, AI chớp offline 90 s khi xem TV
    _env_ramp(b, 0, 600, 31.0, 31.0, 150, 30)
    _hold(b, 1, 300, [(1, SO, "lying")])
    b.ev.append(Event(300, "home/ai/status", {"online": False}, True))
    b.ev.append(Event(390, "home/ai/status", {"online": True}, True))
    _hold(b, 392, 600, [(1, SO, "lying")])


def r12(b):                                                       # camera lỗi / bị che ống kính
    _dev(b, 0, "light", True)
    b.env(0, 27.0, 150)
    _hold(b, 1, 60, [(1, LV, "standing")])
    _hold(b, 60, 200, [], camera_ok=False)
    _hold(b, 202, 300, [(1, LV, "standing")])


def r13(b):                                                       # nóng nhưng người dùng tắt quạt tay
    b.env(0, 33.0, 150)
    _hold(b, 1, 600, [(1, SO, "lying")])
    b.ev.append(Event(150, f"{FAN}/set", {"on": False, "source": "manual"}))
    for t in range(180, 600, 60): b.env(t, 33.0, 150)


def r14(b):                                                       # ban ngày bật đèn tay dù sáng
    b.env(0, 26.0, 500)
    _hold(b, 1, 400, [(1, LV, "standing")])
    b.ev.append(Event(100, f"{LIGHT}/set", {"on": True, "source": "manual"}))
    for t in range(60, 400, 60): b.env(t, 26.0, 500)


def r15(b):                                                       # nhiệt dao động quanh 28 khi nằm sofa (cảm biến nhiễu)
    rng = random.Random(15)
    b.env(0, 27.8, 150)
    _hold(b, 1, 900, [(1, SO, "lying")])
    for t in range(10, 900, 10): b.env(t, round(28.0 + rng.gauss(0, 0.6), 1), 150)


def r16(b):                                                       # mưa chiều: 31 -> 25 độ
    _env_ramp(b, 0, 1200, 31.0, 25.0, 150, 30)
    _hold(b, 1, 1200, [(1, SO, "lying")])


def r17(b):                                                       # mất điện: ổ cắm offline 3 phút, về lại ở trạng thái tắt
    b.env(0, 33.0, 150)
    _hold(b, 1, 900, [(1, SO, "lying")])
    b.ev.append(Event(300, "home/device/status", {"online": False}, True))
    b.ev.append(Event(480, "home/device/status", {"online": True}, True))
    _dev(b, 481, "fan", False); _dev(b, 481, "light", False)
    for t in range(30, 900, 30): b.env(t, 33.0, 150)


def r18(b):                                                       # Hub khởi động lại giữa lúc nằm sofa (test tự khởi động lại Hub)
    b.env(0, 33.0, 150)
    _hold(b, 1, 300, [(1, SO, "lying")])
    for t in range(5, 300, 5): b.env(t, 33.0, 150)


def r19(b):                                                       # trẻ chạy nhảy qua lại sofa/phòng khách, người lớn ngồi sofa
    rng = random.Random(19)
    for t in range(0, 600, 10): b.env(t, 30.0, 40)
    for t in range(1, 600):
        kid = (2, rng.choice([SO, LV]), rng.choice(["moving", "moving", "standing", "sitting", "lying"]))
        b.state(t, t, [(1, SO, "sitting"), kid])


def r20(b):                                                       # ngày dài nén: đêm, sáng, trưa nóng ngủ trưa, đi vắng, tối
    b.env(0, 25.0, 2)
    _hold(b, 1, 200, [])
    _enter(b, 200)
    _hold(b, 203, 600, [(1, LV, "standing")])
    b.env(400, 26.0, 250)
    _env_ramp(b, 600, 1500, 27.0, 33.0, 400)
    _hold(b, 600, 900, [(1, SO, "sitting")])
    _hold(b, 900, 1500, [(1, SO, "lying")])
    b.state(1500, 1500, [(1, LV, "standing")]); b.env(1500, 33.0, 400)
    _leave(b, 1560)
    _hold(b, 1560, 2300, [])
    b.env(1800, 31.0, 200)
    _enter(b, 2300)
    _hold(b, 2303, 3000, [(1, SO, "sitting")])
    _env_ramp(b, 2300, 3000, 29.0, 25.0, 5, 50)


M = {
    "R01": (r01, Meta("Sáng sớm vào phòng khách tối rồi mở rèm", "Người dậy vào phòng khách lúc lux 3, đèn tự bật; vài phút sau mở rèm, lux tăng lên 350.",
            ["Đèn tự bật đúng 1 lần", "Lux tăng không làm đèn bật/tắt thêm", "Không bật quạt"], ["p100"])),
    "R02": (r02, Meta("Tối về nhà đi tới sofa ngồi", "Về nhà lúc tối (lux 5, 27 °C): vào cửa, đi quanh phòng khách rồi ngồi sofa.",
            ["Đèn bật 1 lần khi vào", "Không bật quạt (ngồi, dưới 28 °C)"], ["p100", "esp"])),
    "R03": (r03, Meta("Trưa nóng nằm xem TV rồi ngủ quên", "Nhiệt 30 lên 33 °C, ngồi sofa rồi nằm xuống ngủ quên; trời sáng.",
            ["Quạt tự bật 1 lần sau khi nằm", "Không bật đèn (lux 400)", "Không lệnh tắt nào"], ["p100"])),
    "R04": (r04, Meta("Đêm ngủ quên ở sofa, camera hồng ngoại nhiễu", "Đêm, nhiệt giảm 27 xuống 24 °C; tư thế nhấp nháy lying/sitting/unknown do camera IR.",
            ["Đèn bật tối đa 1 lần", "Không bật quạt (dưới 28 °C)", "Vẫn có người, không bao giờ AwayConfirmed"], ["p100"])),
    "R05": (r05, Meta("Đi vệ sinh 2 phút rồi về", "Ngồi sofa, sang khu khác (vẫn thấy ở lối đi) 2 phút rồi quay lại.",
            ["Không bao giờ AwayConfirmed", "Không cảnh báo"], [])),
    "R06": (r06, Meta("Ra cửa nhận hàng 1 phút", "Ra cửa chính (cửa mở), vắng khung hình 1 phút, quay vào.",
            ["Không cảnh báo"], ["esp"])),
    "R07": (r07, Meta("Đi làm quên tắt quạt và đèn, về sớm", "Ra khỏi nhà để quạt và đèn bật: cảnh báo đầu, nhắc lại; người về giữa chừng.",
            ["Cảnh báo first sau khi xác nhận vắng", "Có nhắc lại cùng id", "Hủy person_returned khi về", "Hệ thống không tự tắt thiết bị"], ["esp"])),
    "R08": (r08, Meta("Đi vắng cả ngày để đèn bật", "Ra khỏi nhà để đèn bật, không về: cảnh báo đầu rồi nhắc lại đều đặn.",
            ["Cảnh báo first 1 lần", "Nhắc lại đều đặn cùng id, không dồn", "Không tự tắt đèn"], ["esp"])),
    "R09": (r09, Meta("Khách đến, một người ra về", "Hai người trong phòng, khách ra về, chủ nhà ngồi lại.",
            ["Không bao giờ AwayConfirmed", "Không cảnh báo"], ["esp"])),
    "R10": (r10, Meta("Cả nhà ra ngoài", "Hai người ra cửa cách nhau 10 giây, đèn còn bật.",
            ["AwayConfirmed", "Cảnh báo first (đèn bật)"], ["esp"])),
    "R11": (r11, Meta("Mất internet, AI offline 90 s khi xem TV", "Nằm sofa nóng 31 °C; AI offline 90 s rồi online lại.",
            ["Không suy ra nhà vắng (không cảnh báo)", "Quạt/đèn không bị tắt hay bật lại"], ["p100"])),
    "R12": (r12, Meta("Camera lỗi hoặc bị che ống kính", "Camera_ok=false 140 s khi đèn đang bật.",
            ["Trạng thái Unknown", "Không AwayConfirmed, không cảnh báo"], [])),
    "R13": (r13, Meta("Nóng nhưng người dùng tắt quạt tay", "Nằm sofa 33 °C, quạt tự bật rồi người dùng tắt tay.",
            ["Không lệnh auto bật quạt trong thời gian chặn tay"], ["p100"])),
    "R14": (r14, Meta("Bật đèn tay ban ngày", "Lux 500 vẫn bật đèn tay.",
            ["Chỉ 1 lệnh manual, không lệnh auto đèn", "Không tắt đèn"], ["p100"])),
    "R15": (r15, Meta("Nhiệt dao động quanh 28 °C", "Nằm sofa, cảm biến nhiễu Gauss 0,6 °C quanh ngưỡng.",
            ["Quạt bật tối đa 1 lần", "Không lệnh tắt"], ["p100"])),
    "R16": (r16, Meta("Mưa chiều: 31 xuống 25 °C", "Nằm sofa, trời mát dần.",
            ["Quạt bật 1 lần", "Không bật thêm", "Quạt không tự tắt (chưa có luật tắt)"], ["p100"])),
    "R17": (r17, Meta("Mất điện, ổ cắm offline 3 phút", "Quạt/đèn offline 180 s rồi về ở trạng thái tắt, người vẫn nằm sofa nóng.",
            ["Không dồn lệnh (khoảng cách set tối thiểu 3 s)", "Số lệnh bật quạt tối đa 3", "Hub vẫn sống, ổ cắm online lại"], ["p100"])),
    "R18": (r18, Meta("Hub khởi động lại giữa lúc nằm sofa", "Hub tắt và chạy lại khi người đang nằm sofa 33 °C.",
            ["Không auto trong thời gian StartupGrace", "Sau grace bật quạt tối đa 1 lần"], ["p100"])),
    "R19": (r19, Meta("Trẻ chạy nhảy qua lại sofa/phòng khách", "Trẻ đổi vùng/tư thế mỗi giây, người lớn ngồi sofa; tối nhẹ, 30 °C.",
            ["Mỗi thiết bị tối đa 2 lệnh set", "Không lệnh tắt"], ["p100"])),
    "R20": (r20, Meta("Ngày dài tổng hợp", "Đêm, sáng, trưa nóng ngủ trưa, đi vắng, tối.",
            ["Đèn bật 1-2 lần, quạt bật 1-2 lần", "Tổng lệnh set tối đa 8", "Cảnh báo first khi vắng và hủy khi về"], ["p100", "esp"])),
}
SCENARIOS.update({k: v[0] for k, v in M.items()})


def catalog():
    """Mảng cho `python -m sim list --json`."""
    from .scenarios import build
    return [{"code": k, "title": m.title, "description": m.description, "durationSec": round(build(k)[-1].t),
             "expect": m.expect, "requires": m.requires} for k, (_, m) in M.items()]
