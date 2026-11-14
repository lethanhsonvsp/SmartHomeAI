"""Chống nhiễu: Hub thật + sim phát N01..N08 và SC nhiễu; đếm lệnh set trên bus, đọc /api/events. Cấu hình ở hubkit.py."""
import json, threading, time

import pytest

from hubkit import MANUAL_BLOCK, api, events, gap_stats, nhub, play_scenario, start_p100, dotnet_ok  # noqa: F401

LIGHT_ON = {"ts": "2026-11-14T12:00:00Z", "on": True}


def run(code, speed=8, wait=2, **kw):
    play_scenario(code, speed, **kw)
    time.sleep(wait)


def test_n01_posture_flicker_no_fan_spam(nhub):
    start_p100(nhub)
    run("N01", speed=20)  # 10 phút mô phỏng
    assert len(nhub.sets("fan", on=True)) <= 1, nhub.sets("fan")


def test_n02_temp_at_threshold_no_toggle(nhub):
    start_p100(nhub)
    run("N02")
    assert len(nhub.sets("fan", on=True)) <= 1 and not nhub.sets("fan", on=False), nhub.sets("fan")


def test_n03_dropped_frames_keep_occupied(nhub):
    seen, th = [], threading.Thread(target=play_scenario, args=("N03", 8))
    th.start()
    while th.is_alive():
        seen.append(api("/api/state")["occupancy"]); time.sleep(0.15)
    first = seen.index("Occupied")
    assert set(seen[first:]) == {"Occupied"}, [s for s in seen[first:] if s != "Occupied"][:5]
    bad = [e["message"] for e in events() if e["kind"] == "occupancy" and "Có người →" in e["message"]]
    assert not bad, bad


def test_n04_ai_status_flapping_keeps_one_alert(nhub):
    nhub.c.publish("home/device/light/state", json.dumps(LIGHT_ON), qos=1, retain=True)
    run("N04", wait=1)
    ids = {d["id"] for _, d in nhub.alerts()}
    assert len(ids) == 1, (ids, [(d["reason"]) for _, d in nhub.cancels()])
    assert not nhub.cancels(), nhub.cancels()


def test_n05_mute_p100_stops_after_retries(nhub):
    # RetrySchedule mặc định 60/120 s không rút gọn được bằng biến môi trường (binder .NET nối thêm vào mảng mặc định),
    # nên trong ~20 s thật chỉ thấy lệnh đầu: kiểm không dội (<= 3) và không tăng thêm sau khi chờ.
    start_p100(nhub, mute=True)
    run("N05", wait=4)
    n = len(nhub.sets("light", on=True, source="auto"))
    assert 1 <= n <= 3, n
    time.sleep(4)
    assert len(nhub.sets("light", on=True, source="auto")) == n


@pytest.mark.xfail(reason="cần > 180 s thật (RetrySchedule 60/120 s không rút gọn được bằng env); chạy tay với kịch bản dài", strict=False)
def test_n05b_unresponsive_logged(nhub):
    start_p100(nhub, mute=True)
    run("N05", wait=4)
    assert any("không phản hồi" in e["message"] for e in events())


def test_n06_external_off_blocks_auto(nhub):
    start_p100(nhub)
    run("N06", wait=1)
    on_state = [t for t, d in nhub.timed("home/device/fan/state") if d["on"]]
    off_state = [t for t, d in nhub.timed("home/device/fan/state") if not d["on"]]
    assert on_state and off_state, "quạt chưa bật trước khi bị tắt ngoài: test vô nghĩa"
    after = [t for t in nhub.set_times("fan", on=True, source="auto") if t > off_state[0]]
    assert not after, f"{len(after)} lệnh bật quạt sau khi bị tắt ngoài"


def test_n07_lux_at_threshold_one_light_on(nhub):
    start_p100(nhub)
    run("N07")
    assert len(nhub.sets("light", on=True)) <= 1, nhub.sets("light")


def test_n08_manual_always_wins(nhub):
    start_p100(nhub)
    run("N08", wait=1)
    manual = nhub.set_times("fan", source="manual")
    assert manual
    bad = [t for t in nhub.set_times("fan", source="auto") if any(0 <= t - m < MANUAL_BLOCK for m in manual)]
    assert not bad, f"{len(bad)} lệnh auto trong thời gian chặn"


# ---- Bền với nhiễu: SC giữ kết quả cuối như bản sạch (tiêu chí của test_e2e.py) ----
def _sc01(h): assert h.sets("light", on=True, source="auto")
def _sc03(h): assert h.sets("fan", on=True, source="auto")
def _sc07(h):
    assert h.alerts() and h.alerts()[0][1]["level"] == "first"


def _sc08(h):
    assert api("/api/state")["occupancy"] != "AwayConfirmed" and not h.alerts()


def _sc09(h):
    m = h.set_times("fan", source="manual")
    assert m and not [t for t in h.set_times("fan", on=True, source="auto") if t > m[0]]


CHECKS = {"SC01": (_sc01, True), "SC03": (_sc03, True), "SC07": (_sc07, False), "SC08": (_sc08, False), "SC09": (_sc09, True)}
MODERATE_XFAIL = {}  # điền theo kết quả đo thật: SC -> lý do


@pytest.mark.parametrize("level", ["mild", "moderate"])
@pytest.mark.parametrize("code", sorted(CHECKS))
def test_robust_to_noise(nhub, code, level, request):
    if level == "moderate" and code in MODERATE_XFAIL:
        request.applymarker(pytest.mark.xfail(reason=MODERATE_XFAIL[code], strict=False))
    check, p100 = CHECKS[code]
    if p100:
        start_p100(nhub)
    else:  # SC07 cần thiết bị đang bật
        nhub.c.publish("home/device/light/state", json.dumps(LIGHT_ON), qos=1, retain=True)
        time.sleep(0.5)
    run(code, noise=level, seed=7)
    check(nhub)
