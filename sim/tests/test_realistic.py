import json, subprocess, sys
from pathlib import Path

import pytest

from sim.common import validate
from sim.realistic import M, catalog
from sim.scenarios import SCENARIOS, build

ROOT = Path(__file__).resolve().parents[2]


def test_twenty_registered():
    assert sorted(M) == [f"R{i:02d}" for i in range(1, 21)]
    assert set(M) <= set(SCENARIOS)


@pytest.mark.parametrize("code", sorted(M))
def test_realistic_valid_and_ordered(code):
    ev = build(code)
    for e in ev:
        validate(e.topic, e.payload)
    ts = [e.t for e in ev]
    assert ts == sorted(ts) and ts[-1] >= 120
    m = M[code][1]
    assert m.title.strip() and m.description.strip() and m.expect and all(x.strip() for x in m.expect)
    seqs = [e.payload["seq"] for e in ev if "seq" in e.payload]
    assert seqs == sorted(set(seqs))


def test_list_json_format():
    out = subprocess.run([sys.executable, "-m", "sim", "list", "--json"], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    data = json.loads(out)
    assert data == json.loads(json.dumps(catalog(), ensure_ascii=False)) and len(data) == 20
    for d in data:
        assert set(d) == {"code", "title", "description", "durationSec", "expect", "requires"}
        assert d["durationSec"] > 0 and d["expect"] and set(d["requires"]) <= {"p100", "esp"}
