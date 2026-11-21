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


# ---- API cấu hình / vùng ----
import json
import urllib.error

from smarthome_ai.zones import Zones

ITEM = {"box": (0.2, 0.2, 0.6, 0.9), "kp": kp17(k5=(.3, .3, .9), k6=(.5, .3, .9)), "id": 1, "zone": "sofa", "posture": "sitting", "conf": .8}
ZONES = {"livingroom": [[0, 0], [1, 0], [1, 1], [0, 1]], "sofa": [[.1, .1], [.5, .1], [.5, .5], [.1, .5]]}


def call(base, path, method="GET", body=None, raw=None):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(base + path, data=data, method=method, headers={"Content-Type": "application/json"})
    try:
        r = urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        r = e
    return r.status, r.headers, r.read()


def test_overlay_respects_cfg():
    f = np.full((120, 160, 3), 50, np.uint8)
    off = {"skeleton": False, "boxes": False, "zones": False, "labels": False}
    assert not (draw_overlay(f, [ITEM], ZONES, off) != f).any()
    for k in off:
        assert (draw_overlay(f, [ITEM], ZONES, {**off, k: True}) != f).any(), k


def test_config_frame_zones_api(tmp_path):
    zp = tmp_path / "zones.json"
    zn = Zones({"livingroom": ZONES["livingroom"]})
    srv = StreamServer(0, zn, host="127.0.0.1", fps=50, zones_path=zp)
    try:
        b = f"http://127.0.0.1:{srv.port}"
        # config
        assert json.loads(call(b, "/config")[2]) == {"skeleton": True, "boxes": True, "zones": True, "labels": True, "sofa_lying": False}
        s, h, body = call(b, "/config", "POST", {"skeleton": False})
        assert s == 200 and json.loads(body)["skeleton"] is False and h["Access-Control-Allow-Origin"] == "*"
        for bad in ({"foo": True}, {"boxes": "yes"}, {"boxes": 1}, [1]):
            assert call(b, "/config", "POST", bad)[0] == 400
        assert call(b, "/config", "POST", raw=b"{x")[0] == 400
        assert json.loads(call(b, "/config")[2])["skeleton"] is False
        # preflight
        s, h, _ = call(b, "/zones", "OPTIONS")
        assert s == 204 and "PUT" in h["Access-Control-Allow-Methods"] and h["Access-Control-Allow-Origin"] == "*"
        # frame.jpg: không lớp phủ; snapshot có
        raw = np.full((90, 160, 3), 50, np.uint8)
        call(b, "/config", "POST", {"skeleton": True})
        srv.update(raw, [ITEM])
        assert call(b, "/zones", "PUT", {"zones": {**ZONES}})[0] == 200  # vùng sofa tô lên snapshot
        import cv2, time
        time.sleep(0.2)
        dec = lambda x: cv2.imdecode(np.frombuffer(x, np.uint8), cv2.IMREAD_COLOR)
        s, h, fj = call(b, "/frame.jpg")
        assert h["Cache-Control"] == "no-store"
        fr, sn = dec(fj), dec(call(b, "/snapshot.jpg")[2])
        assert np.abs(fr.astype(int) - 50).max() <= 3  # JPEG của nền phẳng
        assert np.abs(sn.astype(int) - fr.astype(int)).max() > 30
        # chưa có khung -> ảnh chờ
        srv.latest = (None, [], 0.0)
        assert call(b, "/frame.jpg")[2][:2] == b"\xff\xd8"
        # zones: GET, PUT hợp lệ đã nạp và ghi tệp
        assert json.loads(call(b, "/zones")[2])["zones"].keys() == ZONES.keys()
        good = {"zones": {"livingroom": ZONES["livingroom"], "door": [[0, 0], [.2, 0], [.2, 1]]}}
        assert call(b, "/zones", "PUT", good)[0] == 200
        assert list(zn.polys) == ["door", "livingroom"] and zn.zone_of((.1, .5)) == "door"
        assert json.loads(zp.read_text()) == {"zones": zn.polys} and not (tmp_path / "zones.json.tmp").exists()
        # PUT sai: 400, không đổi
        before, disk = dict(zn.polys), zp.read_text()
        tri = [[0, 0], [1, 0], [1, 1]]
        for bad in ({"zones": {"door": tri}}, {"zones": {"livingroom": tri[:2]}}, {"zones": {"livingroom": [[0, 0], [1, 0], [2, 1]]}},
                    {"zones": {"livingroom": tri, "kitchen": tri}}, {"zones": {"livingroom": [[0, 0], [1, 0], [True, 1]]}},
                    {"zones": {"livingroom": [[0, 0]] * 21}}, {"zones": {"livingroom": [[0, 0], [1, 0], ["a", 1]]}}, [1]):
            s, _, body = call(b, "/zones", "PUT", bad)
            assert s == 400 and "error" in json.loads(body), bad
        assert call(b, "/zones", "PUT", raw=b"x" * 70000)[0] == 413
        assert zn.polys == before and zp.read_text() == disk
        # mặc định
        assert "sofa" in json.loads(call(b, "/zones/default")[2])["zones"]
    finally:
        srv.close()


