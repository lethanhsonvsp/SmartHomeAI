import pytest

from sim.common import validate
from sim.scenarios import SCENARIOS, build


@pytest.mark.parametrize("code", sorted(SCENARIOS))
def test_scenario_valid_and_ordered(code):
    ev = build(code)
    assert ev
    for e in ev:
        validate(e.topic, e.payload)
    ts = [e.t for e in ev]
    assert ts == sorted(ts)
    assert ts[-1] > ts[0]
    seqs = [e.payload["seq"] for e in ev if e.topic.endswith("/state") and "seq" in e.payload]
    assert seqs == sorted(set(seqs))


def test_bad_payload_rejected():
    with pytest.raises(ValueError):
        validate("home/sensor/esp1/env", {"ts": "2026-11-14T12:00:00Z", "temp_c": 1, "humidity": 120, "lux": 1})


def test_ts_real_vs_sim():
    from datetime import datetime, timezone
    base = datetime(2026, 11, 14, tzinfo=timezone.utc)
    span = lambda ev: (datetime.fromisoformat(ev[-1].payload["ts"]) - datetime.fromisoformat(ev[0].payload["ts"])).total_seconds()
    sim, real = build("SC07", base), build("SC07", base, scale=1 / 20)
    assert span(sim) == sim[-1].t
    assert abs(span(real) - sim[-1].t / 20) < 1


def test_p100_publishes_state_on_set():
    """Cần broker localhost:1883; bỏ qua nếu không có."""
    import json, socket, subprocess, sys, time
    import paho.mqtt.client as mqtt
    try:
        socket.create_connection(("localhost", 1883), 1).close()
    except OSError:
        pytest.skip("không có broker")
    got = []
    c = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="test-p100")
    c.on_message = lambda _c, _u, m: got.append((m.topic, json.loads(m.payload)))
    c.connect("localhost", 1883); c.loop_start()
    c.subscribe("home/device/fan/state")
    p = subprocess.Popen([sys.executable, "-m", "sim", "p100"], cwd=__import__("pathlib").Path(__file__).resolve().parents[2])
    try:
        time.sleep(2)
        c.publish("home/device/fan/set", json.dumps({"ts": "2026-11-14T12:00:00Z", "on": True, "source": "auto"}), qos=1)
        c.publish("home/device/fan/set", "không phải json", qos=1)  # không được làm sập
        time.sleep(2)
        assert p.poll() is None
        assert any(t == "home/device/fan/state" and v["on"] is True for t, v in got)
    finally:
        p.kill(); p.wait(); c.loop_stop()


def test_play_waits_for_connack(monkeypatch):
    import time as _t
    from sim import player

    class FakeClient:
        ready_at = _t.monotonic() + 0.3
        sent_early = False

        def is_connected(self):
            return _t.monotonic() >= self.ready_at

        def disconnect(self):
            pass

    c = FakeClient()
    monkeypatch.setattr(player, "connect", lambda *a, **k: c)

    def fake_publish(cl, topic, payload, retain=False):
        if not cl.is_connected():
            cl.sent_early = True

    monkeypatch.setattr(player, "publish", fake_publish)
    monkeypatch.setattr(player, "build", lambda *a, **k: build("SC10")[:2])
    player.play("SC10", speed=1000)
    assert not c.sent_early
