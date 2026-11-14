import urllib.request

import numpy as np

from smarthome_ai import posture
from smarthome_ai.pose import PoseDetector
from smarthome_ai.source import CvSource
from smarthome_ai.stream import StreamServer, draw_overlay


def kp17(**pts):
    kp = [(0.5, 0.5, 0.0)] * 17
    for k, v in pts.items():
        kp[int(k[1:])] = v
    return kp


def test_source_numeric(monkeypatch):
    import cv2
    seen = []

    class Cap:
        def __init__(self, s): seen.append(s)
        def set(self, *a): return True
        def release(self): pass

    monkeypatch.setattr(cv2, "VideoCapture", Cap)
    s = CvSource("0", 640, 480)
    assert seen == [0] and s.live
    assert not CvSource("video.mp4").live


def test_posture_keypoints():
    k = lambda x, y: (x, y, 0.9)
    stand = kp17(k0=k(.5, .1), k5=k(.45, .3), k6=k(.55, .3), k11=k(.46, .55), k12=k(.54, .55), k13=k(.46, .75), k14=k(.54, .75))
    sit = kp17(k0=k(.5, .1), k5=k(.45, .3), k6=k(.55, .3), k11=k(.46, .6), k12=k(.54, .6), k13=k(.3, .62), k14=k(.35, .62))
    lie = kp17(k0=k(.1, .5), k5=k(.25, .48), k6=k(.25, .52), k11=k(.5, .48), k12=k(.5, .52), k13=k(.7, .5), k14=k(.7, .52))
    box = (0.3, 0.1, 0.6, 0.9)
    assert [posture.classify(box, 0, 1, x) for x in (stand, sit, lie)] == ["standing", "sitting", "lying"]


def test_overlay_draws_and_keeps_input():
    f = np.zeros((120, 160, 3), np.uint8)
    item = {"box": (0.2, 0.2, 0.6, 0.9), "kp": kp17(k5=(.3, .3, .9), k6=(.5, .3, .9)), "id": 1, "zone": "sofa", "posture": "sitting", "conf": .8}
    out = draw_overlay(f, [item], {"sofa": [[0, 0], [1, 0], [1, 1], [0, 1]]})
    assert out.shape == f.shape and out.any() and not f.any()


def test_pose_decode():
    d = PoseDetector.__new__(PoseDetector)
    import cv2
    d.cv2, d.conf, d.iou = cv2, 0.4, 0.5
    row = np.zeros(56, np.float32)
    row[:5] = [320, 320, 100, 200, 0.9]
    row[5:] = np.tile([320, 300, 0.8], 17)
    res = d._decode(np.stack([row, row * 0 + 0]), 640, 640, 1.0, 0, 0)
    assert len(res) == 1 and res[0].box == (270 / 640, 220 / 640, 370 / 640, 420 / 640) and len(res[0].keypoints) == 17


def test_server_snapshot_and_mjpeg():
    srv = StreamServer(0, host="127.0.0.1", fps=20)
    try:
        base = f"http://127.0.0.1:{srv.port}"
        r = urllib.request.urlopen(base + "/snapshot.jpg")
        assert r.headers["Access-Control-Allow-Origin"] == "*" and r.read(2) == b"\xff\xd8"  # placeholder khi chưa có khung
        srv.update(np.full((90, 160, 3), 90, np.uint8), [])
        r = urllib.request.urlopen(base + "/stream.mjpg")
        assert r.headers["Content-Type"].startswith("multipart/x-mixed-replace")
        assert r.headers["Access-Control-Allow-Origin"] == "*"
        assert r.read(200).startswith(b"--frame\r\nContent-Type: image/jpeg")
        r.close()
    finally:
        srv.close()