def test_posture_samples(tmp_path):
    k = lambda x, y: (x, y, 0.9)
    sit = kp17(k0=k(.5, .4), k5=k(.45, .45), k6=k(.55, .45), k11=k(.46, .55), k12=k(.54, .55), k15=k(.5, .6), k16=k(.52, .6))
    lie = kp17(k0=k(.2, .3), k5=k(.3, .35), k6=k(.3, .4), k11=k(.5, .45), k12=k(.5, .5), k15=k(.8, .7), k16=k(.8, .72))
    box = (0.3, 0.1, 0.6, 0.9)
    s = posture.Samples(tmp_path / "posture-samples.json")
    assert not s.trained and posture.classify(box, 0, 1, lie, s) == posture.classify(box, 0, 1, lie)  # chưa đủ mẫu: dùng quy tắc
    for _ in range(posture.MIN_SAMPLES):
        s.add("sitting", posture.features(sit, 1))
        s.add("lying", posture.features(lie, 1))
    s.save()
    s = posture.Samples(s.path)  # đọc lại từ tệp
    assert s.trained and s.counts() == {"standing": 0, "sitting": 5, "lying": 5}
    moved = [(x + .1, y + .05, c) for x, y, c in lie]  # cùng tư thế, khác vị trí
    assert [posture.classify(box, 0, 1, x, s) for x in (sit, lie, moved)] == ["sitting", "lying", "lying"]
    assert posture.classify(box, 2.0, 1, sit, s) == "moving" and posture.features(None) is None


def test_posture_capture_api(tmp_path):
    srv = StreamServer(0, host="127.0.0.1", zones_path=tmp_path / "zones.json")
    try:
        import urllib.request
        def post(label):
            rq = urllib.request.Request(f"http://127.0.0.1:{srv.port}/posture", json.dumps({"label": label}).encode(), {"Content-Type": "application/json"})
            return json.loads(urllib.request.urlopen(rq).read())
        assert post("lying")["capture"]["label"] == "lying" and srv.capture["left"] > 0
        # ảnh xem lại + xóa từng mẫu
        srv.samples.add("lying", [0.1] * 34, srv.thumb(np.full((90, 160, 3), 90, np.uint8), (0.2, 0.2, 0.6, 0.9), kp17()))
        sid = srv.samples.ids()["lying"][0]
        b = f"http://127.0.0.1:{srv.port}"
        assert urllib.request.urlopen(f"{b}/posture/img/{sid}.jpg").read(2) == b"\xff\xd8"
        for bad in ("../zones", "123456"):
            try:
                urllib.request.urlopen(f"{b}/posture/img/{bad}.jpg")
                assert False, "phải trả 404"
            except urllib.error.HTTPError as e:
                assert e.code == 404
        rq = urllib.request.Request(b + "/posture", json.dumps({"delete": sid}).encode(), {"Content-Type": "application/json"})
        assert json.loads(urllib.request.urlopen(rq).read())["samples"]["lying"] == [] and not list((tmp_path / "posture-samples").glob("*.jpg"))
        st = post("clear")
        assert st["counts"] == {"standing": 0, "sitting": 0, "lying": 0} and st["capture"] is None and not st["trained"]
        try:
            post("flying")
            assert False, "phải trả 400"
        except urllib.error.HTTPError as e:
            assert e.code == 400
    finally:
        srv.close()
