"""Bất biến an toàn rơ-le trên N chuỗi ngẫu nhiên nhiễu nặng (sim/fuzz.py). Mỗi chuỗi một Hub mới.

N = FUZZ_N (mặc định 12), seed 1..N, tốc độ FUZZ_SPEED (mặc định 12). Chạy lại một chuỗi: python -m sim fuzz --seed N.
Thời gian Hub (hubkit.HUB_ENV): ManualBlock 15 s thay cho 30 phút; cửa sổ 1 giờ thật bao trọn cả chuỗi (< 1 giờ).
"""
import os, statistics, time

import pytest

from hubkit import MANUAL_BLOCK, MQTT_PORT, api, gap_stats, has_feature, hub_ctx, start_p100, dotnet_ok  # noqa: F401
from sim import fuzz

N, SPEED = int(os.environ.get("FUZZ_N", 12)), float(os.environ.get("FUZZ_SPEED", 12))
MIN_GAP = 3.0
NEW = not has_feature("MinCommandInterval")
needs_burst = pytest.mark.xfail(NEW, reason="chờ Core/Hub chống dội ngắn hạn (MinCommandInterval/MinHoldOn)", strict=False)


@pytest.fixture(scope="module")
def runs(dotnet_ok, tmp_path_factory):
    out = []
    for seed in range(1, N + 1):
        ev = fuzz.stamp(fuzz.gen(seed), SPEED, seed=seed)
        with hub_ctx(tmp_path_factory.mktemp(f"fz{seed}")) as bus:
            start_p100(bus)
            t0 = fuzz.play(ev, SPEED, port=MQTT_PORT, api_base="http://127.0.0.1:5098")
            time.sleep(2)
            try:
                health = api("/health")["status"] == "ok"
            except Exception:
                health = False
            sets = {d: [(t, j["on"], j["source"]) for t, j in bus.timed(f"home/device/{d}/set")] for d in ("light", "fan")}
            states = {d: [(t, j["on"]) for t, j in bus.timed(f"home/device/{d}/state")] for d in ("light", "fan")}
            out.append(dict(seed=seed, ev=ev, t0=t0, health=health, sets=sets, states=states))
    return out


def where(r, t_bus, dev):
    near = [f"  bus {t - r['t0']:.1f}s(mô phỏng {(t - r['t0']) * SPEED:.0f}) {dev} set on={on} {src}" for t, on, src in r["sets"][dev] if abs(t - t_bus) < 12]
    return f"seed={r['seed']} (python -m sim fuzz --seed {r['seed']})\n" + "\n".join(near) + "\n" + fuzz.describe(r["ev"], (t_bus - r["t0"]) * SPEED)


def check(runs, rule):
    bad = [m for r in runs for d in ("light", "fan") if (m := rule(r, d))]
    assert not bad, f"{len(bad)} vi phạm; đầu tiên:\n{bad[0]}"


def test_inv5_hub_alive_after_every_sequence(runs):
    assert [r["seed"] for r in runs if not r["health"]] == []


def test_inv1_min_gap_between_any_two_sets(runs):
    def rule(r, d):
        s = r["sets"][d]
        for a, b in zip(s, s[1:]):
            if b[0] - a[0] < MIN_GAP - 0.05:
                return f"bất biến 1: {d} hai lệnh cách {b[0] - a[0]:.2f}s < {MIN_GAP}s\n" + where(r, b[0], d)
    check(runs, rule)


@needs_burst
def test_inv2_auto_changes_per_window(runs):
    def rule(r, d):
        ch, last = [], False
        for t, on, src in r["sets"][d]:
            if src == "auto" and on != last: ch.append(t)
            last = on
        for i, t in enumerate(ch):
            if i and t - ch[i - 1] < 10: return f"bất biến 2: {d} hai đổi auto cách {t - ch[i - 1]:.1f}s < 10s\n" + where(r, t, d)
        if len(ch) > 6: return f"bất biến 2: {d} {len(ch)} đổi auto > 6/giờ\n" + where(r, ch[6], d)
    check(runs, rule)


def test_inv3_no_auto_during_manual_block(runs):
    def rule(r, d):
        man = [t for t, _, src in r["sets"][d] if src == "manual"]
        for t, _, src in r["sets"][d]:
            if src == "auto" and any(0 <= t - m < MANUAL_BLOCK for m in man):
                return f"bất biến 3: {d} lệnh auto trong {MANUAL_BLOCK}s sau lệnh tay\n" + where(r, t, d)
    check(runs, rule)


@needs_burst
def test_inv4_no_repeated_same_state_commands(runs):
    def rule(r, d):
        run, prev = 0, None
        st = r["states"][d]
        for t, on, _ in r["sets"][d]:
            opposite = prev is not None and any(not on == prev and pt > prev_t and pt < t and pon != prev for pt, pon in st)
            run = run + 1 if (on == prev and not opposite) else 1
            prev, prev_t = on, t
            if run > 3: return f"bất biến 4: {d} {run} lệnh on={on} liên tiếp không có state ngược\n" + where(r, t, d)
    check(runs, rule)


def test_stats_report(runs):
    allgaps = [b[0] - a[0] for r in runs for d in r["sets"] for a, b in zip(r["sets"][d], r["sets"][d][1:])]
    n_ev, n_set = sum(len(r["ev"]) for r in runs), sum(len(v) for r in runs for v in r["sets"].values())
    print(f"\nFUZZ: {len(runs)} chuỗi, {n_ev} sự kiện, {n_set} lệnh set, khoảng cách min={min(allgaps, default=None)} median={statistics.median(allgaps) if allgaps else None}")
    for r in runs:
        print(f"  seed {r['seed']}: {len(r['ev'])} sk, set fan/light={len(r['sets']['fan'])}/{len(r['sets']['light'])}, min/median gap fan={gap_stats([t for t, *_ in r['sets']['fan']])} light={gap_stats([t for t, *_ in r['sets']['light']])}")
