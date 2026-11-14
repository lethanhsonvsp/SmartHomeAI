# Hướng dẫn thực hiện đồ án

Dành cho người làm đồ án. Đọc cùng `docs/nhiem-vu.md` (việc cần làm) và `docs/mqtt-topics.md` (hợp đồng dữ liệu). Mục "Tình trạng" cuối file cho biết phần nào đã có.

## 1. Chuẩn bị máy

Cần: .NET SDK 10, Python 3.11+, Docker, Git. MATLAB cài sau, chỉ dùng ở giai đoạn phân tích.

```bash
git clone <repo> && cd SmartHomeAI
docker compose up -d                 # PostgreSQL + Mosquitto
dotnet build --no-incremental        # nhớ --no-incremental để thấy hết cảnh báo
dotnet test
python3 -m venv sim/.venv && sim/.venv/bin/pip install -r sim/requirements.txt
sim/.venv/bin/python -m pytest sim
```

Cả hai bộ test qua là máy đã sẵn sàng.

## 2. Cách làm việc

- Làm trước bằng mô phỏng, phần cứng thật ghép sau. Thư mục `sim/` giả ESP32, P100 và phát kịch bản.
- Mỗi kịch bản SC01–SC12 phải có một test tự động. Viết test trước, sửa code cho qua.
- Mọi dữ liệu trao đổi qua MQTT phải đúng schema trong `docs/schemas/`. Muốn đổi payload thì sửa `docs/mqtt-topics.md` và schema trước, code sau.
- Ranh giới cố định: Python AI chỉ quan sát và gửi sự kiện; Hub (C#) giữ trạng thái hiện diện và ra quyết định.
- Mỗi việc xong thì commit riêng, câu commit nói rõ làm gì.
- Phạm vi tối thiểu của đồ án là SC01–SC11. Học thói quen (NV03) chỉ cần đề xuất ngưỡng; làm sâu hơn là phần thêm.

## 3. Các bước

### Bước 0. Hợp đồng dữ liệu
Đọc `docs/mqtt-topics.md`. Hiểu từng topic, ai gửi, ai nhận, tần suất. Chốt các mục "Chưa chốt" ở cuối file khi có phần cứng.

### Bước 1. Mô phỏng và Occupancy Engine
- Xem kịch bản: `sim/.venv/bin/python -m sim list`.
- Phát thử: `sim/.venv/bin/python -m sim run SC07 --speed 20`, mở cửa sổ khác chạy `mosquitto_sub -t 'home/#' -v` để xem tin.
- Đọc `src/SmartHomeAI.Core/Occupancy/`. Chạy `dotnet test` để thấy SC05, SC06, SC07, SC08, SC10, SC11.
- Nếu Hub tính thời gian theo `ts` trong payload thì chạy sim với `--ts sim`.

### Bước 2. Hub nhận MQTT và lưu dữ liệu (NV05, NV08)
- Hub kết nối Mosquitto, nhận mọi topic `home/#`, đẩy vào Occupancy Engine, ghi log vào PostgreSQL.
- Bật ghi log cảm biến và lệnh điều khiển ngay từ bước này. Đề tài học thói quen (SC12) và MATLAB cần dữ liệu nhiều tuần, để muộn là thiếu.
- Kiểm: chạy sim, gọi `GET /api/state` của Hub, xem bảng trong PostgreSQL có dữ liệu.

### Bước 3. Automation (NV04)
- Engine ở `src/SmartHomeAI.Core/Automation/`: đèn theo độ sáng, quạt theo nhiệt độ và tư thế nằm sofa, lệnh thủ công thắng lệnh tự động (SC09), chống bật tắt liên tục.
- Nối vào Hub: Hub gọi engine rồi publish lệnh tới `home/device/{id}/set`.
- Kiểm bằng sim: SC01, SC02, SC03, SC04, SC05, SC09.

### Bước 4. AI camera (NV01) — việc rủi ro nhất
- Làm trong `ai/`. Python chỉ phát sự kiện `home/ai/livingroom/state` và `transition` đúng schema, không giữ trạng thái hiện diện.
- Quay video thật càng sớm càng tốt (điện thoại đặt đúng góc dự kiến của Tapo C222): ban ngày, ban đêm, nằm sofa, ra vào cửa, hai người. Không có dữ liệu thật thì không đo được độ chính xác.
- Gợi ý: phát hiện người và pose bằng mô hình nhỏ chạy CPU (3–5 khung hình/giây là đủ), chia vùng (phòng khách, sofa, cửa) bằng đa giác cấu hình được, làm mượt theo thời gian để tránh nhảy trạng thái.
- Chạy thử trên video ghi sẵn trước, RTSP thật sau.
- Đo và ghi lại: độ chính xác nằm sofa, tỷ lệ báo nhà vắng sai. Ưu tiên không báo sai hơn không bỏ sót.

### Bước 5. Cảnh báo và Dashboard (NV06, NV07)
- Cảnh báo: xác minh đi vắng 30 giây, báo lần đầu sau 2 phút, nhắc lại sau 5 phút, tùy chọn tự tắt sau 10 phút, hủy khi người về hoặc thiết bị đã tắt. Test SC07 và SC11.
- Dashboard Blazor: trạng thái người, cảm biến, đèn quạt, chế độ Auto/Manual, cấu hình ngưỡng, lịch sử và biểu đồ.

### Bước 6. Học thói quen và MATLAB (NV03, NV09)
- Dùng dữ liệu đã ghi từ bước 2. Đề xuất ngưỡng nhiệt độ và ánh sáng từ lần người dùng điều khiển thủ công; người dùng duyệt, sửa hoặc tắt từng luật.
- MATLAB: xuất dữ liệu từ PostgreSQL ra CSV rồi phân tích trong `matlab/`. So luật cố định với luật thích nghi, xuất biểu đồ và bảng cho luận văn.

### Bước 7. Bảo mật và triển khai (NV08, NV10)
- Đóng `allow_anonymous` trong `infra/mosquitto/mosquitto.conf`, thêm tài khoản MQTT. Thêm xác thực cho API, sao lưu PostgreSQL.
- Nâng gói Blazor đang có cảnh báo lỗ hổng NU1903 khi build.
- Triển khai bằng systemd: dùng `infra/systemd/smarthome-hub.service` và `smarthome-ai.service` (sửa đường dẫn và user cho đúng máy), rồi `systemctl enable --now`.
- Thử lỗi: mất MQTT, mất camera (SC10), mất Internet, khởi động lại server; đo CPU, RAM khi chạy AI và độ trễ từ cảm biến đến ổ cắm.

## 4. Ghép phần cứng thật

Làm lần lượt, mỗi thiết bị một lần, thay đúng phần mô phỏng tương ứng:
1. **ESP32 + SHT30, BH1750, MC-38:** firmware publish đúng topic `home/sensor/{node}/...` và có LWT. Xem tin bằng `mosquitto_sub`.
2. **Ổ cắm Tapo P100:** thử điều khiển qua LAN bằng một thư viện có sẵn, sớm. Firmware có thể đổi giao thức; nếu hỏng, dùng relay qua ESP32 thay thế. Viết một adapter nhận `home/device/{id}/set` và trả `state`, giống `sim/p100.py`.
3. **Camera Tapo C222:** bật RTSP trong app, tạo tài khoản camera, thử đọc luồng RTSP bằng OpenCV.
4. Chạy kịch bản thật và đối chiếu với mô phỏng; chỗ nào lệch thì cập nhật sim bằng dữ liệu đã ghi.

## 5. Điều cần cẩn thận

- Một camera không biết có người ở phòng ngoài vùng quan sát. Chỉ kết luận "đi vắng" khi đủ chuỗi bằng chứng: cửa mở, người đi ra, hết người trong hình, qua thời gian xác minh. Thiếu bằng chứng thì ở trạng thái chưa biết.
- Camera mất kết nối không được suy ra nhà trống.
- Thiết bị không tự bật lại trái lệnh thủ công.
- Con số mặc định (50 lux, 28 °C, 30 giây, 60 giây, 30 phút) là giá trị tạm, phải chỉnh lại theo dữ liệu thật và ghi lý do vào luận văn.
- Mô phỏng chạy qua chưa chứng minh hệ thống đúng ngoài đời. Luôn kiểm lại bằng dữ liệu thật.

## 6. Tình trạng hiện tại

Xong: khung dự án, hợp đồng MQTT, mô phỏng, Occupancy Engine, Automation Engine, Hub nhận MQTT, `GET /api/state` và ghi log PostgreSQL. Automation chưa nối vào Hub (Hub chưa tự gửi lệnh nào). Chưa có: AI camera, cảnh báo, Dashboard, học thói quen, MATLAB, bảo mật, triển khai. Danh sách đầy đủ ở `docs/nhiem-vu.md`.

Lưu ý khi chạy Hub: `dotnet run` dùng cổng trong `launchSettings.json` (5002), không phải `ASPNETCORE_URLS`. Chuỗi kết nối PostgreSQL mặc định trỏ cổng 5432; máy đã có PostgreSQL khác thì ghi đè bằng biến môi trường `ConnectionStrings__Postgres`.
