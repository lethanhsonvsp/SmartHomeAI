"""ESP32 giả: env mỗi interval giây, door khi đổi (gõ o/c + Enter), nhận buzzer/set."""
import json, random, sys, threading, time

from .common import connect, now_iso, publish, publish_status


def run(node="esp1", host="localhost", port=1883, interval=10.0, temp=29.0, lux=200.0):
    base = f"home/sensor/{node}"
    c = connect(f"sim-{node}", f"{base}/status", host, port)
    c.on_message = lambda _c, _u, m: print("buzzer/set", json.loads(m.payload))
    c.subscribe(f"{base}/buzzer/set", qos=1)
    publish_status(c, f"{base}/status")
    publish(c, f"{base}/door", {"ts": now_iso(), "open": False}, retain=True)

    def keys():
        for line in sys.stdin:
            if line.strip() in ("o", "c"):
                publish(c, f"{base}/door", {"ts": now_iso(), "open": line.strip() == "o"}, retain=True)

    threading.Thread(target=keys, daemon=True).start()
    try:
        while True:
            publish(c, f"{base}/env", {"ts": now_iso(), "temp_c": round(temp + random.uniform(-.3, .3), 1),
                                       "humidity": round(random.uniform(55, 65), 1), "lux": round(lux)})
            time.sleep(interval)
    except KeyboardInterrupt:
        publish_status(c, f"{base}/status", online=False)  # thoát sạch: LWT không tự gửi
        c.disconnect()
