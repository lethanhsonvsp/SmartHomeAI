"""ESP32 giả: env mỗi interval giây, door khi đổi (gõ o/c + Enter), nhận buzzer/set."""
import json, random, signal, sys, threading, time

from .esp_control import State, serve
from .common import connect, now_iso, publish, publish_status, validate


def run(node="esp1", host="localhost", port=1883, interval=10.0, temp=None, lux=None,
        control_port=None, humidity=None, door=False):
    ctl = control_port is not None  # tắt: giữ hành vi cũ (nhiễu ngẫu nhiên, không HTTP)
    st = State(28.0 if temp is None else temp, 60.0 if humidity is None else humidity,
               150 if lux is None else lux, door)
    temp = 29.0 if temp is None else temp
    lux = 200.0 if lux is None else lux
    base = f"home/sensor/{node}"
    c = connect(f"sim-{node}", f"{base}/status", host, port)
    def on_message(_c, _u, m):
        try:
            cmd = json.loads(m.payload)
            validate(m.topic, cmd)
        except ValueError as e:
            return print(f"bỏ qua lệnh sai {m.topic}: {e}", flush=True)
        st.v["buzzer"] = cmd["on"]
        print("buzzer/set", cmd, flush=True)

    c.on_message = on_message
    c.subscribe(f"{base}/buzzer/set", qos=1)
    publish_status(c, f"{base}/status")
    def pub_door(o):
        publish(c, f"{base}/door", {"ts": now_iso(), "open": o}, retain=True, wait=False)

    def pub_env():
        v = st.snapshot()
        e = ({"temp_c": v["tempC"], "humidity": v["humidity"], "lux": v["lux"]} if ctl else
             {"temp_c": round(temp + random.uniform(-.3, .3), 1), "humidity": round(random.uniform(55, 65), 1), "lux": round(lux)})
        publish(c, f"{base}/env", {"ts": now_iso(), **e}, wait=False)

    pub_door(door)
    wake = threading.Event()  # POST /set có đổi cảm biến: phát env ngay
    srv = None
    if ctl:
        def changed(ch):
            if "doorOpen" in ch:
                pub_door(st.snapshot()["doorOpen"])
            if ch & {"tempC", "humidity", "lux"}:
                wake.set()
        srv = serve(st, control_port, changed)
        print(f"điều khiển http://127.0.0.1:{control_port}/state", flush=True)

    def keys():
        for line in sys.stdin:
            if line.strip() in ("o", "c"):
                publish(c, f"{base}/door", {"ts": now_iso(), "open": line.strip() == "o"}, retain=True)

    threading.Thread(target=keys, daemon=True).start()
    signal.signal(signal.SIGTERM, signal.default_int_handler)  # SIGTERM tắt sạch như Ctrl+C
    try:
        while True:
            pub_env()
            wake.wait(interval)
            wake.clear()
    except KeyboardInterrupt:
        if srv:
            srv.shutdown()
            srv.server_close()
        publish_status(c, f"{base}/status", online=False)  # thoát sạch: LWT không tự gửi
        c.disconnect()
