# Hướng dẫn thực hiện đồ án (từ dễ đến khó)

Tài liệu gồm 11 bài. Mỗi bài có ba phần: **Mục tiêu**, **Làm**, **Bạn sẽ thấy**. Làm xong bài trước mới
sang bài sau. Nếu kết quả không giống mục "Bạn sẽ thấy" thì xem mục "Nếu không được" của bài đó, đừng đi tiếp.

Trước khi bắt đầu, đọc [README.md](README.md) để biết năm cái tên: AI, Hub, Dashboard, ESP32, Mô phỏng.

| Bậc | Bài | Cần phần cứng? |
|---|---|---|
| 1. Chạy được | 1, 2, 3 | Không |
| 2. Hiểu các khối | 4, 5 | Không |
| 3. Phần cứng thật | 6, 7, 8 | Có |
| 4. Đọc và sửa mã | 9, 10, 11 | Không |

---

## Bậc 1. Chạy được

### Bài 1. Cài và chạy toàn bộ hệ thống

**Mục tiêu.** Mở được trang Dashboard trên máy mình.

**Làm.** Máy chỉ cần cài Docker và Git (Ubuntu/Debian).

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER        # xong thì đăng xuất, đăng nhập lại
git clone https://github.com/lethanhsonvsp/SmartHomeAI.git
cd SmartHomeAI
docker compose up -d --build         # lần đầu mất vài phút
```

Mở trình duyệt: http://localhost:5037

**Bạn sẽ thấy.** Trang SmartHomeAI, góc trên phải có chấm xanh "Hub chạy". Camera báo "Camera tắt", các
hàng cảm biến hiện dấu gạch. Như vậy là đúng, vì chưa có nguồn dữ liệu nào.

**Giải thích.** Lệnh `docker compose up` dựng bốn "hộp" (container): `postgres` (cơ sở dữ liệu),
`mosquitto` (bưu điện MQTT), `hub` (bộ não), `dashboard` (trang web, kèm AI và mô phỏng).
Xem chúng bằng `docker compose ps`.

**Nếu không được.**
- `docker: permission denied`: bạn chưa đăng xuất, đăng nhập lại sau lệnh `usermod`.
- Trang không mở: `docker compose logs -f dashboard` để đọc lỗi. Dashboard chỉ lên sau khi Hub "healthy".
- Cổng 5037 đã có chương trình khác dùng: tạo tệp `.env` có dòng `DASHBOARD_PORT=5038` rồi chạy lại.

### Bài 2. Cho hệ thống "ăn" dữ liệu giả

**Mục tiêu.** Thấy số liệu cảm biến chạy trên Dashboard mà chưa cần phần cứng.

**Làm.** Trên Dashboard bấm **Mô phỏng** (góc trên phải), bật hai công tắc: "Mô phỏng cảm biến (ESP32)" và
"Mô phỏng ổ cắm".

**Bạn sẽ thấy.** Ô "Thiết bị": các hàng Nhiệt độ, Ánh sáng, Cửa chuyển chấm xanh và có số (ví dụ 28 °C, 150 lux).
Trong ngăn Mô phỏng có thanh kéo nhiệt độ, độ sáng, cửa: kéo thử, số trên Dashboard đổi theo sau vài giây.

**Giải thích.** Chương trình mô phỏng đóng vai mạch ESP32: cứ 10 giây nó gửi một tin "nhiệt độ, độ ẩm,
ánh sáng" lên bưu điện MQTT. Hub nhận tin đó y như nhận từ mạch thật. Đây là lý do có thể viết và thử gần hết
phần mềm trước khi mua linh kiện.

### Bài 3. Chạy kịch bản đầu tiên: vào phòng tối, đèn tự bật

**Mục tiêu.** Thấy hệ thống tự ra quyết định.

**Làm.**
1. Ở ô "Thiết bị" chọn chế độ **Auto**.
2. Tắt công tắc AI camera nếu đang bật (kịch bản và camera cùng gửi dữ liệu người nên không chạy chung).
3. Trong ngăn Mô phỏng, phần Kịch bản, chọn **SC01** rồi bấm chạy.

**Bạn sẽ thấy.** Ô "Hiện diện" đổi sang "Có người". Sau vài giây, ô "Nhật ký hoạt động" có dòng
"Đèn đã bật", hàng Ổ cắm đổi sang "Bật".

**Giải thích.** SC01 là kịch bản "vào phòng khách, trời tối". Mô phỏng gửi tin "có một người ở phòng khách"
và "ánh sáng 18 lux". Hub ghép hai tin, áp luật "có người và dưới 50 lux thì bật đèn", rồi gửi lệnh cho ổ cắm.
Thử tiếp SC02 (trời sáng, đèn giữ nguyên) và SC07 (ra ngoài quên tắt, có cảnh báo). Danh sách 12 kịch bản SC
nằm trong [nhiem-vu.md](nhiem-vu.md).

**Nếu không được.** Nhật ký ghi "Đang chờ: ..." kèm lý do. Đọc lý do đó: thường là đang ở Manual, hoặc vừa
bấm tay nên tự động tạm nghỉ.

---

## Bậc 2. Hiểu các khối

### Bài 4. Nhìn tận mắt các tin nhắn MQTT

**Mục tiêu.** Hiểu "các khối nói chuyện với nhau" nghĩa là gì.

**Làm.** Giữ mô phỏng cảm biến đang bật, mở cửa sổ lệnh:

```bash
docker compose exec mosquitto mosquitto_sub -t 'home/#' -v
```

**Bạn sẽ thấy.** Mỗi dòng là một tin, gồm địa chỉ (topic) và nội dung (JSON):

```
home/sensor/esp1/env {"ts": "2026-11-21T11:28:44Z", "temp_c": 28.0, "humidity": 60.0, "lux": 150}
home/sensor/esp1/door {"ts": "2026-11-21T11:28:49Z", "open": false}
home/device/light/state {"ts": "...", "on": true}
```

**Giải thích.** `home/sensor/esp1/env` đọc là: nhà / cảm biến / mạch esp1 / môi trường. `home/#` nghĩa là
"mọi tin bắt đầu bằng home". Chạy lại SC01 và quan sát thứ tự tin: tin AI báo có người, rồi Hub gửi
`home/device/light/set` (lệnh bật), rồi ổ cắm trả `home/device/light/state` (đã bật). Bảng đầy đủ ở
[mqtt-topics.md](mqtt-topics.md). Bấm Ctrl+C để thoát.

