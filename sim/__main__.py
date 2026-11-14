import argparse

from . import esp32, p100, player
from .scenarios import SCENARIOS

p = argparse.ArgumentParser(prog="python -m sim")
p.add_argument("--host", default="localhost")
p.add_argument("--port", type=int, default=1883)
s = p.add_subparsers(dest="cmd", required=True)
r = s.add_parser("run", help="phát kịch bản"); r.add_argument("code", choices=sorted(SCENARIOS))
r.add_argument("--speed", type=float, default=1.0, help="x lần nhanh hơn thời gian thật")
r.add_argument("--dry", action="store_true", help="chỉ kiểm schema và in, không cần broker")
e = s.add_parser("esp", help="ESP32 giả"); e.add_argument("--node", default="esp1", choices=["esp1", "esp2"])
e.add_argument("--interval", type=float, default=10.0)
s.add_parser("p100", help="P100 giả (đèn, quạt)")
s.add_parser("list", help="liệt kê kịch bản")
a = p.parse_args()
if a.cmd == "run":
    player.play(a.code, a.speed, a.host, a.port, a.dry)
elif a.cmd == "esp":
    esp32.run(a.node, a.host, a.port, a.interval)
elif a.cmd == "p100":
    p100.run(a.host, a.port)
else:
    print(*SCENARIOS)
