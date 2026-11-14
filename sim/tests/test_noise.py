import json, pytest

from sim.common import validate
from sim.noise import LEVELS, inject
from sim.scenarios import SCENARIOS, build


@pytest.mark.parametrize("level", sorted(LEVELS))
@pytest.mark.parametrize("code", ["SC03", "SC08", "N01", "N04"])
def test_noisy_payloads_valid(code, level):
    for e in build(code, noise=level, seed=3):
        validate(e.topic, e.payload)


def test_deterministic_and_input_untouched():
    base = build("SC08")
    snap = json.dumps([(e.t, e.payload) for e in base])
    a, b = inject(base, "heavy", 5), inject(base, "heavy", 5)
    assert [(e.t, e.payload) for e in a] == [(e.t, e.payload) for e in b]
    assert snap == json.dumps([(e.t, e.payload) for e in base])
    assert [(e.t, e.payload) for e in a] != [(e.t, e.payload) for e in inject(base, "heavy", 6)]


def test_each_knob_acts():
    base = build("SC08")
    n_state = lambda ev: sum("persons" in e.payload for e in ev)
    assert n_state(inject(base, "mild", 1, dropout=0.5, duplicate=0, late=0)) < n_state(base)
    assert n_state(inject(base, "mild", 1, dropout=0, duplicate=0.5, late=0)) > n_state(base)
    postures = lambda ev: [q["posture"] for e in ev if "persons" in e.payload for q in e.payload["persons"]]
    assert postures(inject(base, "mild", 1, dropout=0, posture_flip=0.5)) != postures(base)
    ids = lambda ev: {q["track_id"] for e in ev if "persons" in e.payload for q in e.payload["persons"]}
    assert ids(inject(base, "mild", 1, dropout=0, track_id_switch=0.5)) != ids(base)
    env = lambda ev: [e.payload["temp_c"] for e in ev if e.topic.endswith("/env")]
    assert env(inject(base, "mild", 1, temp_jitter=2.0)) != env(base)


def test_late_keeps_t_sorted():
    ev = inject(build("SC07"), "heavy", 2, late=0.5)
    assert [e.t for e in ev] == sorted(e.t for e in ev)


def test_noise_scenarios_registered():
    assert {f"N{i:02d}" for i in range(1, 9)} <= set(SCENARIOS)