### Bài 5. Hỏi thẳng Hub

**Mục tiêu.** Biết Dashboard lấy dữ liệu từ đâu.

**Làm.**

```bash
curl http://localhost:5002/api/state
curl http://localhost:5002/api/events
```

**Bạn sẽ thấy.** Lệnh đầu trả một khối JSON có `occupancy` (trạng thái hiện diện), `mode` (auto/manual),
`env`, `door`, `devices`, `online`. Lệnh sau trả danh sách sự kiện, chính là nội dung ô Nhật ký.

**Giải thích.** Dashboard không tự tính gì. Cứ 2 giây nó hỏi Hub hai câu trên rồi vẽ lại. Mọi quyết định
nằm trong Hub. Đây là nguyên tắc quan trọng nhất của thiết kế: **AI chỉ nhìn và báo cáo, Hub quyết định,
Dashboard chỉ hiển thị.** Lý do ở Mục 8.6 của [kien-thuc-nen-tang.md](kien-thuc-nen-tang.md).

Đến đây nên đọc Mục 1 đến 9 của tài liệu kiến thức nền tảng trước khi sang bậc 3.

---

## Bậc 3. Phần cứng thật

Ghép **từng thiết bị một**. Mỗi lần ghép một thiết bị thật thì tắt đúng phần mô phỏng tương ứng.

### Bài 6. Camera Tapo C222

**Mục tiêu.** Dashboard hiện hình camera và nhận ra người.

**Làm.**
1. Trong ứng dụng Tapo: bật RTSP, tạo "tài khoản camera" (khác tài khoản TP-Link). Xem IP camera trong app.
2. Tạo tệp `.env` ở thư mục gốc (tệp này không đưa lên Git vì có mật khẩu):
   ```
   AI_SOURCE=rtsp://<tài khoản camera>:<mật khẩu>@<IP camera>:554/stream2
   AI_MODELS="yolov8m-pose yolo26s-pose rtmo-m"
   ```
   Mật khẩu có ký tự `@` thì viết thành `%40`.
