"""Dùng chung cho test_noise/test_burst: Hub thật cổng 5098 + bus spy + p100 giả. Tự bỏ qua nếu thiếu broker/dotnet."""
import contextlib, json, os, signal, socket, statistics, subprocess, sys, time, urllib.request
from pathlib import Path

import paho.mqtt.client as mqtt
import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
PORT = 5098
MQTT_PORT = int(os.environ.get("E2E_MQTT_PORT", 1883))  # broker riêng khi broker chung đang bận
BASE = f"http://127.0.0.1:{PORT}"
RETAINED = ["home/ai/status", "home/sensor/esp1/door", "home/sensor/esp1/env", "home/device/light/state", "home/device/fan/state"]
# Thời gian thật của Hub (s); kịch bản nén speed 8-20. ManualBlock phải dài hơn kịch bản.
HUB_ENV = {"Automation__StartupGrace": "00:00:01", "Automation__SofaLyingMin": "00:00:01", "Automation__MinInterval": "00:00:03",
           "Automation__ManualBlock": "00:00:15",
           "Automation__CommandMatchWindow": "00:00:01", "Alerts__FirstAfter": "00:00:05", "Alerts__UnknownFreeze": "00:00:10",
           "Occupancy__VerifyAway": "00:00:05", "Occupancy__StateStale": "00:00:03"}
MANUAL_BLOCK = 15


def api(path, body=None):
    req = urllib.request.Request(BASE + path, method="POST" if body is not None else "GET",
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req, timeout=5))


class Bus:
    def __init__(self):
        self.msgs = []  # (monotonic, topic, payload str)
        self.c = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="noise-spy")
        self.c.on_message = lambda c, u, m: self.msgs.append((time.monotonic(), m.topic, m.payload.decode()))
        self.c.connect("localhost", MQTT_PORT)
        self.c.subscribe("home/#", 1)
        self.c.loop_start()

    def clear_retained(self):
        for t in RETAINED:
            self.c.publish(t, b"", qos=1, retain=True)
        time.sleep(0.5)
        self.msgs.clear()

    def timed(self, topic):
        out = []
        for t, tp, p in self.msgs:
            if tp == topic and p:
                try:
                    d = json.loads(p)
                except ValueError:  # gói sai do fuzz
                    continue
                if isinstance(d, dict) and isinstance(d.get("on", True), bool):
                    out.append((t, d))
        return out

    def sets(self, dev, **match):
        return [d for _, d in self.timed(f"home/device/{dev}/set") if all(d.get(k) == v for k, v in match.items())]

    def set_times(self, dev, **match):
        return [t for t, d in self.timed(f"home/device/{dev}/set") if all(d.get(k) == v for k, v in match.items())]

    def alerts(self):
        return [(t, json.loads(p)) for _, t, p in self.msgs if t.startswith("home/alert/") and not t.endswith("/cancel") and p]

    def cancels(self):
        return [(t, json.loads(p)) for _, t, p in self.msgs if t.startswith("home/alert/") and t.endswith("/cancel") and p]


def gap_stats(times):
    g = [b - a for a, b in zip(times, times[1:])]
    return (round(min(g), 2), round(statistics.median(g), 2)) if g else (None, None)


def events(n=200):
    return api(f"/api/events?limit={n}")  # mới nhất trước


def has_feature(name):
    return subprocess.run(["grep", "-rq", name, "src"], cwd=ROOT).returncode == 0


@pytest.fixture(scope="session")
def dotnet_ok():
    try:
        socket.create_connection(("localhost", MQTT_PORT), 1).close()
    except OSError:
        pytest.skip("không có broker MQTT localhost:1883")
    if subprocess.run(["which", "dotnet"], capture_output=True).returncode:
        pytest.skip("không có dotnet")


@pytest.fixture
def nhub(dotnet_ok, tmp_path):
    with hub_ctx(tmp_path) as bus:
        yield bus


@contextlib.contextmanager
def hub_ctx(tmp_path):
    bus = Bus()
    bus.clear_retained()
    env = {**os.environ, **HUB_ENV, "ConnectionStrings__Postgres": "", "ASPNETCORE_ENVIRONMENT": "Production",
           "Automation__ManualPath": str(tmp_path / "manual.json"), "Mqtt__Port": str(MQTT_PORT)}
    p = subprocess.Popen(["dotnet", "run", "--project", "src/SmartHomeAI.Hub", "--no-build", "--urls", BASE],
                         cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
    procs = []
    bus.procs = procs
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
        for _ in range(40):
            bus.c.publish("home/sensor/probe/status", json.dumps({"ts": "2026-11-14T12:00:00Z", "online": True}), qos=1)
            time.sleep(0.25)
            if "sensor/probe" in api("/api/state")["online"]:
                break
        else:
            pytest.skip("Hub không nhận được MQTT")
        bus.msgs.clear()
        yield bus
    finally:
        for q in procs:
            q.kill(); q.wait()
        bus.clear_retained()
        try:
            os.killpg(p.pid, signal.SIGTERM)
            p.wait(10)
        except ProcessLookupError:
            pass
        bus.c.loop_stop()


def start_p100(bus, mute=False):
    """P100 giả; chờ Hub thấy cả hai thiết bị."""
    q = subprocess.Popen([sys.executable, "-m", "sim", "--port", str(MQTT_PORT), "p100"] + (["--mute"] if mute else []), cwd=ROOT,
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    bus.procs.append(q)
    for _ in range(40):
        if {"light", "fan"} <= set(api("/api/state")["devices"]):
            break
        time.sleep(0.25)
    else:
        pytest.skip("p100 giả không báo state")
    bus.msgs.clear()
    return q


def play_scenario(code, speed, noise=None, seed=0):
    from sim.player import play
    play(code, speed=speed, port=MQTT_PORT, noise=noise, seed=seed)
