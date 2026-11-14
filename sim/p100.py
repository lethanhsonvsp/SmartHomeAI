"""P100 giả: nghe home/device/{light,fan}/set, giữ trạng thái, publish state retained."""
import json, time

from .common import connect, now_iso, publish, publish_status

IDS = ("light", "fan")


def run(host="localhost", port=1883):
    c = connect("sim-p100", "home/device/status", host, port)

    def on_message(_c, _u, m):
        dev = m.topic.split("/")[2]
        cmd = json.loads(m.payload)
        print(f"{dev} -> {'ON' if cmd['on'] else 'OFF'} ({cmd['source']})")
        publish(c, f"home/device/{dev}/state", {"ts": now_iso(), "on": cmd["on"]}, retain=True)

    c.on_message = on_message
    publish_status(c, "home/device/status")
    for d in IDS:
        publish(c, f"home/device/{d}/state", {"ts": now_iso(), "on": False}, retain=True)
        c.subscribe(f"home/device/{d}/set", qos=1)
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        publish_status(c, "home/device/status", online=False)
        c.disconnect()