3. `docker compose up -d --build dashboard`
4. Mở http://localhost:5037/setup, bật công tắc **Camera AI**.
5. Về Dashboard, bấm **Vẽ lại vùng**: kéo vùng `sofa` trùm đúng chỗ nằm nghỉ, vùng `door` trùm lối ra vào,
   rồi **Lưu**. Có thể nhập số cạnh và bấm "Tạo đa giác" để có sẵn hình rồi kéo đỉnh.

**Bạn sẽ thấy.** Hình camera có khung xanh quanh người, kèm nhãn như `#1 sitting 0.89 [livingroom]`.
Ô Hiện diện ghi vùng và tư thế.

**Dạy tư thế (nên làm với camera gắn trần).** Camera soi từ trên xuống làm quy tắc mặc định hay lẫn ngồi với
nằm. Ở trang Cài đặt, mục "Dạy tư thế theo camera này": bấm "Ghi Nằm", trong 5 giây ra nằm đúng chỗ, giữ yên
đến khi ghi xong 8 ảnh; làm tương tự với "Ghi Ngồi". Xem lại ảnh, bấm ✕ bỏ ảnh sai. Đủ 2 tư thế, mỗi tư thế
5 mẫu là AI chuyển sang phân loại theo mẫu của chính bạn.

**Nếu không được.**
- Camera không lên hình: kiểm tra IP (đổi được ngay ở trang Cài đặt, ô "IP camera"), máy tính và camera phải
  cùng mạng Wi-Fi/LAN. Thử `ffplay -rtsp_transport tcp "rtsp://..."`.
- AI báo lỗi: dòng log cuối hiện ngay dưới công tắc.
- Chi tiết các lỗi đã gặp: [nhat-ky-camera-tapo.md](nhat-ky-camera-tapo.md).

### Bài 7. Ổ cắm Tapo P100

**Mục tiêu.** Bấm nút trên Dashboard, ổ cắm thật kêu "tách".

**Làm.**
1. Thêm ổ cắm vào ứng dụng Tapo như bình thường. Xem IP ổ cắm trong app.
2. Tắt "Mô phỏng ổ cắm" (ổ giả và ổ thật dùng chung tên `light`).
3. Trang Cài đặt, mục **Tapo P100**: nhập email TP-Link, mật khẩu, IP ổ cắm, bấm Đăng nhập.
4. Về Dashboard, chọn **Manual**, bấm Bật/Tắt ở hàng Ổ cắm.

**Bạn sẽ thấy.** Hàng Ổ cắm có chấm xanh; bấm nút thì ổ cắm đóng cắt trong khoảng 1 giây.

**Giải thích.** Hub lưu thông tin đăng nhập trên máy để tự kết nối lại sau khi khởi động lại. Sai mật khẩu
thì Hub không thử lại (tránh bị hãng khóa tài khoản); ổ cắm chưa lên mạng thì Hub thử lại mỗi 60 giây.

**Nếu không được.** "Hết thời gian chờ": sai IP hoặc khác mạng. "Sai email/mật khẩu": dùng đúng tài khoản
TP-Link đã thêm ổ cắm. Chi tiết: [tapo-p100.md](tapo-p100.md).

### Bài 8. Mạch ESP32 với cảm biến

**Mục tiêu.** Số nhiệt độ, ánh sáng, cửa trên Dashboard là số thật.

**Nối dây (ESP32 DevKit V1 30 chân).**

| Linh kiện | Nối |
|---|---|
| SHT30 (nhiệt độ, độ ẩm) | VCC → 3V3, GND → GND, SDA → GPIO21, SCL → GPIO22 |
| BH1750 GY-302 (ánh sáng) | chung bốn chân trên với SHT30 |
| MC-38 (cửa) | một dây → GPIO27, dây kia → GND |
| Buzzer 5V | GPIO25 → điện trở 1k → cực B transistor NPN; E → GND; buzzer (+) → VIN, (−) → cực C |

Không nối buzzer 5V thẳng vào chân GPIO: chân ESP32 chỉ ra 3,3 V và không đủ dòng.

**Làm.** Cần PlatformIO (tiện ích mở rộng của VS Code).
1. Chép `firmware/esp32/include/secrets.example.h` thành `secrets.h`, điền tên Wi-Fi, mật khẩu và
   `MQTT_HOST` là IP của máy chạy Docker (không phải `localhost`).
