# smarthome-ai (NV01)

Python chỉ QUAN SÁT: phát hiện người, track, posture, chuyển vùng, rồi publish `home/ai/livingroom/state`, `transition`, `home/ai/status` (retained + LWT). Payload kiểm theo `docs/schemas/` trước khi gửi. Trạng thái hiện diện do Hub giữ.

## Cài và test
```
cd ai && python3 -m venv .venv && .venv/bin/pip install -e '.[test]' && .venv/bin/pytest
```
Mô hình pose (một lần): `.venv/bin/python tools/fetch_model.py` (tải vào `ai/models/`, kiểm sha256; thư mục này gitignore, không commit trọng số).
(OpenCV ghim <5: bản 5.0 trên pip không có `HOGDescriptor`.)

## Chạy
```
docker compose up -d mosquitto            # ở gốc repo
cp config/zones.example.json config/zones.json   # vẽ lại đa giác theo góc camera thật
.venv/bin/python -m smarthome_ai --source video.mp4 --zones config/zones.json --host localhost
.venv/bin/python -m smarthome_ai --source rtsp://user:pass@ip/stream1 --zones config/zones.json
# webcam + pose + xem trên web (cổng 8090):
.venv/bin/python -m smarthome_ai --source 0 --detector pose --zones config/zones.example.json --stream-port 8090 [--width 640 --height 480]
# thử không camera, kịch bản có sẵn:
.venv/bin/python -m smarthome_ai --detector fake --script config/fake-script.example.json --zones config/zones.example.json
```
Xem tin: `mosquitto_sub -t 'home/ai/#' -v`.

## Luồng hình cho Dashboard
Cùng tiến trình mở HTTP ở `--stream-port` (mặc định 8090, `0` = tắt): `GET /stream.mjpg` (multipart/x-mixed-replace, ~8 fps), `GET /snapshot.jpg`, kèm `Access-Control-Allow-Origin: *`; nhúng bằng `<img src="http://host:8090/stream.mjpg">`. Khung có vùng (tô nhạt), hộp, xương COCO-17, `#track_id posture conf [zone]` (chữ ASCII). Chưa có khung hoặc camera mất >5 s thì trả ảnh "Dang cho camera". Server nghe 0.0.0.0, không xác thực: chỉ dùng trong LAN.

## Mô hình pose
`--detector pose` = YOLOv8n-pose ONNX chạy bằng onnxruntime CPU (không torch). Nguồn: https://huggingface.co/Xenova/yolov8n-pose (xuất từ Ultralytics yolov8n-pose). **Giấy phép AGPL-3.0** (Ultralytics): phù hợp đồ án học thuật/nội bộ; nếu phát hành sản phẩm đóng kín phải đổi mô hình hoặc mua giấy phép. Thiếu mô hình thì báo lỗi rõ, dùng `--detector hog` làm dự phòng.

## Quyết định và giới hạn
- Detector mặc định vẫn là HOG của OpenCV (không thêm phụ thuộc nặng; ultralytics kéo torch cỡ GB). Kém YOLO, nhất là người nằm. Cắm detector khác qua giao diện `Detector.detect(frame) -> [Detection]` (hộp chuẩn hóa, keypoints COCO-17 tùy chọn).
- Posture là quy tắc hình học (keypoints nếu có: thân nằm ngang hoặc đầu-vai-hông cùng độ cao = lying, hông ngang gối = sitting, còn lại standing; thiếu khớp thì dùng tỉ lệ hộp; moving theo tốc độ tâm), ngưỡng đoán trong `posture.py`: BASELINE, phải đo bằng video thật.
- Vùng: đa giác chuẩn hóa 0-1, thứ tự trong file là ưu tiên; điểm xét là chân người (giữa cạnh dưới hộp), dự phòng tâm hộp.
- Transition phát khi vùng mới ổn định 3 khung; `direction` suy từ cặp vùng (phòng -> door/other_exit = out, ngược lại = in).
- RTSP mất luồng: publish `camera_ok:false` và thử mở lại mỗi 2 s.
- `conf` là độ tin cậy detector, chưa hiệu chuẩn.


## Chạy từ gốc repo
`ai/run.sh --source 0 --detector pose --zones ai/config/zones.example.json --host localhost --stream-port 8090` (đặt PYTHONPATH giúp; chạy `python -m smarthome_ai` trực tiếp từ gốc repo sẽ báo "No module named smarthome_ai").
