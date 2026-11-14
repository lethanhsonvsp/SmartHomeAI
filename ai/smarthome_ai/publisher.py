"""Publish MQTT, mọi payload kiểm schema trong docs/schemas trước khi gửi."""
import json
import os
from datetime import datetime, timezone
from pathlib import Path

import jsonschema

SCHEMAS = Path(os.environ.get("SMARTHOME_SCHEMAS", Path(__file__).resolve().parents[2] / "docs" / "schemas"))
TOPICS = {
    "state": ("home/ai/livingroom/state", "ai-state"),
    "transition": ("home/ai/livingroom/transition", "ai-transition"),
    "status": ("home/ai/status", "status"),
}


def now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def check(kind: str, payload: dict) -> None:
    """Ném ValueError nếu sai schema."""
    schema = json.loads((SCHEMAS / f"{TOPICS[kind][1]}.json").read_text())
    try:
        jsonschema.validate(payload, schema)
    except jsonschema.ValidationError as e:
        raise ValueError(f"{TOPICS[kind][0]}: {e.message}") from e


class Publisher:
    def __init__(self, host="localhost", port=1883, client=None):
        """client: tiêm client giả khi test."""
        if client is None:
            import paho.mqtt.client as mqtt
            client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="smarthome-ai")
            lwt = {"ts": now_iso(), "online": False}
            check("status", lwt)
            client.will_set(TOPICS["status"][0], json.dumps(lwt), qos=1, retain=True)
            client.connect(host, port)
            client.loop_start()
        self.c = client
        self.send("status", {"ts": now_iso(), "online": True}, retain=True)

    def send(self, kind: str, payload: dict, retain=False):
        check(kind, payload)
        self.c.publish(TOPICS[kind][0], json.dumps(payload), qos=1, retain=retain)

    def close(self):
        self.send("status", {"ts": now_iso(), "online": False}, retain=True)
        if hasattr(self.c, "loop_stop"):
            self.c.loop_stop()
            self.c.disconnect()