2. Trong thư mục `firmware/esp32`: `pio run -t upload`, rồi `pio device monitor`.
3. Tắt "Mô phỏng cảm biến (ESP32)" trên Dashboard.

**Bạn sẽ thấy.** Cửa sổ monitor in `MQTT đã nối` rồi mỗi 10 giây một dòng `env {...}`. Trên Dashboard,
che tay lên BH1750 thì lux giảm; tách nam châm khỏi MC-38 thì Cửa báo "Mở".

**Nếu không được.** Monitor báo "SHT30 không trả lời": xem lại dây SDA/SCL. Cửa báo ngược: đổi `DOOR_OPEN`
thành `LOW` trong `src/main.cpp`. Không thấy `MQTT đã nối`: mạch cần Internet để lấy giờ (NTP) trước khi gửi tin.

---

## Bậc 4. Đọc và sửa mã

Từ bài này cần cài thêm công cụ lập trình. Chạy `./run.sh` ở thư mục gốc: nó cài .NET 10, Python và các
môi trường ảo. Nên đọc Mục 10 đến 18 của tài liệu kiến thức nền tảng song song.

### Bài 9. Đọc mã theo một luồng: "tối thì bật đèn"

**Mục tiêu.** Lần theo một quyết định từ đầu đến cuối trong mã. Đừng đọc cả dự án một lúc.

Mở lần lượt bảy tệp sau, mỗi tệp chỉ tìm đúng chỗ được nêu:

| Bước | Việc xảy ra | Tệp |
|---|---|---|
| 1 | AI tìm người trong khung hình | `ai/smarthome_ai/pose.py`, hàm `detect` |
| 2 | AI xác định tư thế và vùng, gửi tin `state` | `ai/smarthome_ai/pipeline.py`, hàm `run` |
| 3 | Hub nhận tin và phân loại | `src/SmartHomeAI.Hub/Messages.cs`, `MqttParser.Parse` |
| 4 | Hub cập nhật "có người hay không" | `src/SmartHomeAI.Core/Occupancy/OccupancyEngine.cs` |
| 5 | Hub quyết định bật đèn | `src/SmartHomeAI.Core/Automation/AutomationEngine.cs` |
| 6 | Hub gửi lệnh, có chống dồn lệnh | `src/SmartHomeAI.Hub/MessageRouter.cs`, `CommandGate.cs` |
| 7 | Ổ cắm nhận lệnh | `src/SmartHomeAI.Hub/TapoP100Adapter.cs` |

Mục 18 của tài liệu kiến thức nền tảng mô tả cùng luồng này bằng lời, cho lệnh bật quạt.

### Bài 10. Chạy bộ kiểm thử

**Mục tiêu.** Biết cách kiểm tra mình có làm hỏng gì không.

```bash
dotnet test                                      # lõi và Hub (C#)
ai/.venv/bin/python -m pytest ai/tests           # AI
sim/.venv/bin/python -m pytest sim               # mô phỏng
sim/.venv/bin/python -m pytest tests/e2e -v      # đầu-cuối: Hub thật + kịch bản, vài phút
```

**Bạn sẽ thấy.** Mỗi lệnh kết thúc bằng dòng "passed", không có "failed".

**Giải thích.** Ba lệnh đầu thử từng phần riêng lẻ, chạy trong vài giây. Lệnh cuối chạy Hub thật với
Mosquitto thật, phát lần lượt các kịch bản SC01 đến SC11 và R01 đến R20 rồi so kết quả với kỳ vọng. Thời
gian trong kịch bản được nén lại để không phải chờ hàng giờ. Kết quả 20 kịch bản thực tế ghi ở
[kich-ban-thuc-te.md](kich-ban-thuc-te.md).

### Bài 11. Tự sửa một luật

**Mục tiêu.** Thay đổi hành vi hệ thống một cách an toàn. Đề bài: đổi ngưỡng "tối" từ 50 lux thành 80 lux.

1. Mở `src/SmartHomeAI.Core/Automation/AutomationEngine.cs`, tìm dòng `DarkLux { get; init; } = 50;` (ngưỡng lux coi là tối).
2. Sửa thành 80.
3. Chạy `dotnet test`. Sẽ có test đỏ, vì test đang kỳ vọng ngưỡng 50. Đọc tên test đỏ để hiểu nó kiểm gì.
4. Sửa test cho khớp ngưỡng mới, chạy lại đến khi xanh.
5. Chạy lại kịch bản SC01 và SC02 trên Dashboard để xem hành vi thật.

