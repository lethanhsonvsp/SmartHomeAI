"""Tải YOLOv8n-pose ONNX vào ai/models/ và kiểm sha256. Dùng: python tools/fetch_model.py

Nguồn: https://huggingface.co/Xenova/yolov8n-pose (bản xuất ONNX của yolov8n-pose, Ultralytics).
Giấy phép: AGPL-3.0 (Ultralytics). Trọng số không commit (ai/models/ nằm trong .gitignore).
"""
import hashlib
import sys
import urllib.request
from pathlib import Path

URL = "https://huggingface.co/Xenova/yolov8n-pose/resolve/main/onnx/model.onnx"
SHA256 = "04f6d2416266f2aba6c5ba8b26de33ed9eba3279f972ac02e33f9f1366547586"
DEST = Path(__file__).resolve().parents[1] / "models" / "yolov8n-pose.onnx"


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def main():
    if DEST.exists() and sha(DEST) == SHA256:
        print("đã có, hash đúng:", DEST)
        return 0
    DEST.parent.mkdir(exist_ok=True)
    urllib.request.urlretrieve(URL, DEST)
    if sha(DEST) != SHA256:
        DEST.unlink()
        print("HASH SAI, đã xóa tệp tải về", file=sys.stderr)
        return 1
    print("đã tải, hash đúng:", DEST)
    return 0


if __name__ == "__main__":
    sys.exit(main())
