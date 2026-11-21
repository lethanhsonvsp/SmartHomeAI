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
`--zones` tùy chọn: bỏ qua thì dùng `config/zones.json` nếu có, không thì `zones.example.json`. `config/zones.json` là cấu hình cục bộ (gitignore), do `PUT /zones` ghi.
Xem tin: `mosquitto_sub -t 'home/ai/#' -v`.

## Luồng hình cho Dashboard
Cùng tiến trình mở HTTP ở `--stream-port` (mặc định 8090, `0` = tắt): `GET /stream.mjpg` (multipart/x-mixed-replace, ~8 fps), `GET /snapshot.jpg`, kèm `Access-Control-Allow-Origin: *`; nhúng bằng `<img src="http://host:8090/stream.mjpg">`. Khung có vùng (tô nhạt), hộp, xương COCO-17, `#track_id posture conf [zone]` (chữ ASCII). Chưa có khung hoặc camera mất >5 s thì trả ảnh "Dang cho camera". Server nghe 0.0.0.0, không xác thực: chỉ dùng trong LAN.

### API cấu hình (cùng cổng, CORS GET/POST/PUT/OPTIONS, thân tối đa 64 KB)
- `GET /config` -> `{"skeleton":true,"boxes":true,"zones":true,"labels":true,"sofa_lying":false}`; `POST /config` nhận một phần các khóa đó (bool; khóa lạ/sai kiểu = 400), đổi lớp phủ stream + snapshot ngay, trả config mới. `--no-skeleton` đặt mặc định ban đầu. `sofa_lying` (mặc định theo `--sofa-lying`): cam soi từ trên cao, người đứng yên có tâm hộp trong vùng sofa thì coi là `lying`.
- `GET /frame.jpg`: khung mới nhất KHÔNG lớp phủ (nền cho trình vẽ vùng; ảnh chờ nếu chưa có khung), `Cache-Control: no-store`.
- `GET /zones` -> `{"zones": {...}}` (định dạng như `zones.example.json`); `PUT /zones` cùng định dạng: chỉ tên `livingroom, sofa, door, other_exit`, bắt buộc `livingroom`, mỗi vùng 3..20 điểm `[x,y]` trong 0..1; sai = 400 kèm `error`, không đổi gì. Hợp lệ thì dùng ngay từ khung kế (giữ tracker) và ghi nguyên tử `zones.json` cạnh tệp `--zones` (hoặc `config/zones.json`). `livingroom` luôn được xếp cuối (ưu tiên thấp nhất); các vùng khác giữ thứ tự gửi. `GET /zones/default` -> vùng ví dụ.

## Mô hình pose
`--detector pose` = YOLOv8m-pose ONNX chạy bằng onnxruntime CPU (không torch), ngưỡng tin cậy 0.3. `tools/fetch_model.py` tải 4 bản YOLOv8 n/s/m/l cùng YOLO26-pose (n/s/m/l) và RTMO (s/m/l), loại mô hình tự nhận theo đầu ra ONNX; chọn trên Dashboard (trang Cài đặt, đổi là AI khởi động lại, lựa chọn lưu ở `config/model.txt`) hoặc `--model models/<tên>.onnx`. Nguồn: https://huggingface.co/Xenova/yolov8m-pose và các bản n/s/l cùng chỗ (xuất từ Ultralytics). Số đo so sánh: [docs/nhat-ky-camera-tapo.md](../docs/nhat-ky-camera-tapo.md). **Giấy phép AGPL-3.0** (Ultralytics): phù hợp đồ án học thuật/nội bộ; nếu phát hành sản phẩm đóng kín phải đổi mô hình hoặc mua giấy phép. Thiếu mô hình thì báo lỗi rõ, dùng `--detector hog` làm dự phòng.

## Quyết định và giới hạn
- Detector mặc định vẫn là HOG của OpenCV (không thêm phụ thuộc nặng; ultralytics kéo torch cỡ GB). Kém YOLO, nhất là người nằm. Cắm detector khác qua giao diện `Detector.detect(frame) -> [Detection]` (hộp chuẩn hóa, keypoints COCO-17 tùy chọn).
- Posture là quy tắc hình học (keypoints nếu có: thân nằm ngang hoặc đầu-vai-hông cùng độ cao = lying, hông ngang gối = sitting, còn lại standing; thiếu khớp thì dùng tỉ lệ hộp; moving theo tốc độ tâm), ngưỡng đoán trong `posture.py`: BASELINE, phải đo bằng video thật.
- Vùng: đa giác chuẩn hóa 0-1, thứ tự trong file là ưu tiên; điểm xét theo tư thế: lying/sitting dùng tâm hộp, còn lại dùng chân người (giữa cạnh dưới hộp), dự phòng tâm hộp.
- Chống nhiễu: tư thế bỏ phiếu số đông `--posture-window` khung (mặc định 8 ≈ 2 s ở 4 fps); track mới phát `unknown` cho tới khi đủ 4 khung; hòa phiếu giữ tư thế đang phát (chưa có thì `unknown`). Track mất tạm (≤ 8 khung ≈ 2 s) vẫn nằm trong `persons` với tư thế/vùng cuối, hết hạn mới biến mất.
- Transition phát khi vùng mới ổn định `--zone-stable` khung (mặc định 6 ≈ 1,5 s); `direction` suy từ cặp vùng (phòng -> door/other_exit = out, ngược lại = in).
- RTSP mất luồng: publish `camera_ok:false` và thử mở lại mỗi 2 s.
- `conf` là độ tin cậy detector, chưa hiệu chuẩn.


## Chạy từ gốc repo
`ai/run.sh --source 0 --detector pose --zones ai/config/zones.example.json --host localhost --stream-port 8090` (đặt PYTHONPATH giúp; chạy `python -m smarthome_ai` trực tiếp từ gốc repo sẽ báo "No module named smarthome_ai").

.venv/bin/python test_p100.py --control off

.venv/bin/python test_p100.py --control on