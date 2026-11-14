import json
from pathlib import Path

import pytest

from smarthome_ai import posture, transitions
from smarthome_ai.detector import Detection, FakeDetector
from smarthome_ai.pipeline import run
from smarthome_ai.publisher import Publisher, check
from smarthome_ai.source import FakeSource
from smarthome_ai.tracker import Tracker
from smarthome_ai.zones import Zones

CFG = Path(__file__).resolve().parents[1] / "config"
ZONES = Zones.load(CFG / "zones.example.json")


class FakeMqtt:
    def __init__(self):
        self.sent = []

    def publish(self, topic, payload, qos=0, retain=False):
        self.sent.append((topic, json.loads(payload), retain))


def test_zones():
    assert ZONES.zone_of((0.05, 0.5)) == "door"
    assert ZONES.zone_of((0.95, 0.5)) == "other_exit"
    assert ZONES.zone_of((0.5, 0.6)) == "sofa"
    assert ZONES.zone_of((0.5, 0.1)) == "livingroom"
    with pytest.raises(ValueError):
        Zones({"kitchen": []})


def test_tracker_keeps_id_and_new_id():
    tr = Tracker()
    a = tr.update([Detection((0.1, 0.1, 0.2, 0.5), .9)], 0)[0].id
    b = tr.update([Detection((0.12, 0.1, 0.22, 0.5), .9)], 0.25)[0].id
    assert a == b
    c = tr.update([Detection((0.12, 0.1, 0.22, 0.5), .9), Detection((0.7, 0.1, 0.8, 0.5), .9)], 0.5)
    assert len({x.id for x in c}) == 2 and a in {x.id for x in c}


def test_posture_box_and_moving():
    ar = 1  # khung vuông cho dễ tính
    assert posture.classify((0.4, 0.1, 0.5, 0.9), 0, ar) == "standing"
    assert posture.classify((0.3, 0.5, 0.7, 0.8), 0, ar) == "lying"
    assert posture.classify((0.4, 0.4, 0.7, 0.8), 0, ar) == "sitting"
    assert posture.classify((0.4, 0.1, 0.5, 0.9), 2.0, ar) == "moving"
    assert posture.smooth([], "sitting") == "sitting"


def test_posture_keypoints_lying():
    kp = [(0.5, 0.5, 0.9)] * 17
    kp[5], kp[6] = (0.3, 0.5, .9), (0.3, 0.52, .9)
    kp[11], kp[12] = (0.6, 0.5, .9), (0.6, 0.52, .9)
    assert posture.classify((0.4, 0.1, 0.5, 0.9), 0, 1, kp) == "lying"


def test_direction():
    assert transitions.direction("livingroom", "door") == "out"
    assert transitions.direction("sofa", "other_exit") == "out"
    assert transitions.direction("door", "livingroom") == "in"
    assert transitions.direction("livingroom", "sofa") == "unknown"


def test_schema_rejects_bad_payload():
    with pytest.raises(ValueError):
        check("state", {"ts": "x", "seq": 1, "camera_ok": True, "persons": [{"track_id": 1, "zone": "kitchen", "posture": "lying", "conf": .5}]})


def test_fake_pipeline_end_to_end():
    script = json.loads((CFG / "fake-script.example.json").read_text())
    mq = FakeMqtt()
    pub = Publisher(client=mq)
    run(FakeSource(len(script), 4), FakeDetector(script), ZONES, pub, state_hz=2)
    topics = [t for t, _, _ in mq.sent]
    assert mq.sent[0] == ("home/ai/status", mq.sent[0][1], True) and mq.sent[0][1]["online"] is True
    assert topics.count("home/ai/livingroom/state") >= 4
    trans = [p for t, p, _ in mq.sent if t.endswith("transition")]
    assert [(x["from_zone"], x["to_zone"], x["direction"]) for x in trans] == [("livingroom", "door", "out")]
    # run() đã kiểm schema từng payload qua Publisher.send; sai sẽ ném ValueError


BIG = (0.35, 0.43, 0.65, 0.97)  # tâm (0.5, 0.70) trong sofa, chân (0.5, 0.97) ngoài sofa


def test_zone_for_posture():
    assert ZONES.zone_for(BIG, "lying") == "sofa"
    assert ZONES.zone_for(BIG, "sitting") == "sofa"
    assert ZONES.zone_for(BIG, "standing") == "livingroom"
    assert ZONES.zone_for(BIG, "moving") == "livingroom"


def test_posture_change_no_transition():
    class T:
        zone = pending_zone = None
        pending_n = 0
    t = T()
    z = lambda p: ZONES.zone_for(BIG, p)
    assert transitions.step(t, z("standing")) is None
    out = [transitions.step(t, z("lying")) for _ in range(3)]
    assert t.zone == "sofa" and out[-1] == ("livingroom", "sofa")
    assert transitions.direction("livingroom", "sofa") == "unknown"
