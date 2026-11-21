"""Tải mô hình pose ONNX (YOLOv8, YOLO26, RTMO; tổng ~850 MB) vào ai/models/ và kiểm sha256. Dùng: python tools/fetch_model.py [tên ...]

Nguồn: Hugging Face Xenova/yolov8*-pose, onnx-community/yolo26*-pose-ONNX, Xenova/RTMO-* (xem MODELS).
Mặc định dùng bản m; chọn bản khác trên Dashboard hoặc bằng --model models/<tên>.onnx.
Giấy phép: YOLO là AGPL-3.0 (Ultralytics), RTMO là Apache-2.0 (OpenMMLab). Trọng số không commit (ai/models/ nằm trong .gitignore).
"""
import hashlib
import sys
import urllib.request
from pathlib import Path

# tên -> (repo Hugging Face, sha256 của onnx/model.onnx)
MODELS = {
    "yolov8n-pose": ("Xenova/yolov8n-pose", "04f6d2416266f2aba6c5ba8b26de33ed9eba3279f972ac02e33f9f1366547586"),
    "yolov8s-pose": ("Xenova/yolov8s-pose", "5c92815cc509a9ccb6a817f4c2816de800092964319ad179a2751eda6df35d83"),
    "yolov8m-pose": ("Xenova/yolov8m-pose", "b44ece4006060bd708b7a542f25ae76c758ba2827a6ebe2e22b18be548e23a0b"),
    "yolov8l-pose": ("Xenova/yolov8l-pose", "041a00236a52aafb1cf813e7862a7f2aa03cebf56871edfd28d76c3737215dea"),
    "yolo26n-pose": ("onnx-community/yolo26n-pose-ONNX", "6ca5dc9681e0305708085715427f50779c761d5abfddaa0c38ac7c2d89b56085"),
    "yolo26s-pose": ("onnx-community/yolo26s-pose-ONNX", "6a53bd3781df38b0b7b0fe01e32a1cc7bba8343a42a066a4a37f752c74b60f8a"),
    "yolo26m-pose": ("onnx-community/yolo26m-pose-ONNX", "b88de24c03d876db75301642922c2fd83ae889fe4de56bd93d243c39fe309283"),
    "yolo26l-pose": ("onnx-community/yolo26l-pose-ONNX", "1526e49012d7871d6548417e8c97584382afff6c243c2be3e76a94fc2d7b5e70"),
    "rtmo-s": ("Xenova/RTMO-s", "1cd6a3517658903233e9ccb78666554862f57f0944b6886e2048093594aef4e7"),
    "rtmo-m": ("Xenova/RTMO-m", "76d82c45e5c4810baf587ecf2638d15cbf7ed196afb2055e977382532021b59e"),
    "rtmo-l": ("Xenova/RTMO-l", "293b4b10165b3862fc00b81a9473803838a22046115d54d80f8fdc24daa9a030"),
}
DIR = Path(__file__).resolve().parents[1] / "models"


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def fetch(name, repo, want):
    dest = DIR / f"{name}.onnx"
    if dest.exists() and sha(dest) == want:
        print("đã có, hash đúng:", dest)
        return True
    urllib.request.urlretrieve(f"https://huggingface.co/{repo}/resolve/main/onnx/model.onnx", dest)
    if sha(dest) != want:
        dest.unlink()
        print("HASH SAI, đã xóa tệp tải về:", dest, file=sys.stderr)
        return False
    print("đã tải, hash đúng:", dest)
    return True


def main():
    DIR.mkdir(exist_ok=True)
    # Có tham số thì chỉ tải các bản được nêu tên (vd: yolov8m-pose); tên lạ báo KeyError.
    return 0 if all([fetch(n, *MODELS[n]) for n in sys.argv[1:] or MODELS]) else 1


if __name__ == "__main__":
    sys.exit(main())
