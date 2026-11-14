"""P100 giả: nghe home/device/{light,fan}/set, giữ trạng thái, publish state retained."""
import json, time

from .common import connect, now_iso, publish, publish_status, validate

IDS = ("light", "fan")


def run(host="localhost", port=1883, mute=False):
    c = connect("sim-p100", "home/device/status", host, port)

    def on_message(_c, _u, m):
        dev = m.topic.split("/")[2]
        try:
            cmd = json.loads(m.payload)
            validate(m.topic, cmd)
        except ValueError as e:  # JSON hỏng hoặc sai schema: bỏ qua, không làm sập callback
            print(f"bỏ qua lệnh sai {m.topic}: {e}", flush=True)
            return
        print(f"{dev} -> {'ON' if cmd['on'] else 'OFF'} ({cmd['source']})", flush=True)
        if mute:
            return
        publish(c, f"home/device/{dev}/state", {"ts": now_iso(), "on": cmd["on"]}, retain=True, wait=False)

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
