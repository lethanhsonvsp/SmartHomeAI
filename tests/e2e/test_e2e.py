"""E2E SC01-SC11: Mosquitto thật + Hub thật + sim phát kịch bản. Tự bỏ qua nếu thiếu broker/dotnet."""
import json, os, signal, socket, subprocess, sys, time, urllib.request
from pathlib import Path

import paho.mqtt.client as mqtt
import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from sim.player import play  # noqa: E402

PORT, SPEED = 5099, 2  # speed 2: Hub dùng đồng hồ thật (xác minh 30 s, cảnh báo 5 s) nên không nhanh hơn
BASE = f"http://127.0.0.1:{PORT}"
RETAINED = ["home/ai/status", "home/sensor/esp1/door", "home/sensor/esp1/env", "home/device/light/state", "home/device/fan/state"]


def _broker_up():
    try:
        socket.create_connection(("localhost", 1883), 1).close()
        return True
    except OSError:
        return False


def api(path, body=None):
    req = urllib.request.Request(BASE + path, method="POST" if body is not None else "GET",
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req, timeout=5))


class Bus:
    """Ghi mọi tin home/# kèm mốc thời gian."""

    def __init__(self):
        self.msgs = []
        self.c = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="e2e-spy")
        self.c.on_message = lambda c, u, m: self.msgs.append((time.monotonic(), m.topic, m.payload.decode(), m.retain))
        self.c.connect("localhost", 1883)
        self.c.subscribe("home/#", 1)
        self.c.loop_start()

    def clear_retained(self):
        for t in RETAINED:
            self.c.publish(t, b"", qos=1, retain=True)
        time.sleep(0.5)
        self.msgs.clear()

    def sets(self, dev, **match):
        out = []
        for _, t, p, retain in self.msgs:
            if t == f"home/device/{dev}/set" and not retain:
                d = json.loads(p)
                if all(d.get(k) == v for k, v in match.items()):
                    out.append(d)
        return out

    def topics(self, prefix):
        return [(t, json.loads(p)) for _, t, p, r in self.msgs if t.startswith(prefix) and p]


@pytest.fixture(scope="session")
def dotnet_ok():
    if not _broker_up():
        pytest.skip("không có broker MQTT localhost:1883 (docker compose up -d mosquitto)")
    if subprocess.run(["which", "dotnet"], capture_output=True).returncode:
        pytest.skip("không có dotnet")


@pytest.fixture
def hub(dotnet_ok):
    bus = Bus()
    bus.clear_retained()
    env = {**os.environ, "ConnectionStrings__Postgres": "", "Alerts__FirstAfter": "00:00:05",
           "ASPNETCORE_ENVIRONMENT": "Production"}
    p = subprocess.Popen(["dotnet", "run", "--project", "src/SmartHomeAI.Hub", "--no-build", "--urls", BASE],
                         cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
    try:
        for _ in range(60):
            try:
                api("/health"); break
            except OSError:
                if p.poll() is not None:
                    pytest.skip("Hub không chạy được (chưa build?)")
                time.sleep(0.5)
        else:
            pytest.skip("Hub không lên")
        time.sleep(1)
        bus.msgs.clear()
        yield bus
    finally:
        os.killpg(p.pid, signal.SIGTERM)
        p.wait(10)
        bus.c.loop_stop()


def run(code, wait=0):
    play(code, speed=SPEED)
    time.sleep(wait)


def alerts(bus):
    return [(t, d) for t, d in bus.topics("home/alert/") if not t.endswith("/cancel")]


def cancels(bus):
    return [(t, d) for t, d in bus.topics("home/alert/") if t.endswith("/cancel")]


def test_sc01_dark_light_on(hub):
    run("SC01", 2)
    assert hub.sets("light", on=True, source="auto")


def test_sc02_bright_no_light(hub):
    run("SC02", 2)
    assert not hub.sets("light")
    assert api("/api/state")["occupancy"] == "Occupied"


def test_sc03_sofa_hot_fan_on(hub):
    run("SC03", 2)
    assert hub.sets("fan", on=True, source="auto")


def test_sc04_sofa_cool_no_fan(hub):
    run("SC04", 2)
    assert not hub.sets("fan")


def test_sc05_other_area_not_away(hub):
    run("SC05", 3)
    assert api("/api/state")["occupancy"] != "AwayConfirmed"


def test_sc06_return_cancels_verification(hub):
    run("SC06", 2)
    assert api("/api/state")["occupancy"] == "Occupied"
    assert not alerts(hub)


def test_sc07_alert_after_leaving(hub):
    hub.c.publish("home/device/light/state", json.dumps({"ts": "2026-11-14T12:00:00Z", "on": True}), qos=1, retain=True)
    run("SC07", 1)
    assert alerts(hub), "không có home/alert/*"
    assert alerts(hub)[0][1]["level"] == "first"
    assert [d for _, d in hub.topics("home/sensor/esp1/buzzer/set") if d["on"]]


def test_sc08_two_people_not_away(hub):
    run("SC08", 2)
    assert api("/api/state")["occupancy"] != "AwayConfirmed"
    assert not alerts(hub)


def test_sc09_manual_blocks_auto(hub):
    run("SC09", 1)  # kịch bản: manual tắt quạt lúc t=20
    assert hub.sets("fan", source="manual")
    n = len(hub.sets("fan", on=True, source="auto"))
    api("/api/device/fan", {"on": False})
    time.sleep(3)
    assert len(hub.sets("fan", on=True, source="auto")) == n, "auto bật lại quạt sau lệnh manual"
    assert len(hub.sets("fan", on=True, source="manual")) == 0


def test_sc10_camera_lost_unknown_no_alert(hub):
    run("SC10", 3)
    assert api("/api/state")["occupancy"] == "Unknown"
    assert not alerts(hub)


def test_sc11_return_cancels_alert(hub):
    hub.c.publish("home/device/light/state", json.dumps({"ts": "2026-11-14T12:00:00Z", "on": True}), qos=1, retain=True)
    run("SC11", 2)
    assert alerts(hub), "chưa có cảnh báo để hủy"
    assert cancels(hub) and cancels(hub)[0][1]["reason"] == "person_returned"
