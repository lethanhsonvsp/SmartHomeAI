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
