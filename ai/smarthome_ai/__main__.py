import argparse
import sys

from .detector import FakeDetector, HogDetector
from .pipeline import run
from .publisher import Publisher
from .source import CvSource, FakeSource
from .zones import Zones


def main(argv=None):
    p = argparse.ArgumentParser(prog="smarthome_ai")
    p.add_argument("--source", help="đường dẫn video hoặc rtsp://...")
    p.add_argument("--zones", required=True)
    p.add_argument("--host", default="localhost")
    p.add_argument("--port", type=int, default=1883)
    p.add_argument("--detector", choices=["hog", "fake"], default="hog")
    p.add_argument("--script", help="kịch bản JSON cho --detector fake")
    p.add_argument("--detect-fps", type=float, default=4.0)
    p.add_argument("--state-hz", type=float, default=1.0)
    a = p.parse_args(argv)
    if a.detector == "fake":
        if not a.script:
            p.error("--detector fake cần --script")
        det = FakeDetector.load(a.script)
        src = FakeSource(len(det.script), a.detect_fps)
    else:
        if not a.source:
            p.error("--detector hog cần --source")
        det, src = HogDetector(), CvSource(a.source)
    pub = Publisher(a.host, a.port)
    try:
        run(src, det, Zones.load(a.zones), pub, detect_fps=a.detect_fps, state_hz=a.state_hz)
    except KeyboardInterrupt:
        pass
    finally:
        pub.close()


if __name__ == "__main__":
    sys.exit(main())
