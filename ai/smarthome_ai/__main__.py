import argparse
import sys

from .detector import FakeDetector, HogDetector
from .pose import PoseDetector
from .pipeline import run
from .publisher import Publisher
from .source import CvSource, FakeSource
from pathlib import Path

from .zones import CONFIG_DIR, EXAMPLE_PATH, Zones


def main(argv=None):
    p = argparse.ArgumentParser(prog="smarthome_ai")
    p.add_argument("--source", help="đường dẫn video, rtsp://..., hoặc chỉ số webcam (0, 1, ...)")
    p.add_argument("--zones", help="tệp vùng; mặc định config/zones.json nếu có, không thì zones.example.json")
    p.add_argument("--no-skeleton", action="store_true", help="mặc định ban đầu: không vẽ xương trên luồng hình")
    p.add_argument("--host", default="localhost")
    p.add_argument("--port", type=int, default=1883)
    p.add_argument("--user", help="MQTT user (hoặc SMARTHOME_MQTT_USER)")
    p.add_argument("--password", help="MQTT password (hoặc SMARTHOME_MQTT_PASSWORD)")
    p.add_argument("--detector", choices=["hog", "pose", "fake"], default="hog")
    p.add_argument("--model", help="tệp ONNX pose (mặc định models/yolov8m-pose.onnx)")
    p.add_argument("--script", help="kịch bản JSON cho --detector fake")
    p.add_argument("--detect-fps", type=float, default=4.0)
    p.add_argument("--state-hz", type=float, default=1.0)
    p.add_argument("--posture-window", type=int, default=8, help="số khung bỏ phiếu tư thế (~2 s ở 4 fps)")
    p.add_argument("--zone-stable", type=int, default=6, help="số khung vùng mới phải ổn định mới phát transition")
    p.add_argument("--sofa-lying", action="store_true", help="tâm hộp trong vùng sofa, không di chuyển thì coi là 'lying' (cam soi từ trên cao)")
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
        det = (PoseDetector(a.model) if a.model else PoseDetector()) if a.detector == "pose" else HogDetector()
        src = CvSource(a.source, a.width, a.height)
    local = CONFIG_DIR / "zones.json"
    zpath = Path(a.zones) if a.zones else (local if local.exists() else EXAMPLE_PATH)
    # PUT /zones ghi vào zones.json cạnh tệp --zones (ghi đè nếu chính nó là zones.json)
    save_to = zpath if zpath.name == "zones.json" else zpath.with_name("zones.json")
    zn = Zones.load(zpath)
    viewer = None
    if a.stream_port:
        from .stream import StreamServer
        viewer = StreamServer(a.stream_port, zn, zones_path=save_to, skeleton=not a.no_skeleton, sofa_lying=a.sofa_lying)
        print(f"MJPEG: http://localhost:{a.stream_port}/stream.mjpg", flush=True)
    pub = Publisher(a.host, a.port, user=a.user, password=a.password)
    try:
        run(src, det, zn, pub, detect_fps=a.detect_fps, state_hz=a.state_hz,
            stable_frames=a.zone_stable, posture_window=a.posture_window,
            viewer=viewer, sofa_lying=a.sofa_lying)
    except KeyboardInterrupt:
        pass
    finally:
        pub.close()
        if viewer:
            viewer.close()


if __name__ == "__main__":
    sys.exit(main())
