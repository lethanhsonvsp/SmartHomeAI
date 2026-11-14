"""E2E SC01-SC11: Mosquitto thật + Hub thật + sim phát kịch bản. Tự bỏ qua nếu thiếu broker/dotnet."""
import json, os, signal, socket, subprocess, sys, time, urllib.request
from pathlib import Path

import paho.mqtt.client as mqtt
import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from sim.player import play as _play  # noqa: E402

MQTT_PORT = int(os.environ.get("E2E_MQTT_PORT", 1883))
play = lambda code, speed: _play(code, speed=speed, port=MQTT_PORT)

# Hub dùng đồng hồ thật: xác minh vắng 5 s, cảnh báo 5 s, state cũ sau 3 s (Occupancy__*, Alerts__*); speed 8 nén kịch bản.
PORT, SPEED = int(os.environ.get("E2E_HUB_PORT", 5097)), 8  # 5099 cũ hay bị Hub mồ côi giữ
BASE = f"http://127.0.0.1:{PORT}"
RETAINED = ["home/ai/status", "home/sensor/esp1/door", "home/sensor/esp1/env", "home/device/light/state", "home/device/fan/state"]


def _broker_up():
    try:
        socket.create_connection(("localhost", MQTT_PORT), 1).close()
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
        self.c.connect("localhost", MQTT_PORT)
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
def hub(dotnet_ok, tmp_path):
    bus = Bus()
    bus.clear_retained()
    env = {**os.environ, "ConnectionStrings__Postgres": "", "Alerts__FirstAfter": "00:00:05",
           "Occupancy__VerifyAway": "00:00:05", "Occupancy__StateStale": "00:00:03",
           "ASPNETCORE_ENVIRONMENT": "Production", "Mqtt__Port": str(MQTT_PORT),
           # thời gian thật nén cùng speed 8; lệnh tay lưu vào tệp tạm riêng từng test
           "Automation__StartupGrace": "00:00:00", "Automation__SofaLyingMin": "00:00:01", "Automation__ManualPath": str(tmp_path / "manual.json")}
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
        for _ in range(40):  # đã subscribe home/# khi tin thăm dò hiện trong state
            bus.c.publish("home/sensor/probe/status", json.dumps({"ts": "2026-11-14T12:00:00Z", "online": True}), qos=1)
            time.sleep(0.25)
            if "sensor/probe" in api("/api/state")["online"]:
                break
        else:
            pytest.skip("Hub không nhận được MQTT")
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
    run("SC05", 0)
    assert api("/api/state")["occupancy"] == "OtherArea"


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
    run("SC09", 1)  # kịch bản: manual tắt quạt lúc t=20, state sofa nóng còn đến t=60
    manual = [t for t, tp, p, r in hub.msgs if tp == "home/device/fan/set" and '"manual"' in p]
    assert manual, "kịch bản không phát lệnh manual"
    later = [p for t, tp, p, r in hub.msgs if t > manual[0] and tp == "home/device/fan/set" and '"auto"' in p and '"on":true' in p.replace(" ", "")]
    assert not later, "auto bật lại quạt sau lệnh manual"


def test_sc10_camera_lost_unknown_no_alert(hub):
    run("SC10", 3)
    assert api("/api/state")["occupancy"] == "Unknown"
    assert not alerts(hub)


def test_sc11_return_cancels_alert(hub):
    hub.c.publish("home/device/light/state", json.dumps({"ts": "2026-11-14T12:00:00Z", "on": True}), qos=1, retain=True)
    run("SC11", 2)
    assert alerts(hub), "chưa có cảnh báo để hủy"
    assert cancels(hub) and cancels(hub)[0][1]["reason"] == "person_returned"
