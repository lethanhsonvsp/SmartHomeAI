"""Bật tắt dồn dập ngắn hạn (S01..S06): đo khoảng cách thật giữa các lệnh set trên bus. Mặc định Hub: MinCommandInterval 3 s, MinHold 10 s."""
import threading, time

import pytest

from hubkit import MANUAL_BLOCK, api, gap_stats, has_feature, nhub, play_scenario, start_p100, dotnet_ok  # noqa: F401

NEW = not has_feature("MinCommandInterval")
gated = pytest.mark.xfail(NEW, reason="chờ Core/Hub chống dội ngắn hạn", strict=False)


def press(dev, seq, every):
    out = []
    for on in seq:
        out.append(api(f"/api/device/{dev}", {"on": on}))
        time.sleep(every)
    return out


def dev_on(dev):
    return api("/api/state")["devices"].get(dev, {}).get("on")


@gated
def test_s01_user_mashes_fan_20_times_in_5s(nhub):
    start_p100(nhub)
    seq = [i % 2 == 0 for i in range(20)]  # bật, tắt, bật ... cuối cùng là tắt
    res = press("fan", seq, 0.25)
    time.sleep(5)
    t = nhub.set_times("fan")
    print("S01 sets", len(t), "gap min/median", gap_stats(t), "coalesced", sum(bool(r.get("coalesced")) for r in res))
    assert len(t) <= 2, nhub.sets("fan")
    assert nhub.sets("fan")[-1]["on"] is seq[-1] and dev_on("fan") is seq[-1]


@gated
def test_s02_auto_wants_on_condition_lost_and_back(nhub):
    start_p100(nhub)
    play_scenario("S02", 1)
    time.sleep(2)
    ch = nhub.set_times("fan", source="auto")
    print("S02 auto sets", len(ch), "gap", gap_stats(ch))
    assert all(b - a >= 10 for a, b in zip(ch, ch[1:])), ch


@gated
def test_s03_manual_interleaved_with_auto_30s(nhub):
    start_p100(nhub)
    th = threading.Thread(target=play_scenario, args=("S03", 1)); th.start()
    time.sleep(3)
    press("fan", [i % 2 == 0 for i in range(14)], 2.0)
    th.join(); time.sleep(2)
    man = nhub.set_times("fan", source="manual")
    bad_auto = [t for t in nhub.set_times("fan", source="auto") if any(0 <= t - m < MANUAL_BLOCK for m in man)]
    allt = nhub.set_times("fan")
    print("S03 sets", len(allt), "gap min/median", gap_stats(allt))
    assert not bad_auto
    assert all(b - a >= 2.95 for a, b in zip(allt, allt[1:])), [round(b - a, 2) for a, b in zip(allt, allt[1:])]


@gated
def test_s04_both_devices_mashed_independently(nhub):
    start_p100(nhub)
    for i in range(20):
        api("/api/device/fan", {"on": i % 2 == 0}); api("/api/device/light", {"on": i % 2 == 1}); time.sleep(0.25)
    time.sleep(5)
    for dev, last in (("fan", False), ("light", True)):
        t = nhub.set_times(dev)
        print("S04", dev, len(t), gap_stats(t))
        # bấm kéo dài ~5-6 s: tối đa 2 lệnh trong lúc bấm + 1 lệnh gộp cuối khi hết khoảng chờ
        assert 1 <= len(t) <= 3 and all(b - a >= 2.95 for a, b in zip(t, t[1:])), (dev, nhub.sets(dev))
        assert nhub.sets(dev)[-1]["on"] is last and dev_on(dev) is last


@gated
def test_s05_repeated_same_state_commands_not_sent(nhub):
    start_p100(nhub)
    for _ in range(6):
        api("/api/device/fan", {"on": True}); time.sleep(0.6)
    time.sleep(1)
    assert len(nhub.sets("fan")) == 1, nhub.sets("fan")


def test_s06_device_controllable_after_burst(nhub):
    start_p100(nhub)
    press("fan", [i % 2 == 0 for i in range(20)], 0.25)
    time.sleep(5)
    want = not dev_on("fan")
    n = len(nhub.sets("fan"))
    api("/api/device/fan", {"on": want})
    time.sleep(3)
    assert len(nhub.sets("fan")) == n + 1 and dev_on("fan") is want
