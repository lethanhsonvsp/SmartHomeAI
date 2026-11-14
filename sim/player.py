"""Phát kịch bản lên MQTT theo thời gian (chia cho speed)."""
import time

from .common import connect, publish
from .scenarios import build


def play(code, speed=1.0, host="localhost", port=1883, dry=False):
    events = build(code)
    c = None if dry else connect(f"sim-run-{code}", None, host, port)
    t0 = time.monotonic()
    for e in events:
        time.sleep(max(0, e.t / speed - (time.monotonic() - t0)))
        if dry:
            from .common import validate
            validate(e.topic, e.payload)
        else:
            publish(c, e.topic, e.payload, e.retain)
        print(f"{e.t:6.1f}s {e.topic} {e.payload}")
    if c:
        c.disconnect()
