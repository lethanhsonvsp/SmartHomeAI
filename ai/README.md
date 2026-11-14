# smarthome-ai (NV01)

Python chỉ QUAN SÁT: phát hiện người, track, posture, chuyển vùng, rồi publish `home/ai/livingroom/state`, `transition`, `home/ai/status` (retained + LWT). Payload kiểm theo `docs/schemas/` trước khi gửi. Trạng thái hiện diện do Hub giữ.

## Cài và test
```
cd ai && python3 -m venv .venv && .venv/bin/pip install -e '.[test]' && .venv/bin/pytest
```
(OpenCV ghim <5: bản 5.0 trên pip không có `HOGDescriptor`.)

## Chạy
```
docker compose up -d mosquitto            # ở gốc repo
cp config/zones.example.json config/zones.json   # vẽ lại đa giác theo góc camera thật
.venv/bin/python -m smarthome_ai --source video.mp4 --zones config/zones.json --host localhost
.venv/bin/python -m smarthome_ai --source rtsp://user:pass@ip/stream1 --zones config/zones.json
# thử không camera, kịch bản có sẵn:
.venv/bin/python -m smarthome_ai --detector fake --script config/fake-script.example.json --zones config/zones.example.json
```
Xem tin: `mosquitto_sub -t 'home/ai/#' -v`.

## Quyết định và giới hạn
- Detector mặc định là HOG của OpenCV (không thêm phụ thuộc nặng; ultralytics kéo torch cỡ GB). Kém YOLO, nhất là người nằm. Cắm detector khác qua giao diện `Detector.detect(frame) -> [Detection]` (hộp chuẩn hóa, keypoints COCO-17 tùy chọn).
- Posture là quy tắc hình học (tỉ lệ hộp, tốc độ tâm, keypoints nếu có), ngưỡng đoán trong `posture.py`: BASELINE, phải đo bằng video thật.
- Vùng: đa giác chuẩn hóa 0-1, thứ tự trong file là ưu tiên; điểm xét là chân người (giữa cạnh dưới hộp), dự phòng tâm hộp.
- Transition phát khi vùng mới ổn định 3 khung; `direction` suy từ cặp vùng (phòng -> door/other_exit = out, ngược lại = in).
- RTSP mất luồng: publish `camera_ok:false` và thử mở lại mỗi 2 s.
- `conf` là độ tin cậy detector, chưa hiệu chuẩn.
