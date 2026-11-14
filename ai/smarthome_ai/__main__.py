import argparse
import sys

from .detector import FakeDetector, HogDetector
from .pose import PoseDetector
from .pipeline import run
from .publisher import Publisher
from .source import CvSource, FakeSource
from .zones import Zones


def main(argv=None):
    p = argparse.ArgumentParser(prog="smarthome_ai")
    p.add_argument("--source", help="đường dẫn video, rtsp://..., hoặc chỉ số webcam (0, 1, ...)")
    p.add_argument("--zones", required=True)
    p.add_argument("--host", default="localhost")
    p.add_argument("--port", type=int, default=1883)
    p.add_argument("--user", help="MQTT user (hoặc SMARTHOME_MQTT_USER)")
    p.add_argument("--password", help="MQTT password (hoặc SMARTHOME_MQTT_PASSWORD)")
    p.add_argument("--detector", choices=["hog", "pose", "fake"], default="hog")
    p.add_argument("--script", help="kịch bản JSON cho --detector fake")
    p.add_argument("--detect-fps", type=float, default=4.0)
    p.add_argument("--state-hz", type=float, default=1.0)
    p.add_argument("--width", type=int, help="độ rộng webcam")
    p.add_argument("--height", type=int, help="độ cao webcam")
    p.add_argument("--stream-port", type=int, default=8090, help="cổng MJPEG; 0 = tắt")
    a = p.parse_args(argv)
    if a.detector == "fake":
        if not a.script:
            p.error("--detector fake cần --script")
        det = FakeDetector.load(a.script)
        src = FakeSource(len(det.script), a.detect_fps)
    else:
        if not a.source:
            p.error("--detector {hog,pose} cần --source")
        det = PoseDetector() if a.detector == "pose" else HogDetector()
        src = CvSource(a.source, a.width, a.height)
    viewer = None
    if a.stream_port:
        from .stream import StreamServer
        zn = Zones.load(a.zones)
        viewer = StreamServer(a.stream_port, zn.polys)
        print(f"MJPEG: http://localhost:{a.stream_port}/stream.mjpg", flush=True)
    pub = Publisher(a.host, a.port, user=a.user, password=a.password)
    try:
        run(src, det, Zones.load(a.zones), pub, detect_fps=a.detect_fps, state_hz=a.state_hz,
            viewer=viewer)
    except KeyboardInterrupt:
        pass
    finally:
        pub.close()
        if viewer:
            viewer.close()


if __name__ == "__main__":
    sys.exit(main())
