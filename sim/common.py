"""Kết nối MQTT, ký thời gian và kiểm schema dùng chung."""
import json, os, re
from datetime import datetime, timezone
from pathlib import Path

import jsonschema
import paho.mqtt.client as mqtt

SCHEMAS = Path(__file__).resolve().parent.parent / "docs" / "schemas"
# (mẫu topic -> tệp schema), theo docs/mqtt-topics.md
ROUTES = [
    (r"home/sensor/[^/]+/env", "sensor-env"),
    (r"home/sensor/[^/]+/door", "sensor-door"),
    (r"home/sensor/[^/]+/status|home/device/status|home/ai/status", "status"),
    (r"home/sensor/[^/]+/buzzer/set", "buzzer-set"),
    (r"home/device/[^/]+/set", "device-set"),
    (r"home/device/[^/]+/state", "device-state"),
    (r"home/ai/livingroom/state", "ai-state"),
    (r"home/ai/livingroom/transition", "ai-transition"),
]


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def now_iso() -> str:
    return iso(datetime.now(timezone.utc))


def validate(topic: str, payload: dict) -> None:
    """Ném ValueError nếu topic lạ hoặc payload sai schema."""
    for pat, name in ROUTES:
        if re.fullmatch(pat, topic):
            schema = json.loads((SCHEMAS / f"{name}.json").read_text())
            try:
                jsonschema.validate(payload, schema)
            except jsonschema.ValidationError as e:
                raise ValueError(f"{topic}: {e.message}") from e
            return
    raise ValueError(f"topic không có schema: {topic}")


def connect(client_id: str, lwt_topic: str | None = None, host="localhost", port=1883, user=None, password=None):
    """Tạo client đã kết nối. LWT offline retained nếu có lwt_topic."""
    c = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id=client_id)
    if lwt_topic:
        lwt = {"ts": now_iso(), "online": False}
        validate(lwt_topic, lwt)
        c.will_set(lwt_topic, json.dumps(lwt), qos=1, retain=True)
    user = user or os.environ.get("SMARTHOME_MQTT_USER")
    if user:  # không đặt thì anonymous như cũ
        c.username_pw_set(user, password if password is not None else os.environ.get("SMARTHOME_MQTT_PASSWORD"))
    c.connect(host, port)
    c.loop_start()
    return c


def publish(c, topic: str, payload: dict, retain=False, wait=True):
    """wait=False bắt buộc trong callback on_message: chờ PUBACK ở luồng mạng sẽ tự khóa."""
    validate(topic, payload)
    info = c.publish(topic, json.dumps(payload), qos=1, retain=retain)
    if wait:
        info.wait_for_publish(5)


def publish_status(c, topic: str, online=True):
    publish(c, topic, {"ts": now_iso(), "online": online}, retain=True)