Quy tắc làm việc trong dự án, rút từ bài này:
- Mỗi kịch bản SC có một test tự động. Đổi luật thì đổi cả test.
- Muốn đổi nội dung một loại tin MQTT thì sửa [mqtt-topics.md](mqtt-topics.md) và tệp trong `schemas/` trước, mã sau.
- Ranh giới cố định: Python (AI) chỉ quan sát và gửi sự kiện; Hub (C#) giữ trạng thái và ra quyết định.
- Mỗi việc xong thì commit riêng, câu commit nói rõ đã làm gì.

---

## Điều cần cẩn thận (đọc lại trước khi bảo vệ)

- Một camera không biết có người ở phòng ngoài vùng quan sát. Chỉ kết luận "đi vắng" khi đủ chuỗi bằng chứng:
  cửa mở, người đi ra, hết người trong hình, qua thời gian xác minh. Thiếu bằng chứng thì ở trạng thái chưa biết.
- Camera mất kết nối không được suy ra nhà trống.
- Thiết bị không tự bật lại trái lệnh bấm tay.
- Các con số mặc định (50 lux, 28 °C, 30 giây, 2 phút, 30 phút) là giá trị tạm, phải chỉnh theo dữ liệu thật.
- Mô phỏng chạy qua chưa chứng minh hệ thống đúng ngoài đời.

## Tình trạng hiện tại (cập nhật 21/11/2026)

**Đã chạy thật.**
- Camera Tapo C222 qua RTSP, nhận người và khung xương bằng YOLOv8/YOLO26/RTMO (chọn ở trang Cài đặt).
- Ổ cắm Tapo P100 qua LAN, đăng nhập và điều khiển từ Dashboard.
- Toàn bộ hệ thống chạy bằng Docker; AI tự bật lại và Hub tự đăng nhập lại P100 sau khi khởi động lại.

**Đã có, kiểm bằng mô phỏng và test.** Occupancy, Automation, Alert, Hub (MQTT, API, ghi PostgreSQL),
Dashboard, kịch bản SC01–SC11 và R01–R20, bảo mật MQTT và khóa API (profile `secure`).

**Đã viết nhưng chưa thử đủ.**
- Firmware ESP32: biên dịch được, chưa nạp vào mạch thật.
- Dạy tư thế theo camera: mới thử trên 6 ảnh, chưa đo trên video có nhãn.
- Habit Engine (học thói quen): có và có test, chưa nối vào Hub và Dashboard.
- Script MATLAB: chưa chạy với dữ liệu thật.

**Chưa có.**
- Số đo độ chính xác nhận diện trên video thật có nhãn.
- Đăng nhập cho Dashboard: ai cùng mạng LAN cũng mở được.
- Ổ cắm thứ hai cho quạt: phần cứng hiện chỉ có một P100 (đèn).

## Phụ lục cho người triển khai

- **Chạy Hub ngoài Docker:** `dotnet run` dùng cổng 5002 trong `launchSettings.json`. Máy đã có PostgreSQL
  khác thì đặt `POSTGRES_PORT` cho compose và ghi đè `ConnectionStrings__Postgres`.
- **Chạy Dashboard ngoài Docker với camera:** đặt URL camera trong `appsettings.Local.json` (không commit):
  `{ "Ai": { "Source": "rtsp://..." } }`. Dừng container dashboard trước: `docker compose stop dashboard`.
- **Bật bảo mật MQTT:** chạy `infra/mosquitto/make-passwd.sh`, rồi
  `docker compose --profile secure up -d mosquitto-secure` (cổng 1884). Hub dùng `Mqtt__User`,
  `Mqtt__Password`, `Api__Key`; ngoài môi trường Development thiếu một trong ba thì Hub không khởi động.
- **Chạy 24/7 bằng systemd:** mẫu ở `infra/systemd/`, sửa đường dẫn và tài khoản cho đúng máy.
- **Thời gian trong Hub** (xác minh vắng, quá hạn dữ liệu, cảnh báo) cấu hình ở mục `Occupancy` và `Alerts`
  của `appsettings.json`.
