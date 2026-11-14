"""E2E kịch bản thực tế R01..R20 (sim/realistic.py): Hub thật nén thời gian, đo qua bus và /api/state.

Chạy: E2E_MQTT_PORT=18832 sim/.venv/bin/python -m pytest tests/e2e/test_realistic.py -v -s   (cổng Hub E2E_RHUB_PORT, mặc định 5096)
Kỳ vọng theo LUẬT HIỆN HÀNH; ghi nhận và phát hiện xem docs/kich-ban-thuc-te.md. Mỗi test ~10-60 s thật.
"""
import json, os, threading, time

import pytest

import hubkit
from hubkit import MANUAL_BLOCK, api, events, hub_ctx, nhub, play_scenario, start_p100, dotnet_ok  # noqa: F401

SPEED = {"R18": 12, "R20": 50}  # còn lại 40
REMIND = 6  # Alerts__RemindEvery (s thật)
LIGHT_ON = {"ts": "2026-11-14T12:00:00Z", "on": True}


@pytest.fixture(autouse=True)
def _env(monkeypatch):
    port = int(os.environ.get("E2E_RHUB_PORT", 5096))  # tránh đụng test_noise (5098) chạy song song
    monkeypatch.setattr(hubkit, "PORT", port)
    monkeypatch.setattr(hubkit, "BASE", f"http://127.0.0.1:{port}")
    monkeypatch.setitem(hubkit.HUB_ENV, "Alerts__RemindEvery", f"00:00:{REMIND:02d}")


def go(code, bus=None, wait=2):
    """Phát R nén; trả về tập occupancy thấy được (thăm dò 0,15 s)."""
    seen, th = set(), threading.Thread(target=play_scenario, args=(code, SPEED.get(code, 40)))
    th.start()
    while th.is_alive():
        try:
            seen.add(api("/api/state")["occupancy"])
        except OSError:
            pass
        time.sleep(0.15)
    time.sleep(wait)
    return seen


def pre_on(bus, dev="light"):
    bus.c.publish(f"home/device/{dev}/state", json.dumps(LIGHT_ON), qos=1, retain=True)
    time.sleep(0.5)


def alert_log(bus):
    return [(t, json.loads(p)) for t, tp, p in bus.msgs if tp.startswith("home/alert/") and not tp.endswith("/cancel") and p]


def auto_on(bus, dev): return bus.sets(dev, on=True, source="auto")
def offs(bus, dev): return bus.sets(dev, on=False)


def test_r01_dark_morning_then_curtain(nhub):
    start_p100(nhub); go("R01")
    assert len(auto_on(nhub, "light")) == 1 and not offs(nhub, "light") and not nhub.sets("fan")


def test_r02_evening_home_to_sofa(nhub):
    start_p100(nhub); go("R02")
    assert len(auto_on(nhub, "light")) == 1 and not nhub.sets("fan")


def test_r03_hot_noon_nap(nhub):
    start_p100(nhub); go("R03")
    assert len(auto_on(nhub, "fan")) == 1 and not nhub.sets("light") and not offs(nhub, "fan")


def test_r04_night_ir_noise(nhub):
    start_p100(nhub); seen = go("R04")
    assert len(auto_on(nhub, "light")) <= 1 and not nhub.sets("fan") and not offs(nhub, "light")
    assert "AwayConfirmed" not in seen, seen


def test_r05_toilet_no_away(nhub):
    pre_on(nhub); seen = go("R05")
    assert "AwayConfirmed" not in seen, seen
    assert not alert_log(nhub)


def test_r06_parcel_one_minute(nhub):
    pre_on(nhub); go("R06")
    assert not alert_log(nhub), alert_log(nhub)


def test_r07_forgot_devices_then_back(nhub):
    go("R07")
    log = alert_log(nhub)
    assert log and log[0][1]["level"] == "first", log
    assert any(d["level"] == "reminder" for _, d in log), log
    assert len({d["id"] for _, d in log}) == 1
    c = nhub.cancels()
    assert len(c) == 1 and c[0][1]["reason"] == "person_returned", c
    assert not offs(nhub, "light") and not offs(nhub, "fan")  # hệ thống chỉ nhắc, không tự tắt


def test_r08_away_all_day_light_on(nhub):
    go("R08")
    log = alert_log(nhub)
    assert [d["level"] for _, d in log][:1] == ["first"] and sum(d["level"] == "first" for _, d in log) == 1
    rem = [t for t, d in log if d["level"] == "reminder"]
    assert len(rem) >= 2 and len({d["id"] for _, d in log}) == 1, log
    gaps = [b - a for a, b in zip(rem, rem[1:])]
    assert all(REMIND * 0.7 <= g <= REMIND * 2 for g in gaps), gaps  # đều đặn, không dồn
    assert not nhub.cancels() and not offs(nhub, "light")


def test_r09_guest_leaves_no_away(nhub):
    pre_on(nhub); seen = go("R09")
    assert "AwayConfirmed" not in seen, seen
    assert not alert_log(nhub)


def test_r10_whole_family_out(nhub):
    seen = go("R10")
    assert "AwayConfirmed" in seen, seen
    assert alert_log(nhub) and alert_log(nhub)[0][1]["level"] == "first"


def test_r11_ai_offline_while_tv(nhub):
    start_p100(nhub); seen = go("R11")
    assert not alert_log(nhub) and "AwayConfirmed" not in seen, seen
    assert not offs(nhub, "fan") and not nhub.sets("light") and len(auto_on(nhub, "fan")) <= 1


def test_r12_camera_blocked(nhub):
    pre_on(nhub); seen = go("R12")
    assert "Unknown" in seen and "AwayConfirmed" not in seen, seen
    assert not alert_log(nhub)


def test_r13_manual_off_blocks_auto(nhub):
    start_p100(nhub); go("R13")
    m = nhub.set_times("fan", source="manual")
    assert len(m) == 1
    assert any(t < m[0] for t in nhub.set_times("fan", on=True, source="auto")), "quạt chưa tự bật trước khi tắt tay"
    bad = [t for t in nhub.set_times("fan", source="auto") if 0 <= t - m[0] < MANUAL_BLOCK]
    assert not bad, f"{len(bad)} lệnh auto trong thời gian chặn"


def test_r14_manual_light_by_day(nhub):
    start_p100(nhub); go("R14")
    assert len(nhub.sets("light", on=True, source="manual")) == 1 and not nhub.sets("light", source="auto")
    assert not offs(nhub, "light")


def test_r15_temp_jitter_at_28(nhub):
    start_p100(nhub); go("R15")
    assert len(auto_on(nhub, "fan")) <= 1 and not offs(nhub, "fan")


def test_r16_rain_cooling(nhub):
    start_p100(nhub); go("R16")
    assert len(auto_on(nhub, "fan")) == 1, nhub.sets("fan")
    assert not offs(nhub, "fan"), "quạt bị tắt khi mát: luật hiện hành không có luật tắt"
    assert not nhub.sets("light")


def test_r17_plug_power_cut(nhub):
    start_p100(nhub); go("R17", wait=3)
    assert api("/health")
    n = len(auto_on(nhub, "fan"))
    assert n <= 3, nhub.sets("fan")
    for dev in ("fan", "light"):
        ts = nhub.set_times(dev)
        assert all(b - a >= 2.5 for a, b in zip(ts, ts[1:])), (dev, ts)
    print("R17 lệnh quạt:", [(d["on"], d["source"]) for d in nhub.sets("fan")])


def test_r18_hub_restart_grace(dotnet_ok, tmp_path, monkeypatch):
    grace = 6
    monkeypatch.setitem(hubkit.HUB_ENV, "Automation__StartupGrace", f"00:00:{grace:02d}")
    th = threading.Thread(target=play_scenario, args=("R18", SPEED["R18"]))
    with hub_ctx(tmp_path) as bus1:
        start_p100(bus1)
        th.start()
        time.sleep(10)  # grace 6 s qua, quạt đã bật
        assert auto_on(bus1, "fan")
    t0 = time.monotonic()
    with hub_ctx(tmp_path) as bus2:
        start_p100(bus2)
        th.join()
        time.sleep(3)
        ts = bus2.set_times("fan", on=True, source="auto")
        print("R18 lệnh quạt sau khởi động lại (s từ lúc chạy Hub):", [round(t - t0, 1) for t in ts])
        assert ts and len(ts) <= 1
        assert ts[0] - t0 >= grace - 0.5, f"auto sớm {ts[0] - t0:.1f}s < grace {grace}s"
        assert not offs(bus2, "fan")


def test_r19_kid_running(nhub):
    start_p100(nhub); go("R19")
    for dev in ("light", "fan"):
        assert len(nhub.sets(dev)) <= 2, (dev, nhub.sets(dev))
        assert not offs(nhub, dev)


def test_r20_long_day(nhub):
    start_p100(nhub); go("R20", wait=3)
    assert 1 <= len(auto_on(nhub, "light")) <= 2 and 1 <= len(auto_on(nhub, "fan")) <= 2
    assert not offs(nhub, "light") and not offs(nhub, "fan")
    total = len(nhub.sets("light")) + len(nhub.sets("fan"))
    assert total <= 8, total
    log = alert_log(nhub)
    assert log and log[0][1]["level"] == "first", log
    assert nhub.cancels() and nhub.cancels()[0][1]["reason"] == "person_returned"
    print("R20 tổng lệnh set:", total, "cảnh báo:", [d["level"] for _, d in log])
