# Ôn tập bảo vệ đồ án Smart Home AI

Tài liệu cho sinh viên **chưa biết gì** về đề tài: đọc từ Phần 1 đến Phần 10, mỗi phần có (1) kiến thức nền, (2) từng bước làm, (3) bộ câu hỏi hội đồng kèm gợi ý trả lời, (4) mã nguồn liên quan.

Phần lý thuyết nền (MQTT là gì, HOG, máy trạng thái, rơ-le...) nằm ở `docs/kien-thuc-nen-tang.md`; nên đọc trước.

**Quy ước trung thực.** Hệ thống hiện chạy bằng **mô phỏng**; chưa có video thật, chưa ghép ESP32, ổ P100, camera C222 thật. Câu trả lời nào dựa vào phần chưa chạy thật được gắn nhãn **[CHƯA CHẠY THẬT]**, và mỗi phần có mục *Ghi chú chạy thật* liệt kê việc cần làm khi có phần cứng. Khi bị hỏi, nói thẳng phần nào đã đo, phần nào chưa; hội đồng đánh giá cao sự trung thực hơn một con số bịa.

**Cách tách tệp sau này.** Mỗi phần bắt đầu bằng dòng `<!-- PHẦN n: tên -->` và tiêu đề `## Phần n`. Tách theo các dấu đó thành `docs/on-tap/01-...md`, `02-...md`... và giữ phần Mục lục này làm `docs/on-tap/README.md`.

## Mục lục

| Phần | Nội dung | Cần chạy thật |
|---|---|---|
| 1 | Bài toán và kiến trúc tổng thể | Không |
| 2 | MQTT và IoT | Có: ESP32, P100 |
| 3 | Thị giác máy tính (camera, phát hiện, tư thế, vùng) | Có: video thật, RTSP |
| 4 | Theo dõi hiện diện (Occupancy) | Có: đo trên video thật |
| 5 | Tự động hóa và bảo vệ rơ-le | Có: P100 thật |
| 6 | Cảnh báo quên tắt điện | Có: thiết bị thật |
| 7 | Học thói quen | Có: dữ liệu nhiều tuần |
| 8 | Backend, dữ liệu, bảo mật | Một phần: PostgreSQL, systemd, 48 giờ |
| 9 | Kiểm thử và đánh giá (SC, R, MATLAB) | Một phần: MATLAB, dữ liệu thật |
| 10 | Câu hỏi hóc búa và hạn chế | Có |

Từ viết tắt dùng chung: **Hub** = máy chủ trung tâm ASP.NET Core; **AI** = chương trình Python đọc camera; **SC01–SC12** = kịch bản chức năng; **R01–R20** = kịch bản thực tế; **Occupancy** = trạng thái "có người trong phòng hay không".

---

<!-- PHẦN 1: Bài toán và kiến trúc tổng thể -->
## Phần 1. Bài toán và kiến trúc tổng thể

### 1.1 Kiến thức nền
Nhà thông minh thường chỉ có "hẹn giờ" hoặc "bấm điện thoại". Đề tài làm hệ thống **tự quyết**: biết có người hay không, người đang đứng, ngồi hay nằm ở đâu, rồi tự bật đèn (tối), bật quạt (nóng và đang nằm sofa), và cảnh báo khi cả nhà đi vắng mà đèn quạt vẫn bật.

Chuỗi xử lý: **Camera → AI Python → (MQTT) → Hub → (MQTT) → ổ cắm đèn, quạt**; thêm ESP32 đo nhiệt độ, ánh sáng, cửa.
- Camera Tapo C222 phát luồng hình qua RTSP.
- AI Python phát hiện người, chia vùng, **chỉ quan sát và báo sự kiện**.
- Hub (C#) giữ trạng thái, ra quyết định, ghi PostgreSQL.
- Dashboard (Blazor) hiển thị và điều khiển.
- MATLAB phân tích dữ liệu đã ghi.

Nguyên tắc thiết kế quan trọng: **AI chỉ quan sát, Hub mới quyết định** (vì trạng thái ở một chỗ, test được bằng C# thuần, và mô phỏng thay camera bằng cách gửi sự kiện giả).

### 1.2 Từng bước làm (tổng thể)
1. Viết hợp đồng dữ liệu (`docs/mqtt-topics.md`, `docs/schemas/`) trước khi viết mã.
2. Viết mô phỏng (`sim/`) giả ESP32, ổ P100, kịch bản.
3. Viết lõi thuần (`src/SmartHomeAI.Core`): Occupancy, Automation, Alert, Habit, kèm test.
4. Viết Hub nối MQTT, API, PostgreSQL.
5. Viết AI camera (`ai/`).
6. Viết Dashboard, kiểm thử đầu-cuối, MATLAB.
7. Ghép phần cứng thật, đo, chỉnh.

### 1.3 Câu hỏi hội đồng

**Q1. Đề tài giải quyết vấn đề gì mà ổ cắm hẹn giờ không làm được?**
Hẹn giờ không biết có người. Hệ thống dùng hiện diện và hành vi (ngồi, nằm), nên đèn chỉ bật khi có người và tối, quạt chỉ bật khi nóng và đang nằm sofa, và cảnh báo khi đi vắng quên tắt.

**Q2. Vẽ sơ đồ khối và nói vai trò từng khối.**
Camera → AI (quan sát) → MQTT → Hub (quyết định, lưu) → MQTT → P100 (đèn, quạt) và ESP32 (buzzer); ESP32 gửi nhiệt, ẩm, lux, cửa lên Hub; Dashboard gọi API Hub; MATLAB đọc CSV xuất từ PostgreSQL.

**Q3. Vì sao tách AI (Python) và Hub (C#) thay vì viết chung?**
Python có OpenCV và mô hình AI; C# phù hợp dịch vụ chạy 24/7 và test chặt. Tách bằng MQTT cho phép thay camera bằng mô phỏng, và chạy test logic mà không cần camera.

**Q4. Vì sao AI "chỉ quan sát" và không tự kết luận có người hay không?**
Kết luận cần ghép nhiều nguồn: camera, cảm biến cửa, thời gian xác minh. Nếu AI quyết, trạng thái nằm ở hai nơi, khó test. Hub giữ trạng thái ở một chỗ.

**Q5. Vì sao chọn MQTT, không dùng HTTP trực tiếp giữa các khối?**
MQTT nhẹ, nhiều nguồn một chiều, có retained và LWT để biết nguồn chết, chạy tốt trên ESP32. HTTP phải biết địa chỉ từng nơi và không có LWT sẵn.

**Q6. Vì sao dùng .NET cho Hub?**
Hệ sinh thái mạnh cho dịch vụ nền, kiểu tĩnh bắt lỗi sớm, `TimeProvider` giúp test thời gian, Blazor dùng chung ngôn ngữ cho Dashboard. [Đây là lựa chọn thiết kế; câu trả lời nên nhấn lý do test được.]

**Q7. Phạm vi tối thiểu của đồ án là gì và phần nào là phần thêm?**
Tối thiểu là SC01–SC11 chạy bằng mô phỏng và test đầu-cuối. Học thói quen sâu (SC12), MATLAB, Dashboard nâng cao là phần thêm.

**Q8. Hệ thống chạy trên phần cứng nào, có cần GPU không?**
Linux server 16 GB RAM, không GPU rời. AI chạy CPU (3–5 khung hình/giây là đủ). Đã chọn bộ phát hiện chạy CPU: HOG (không thêm phụ thuộc) và YOLOv8n-pose bản ONNX chạy bằng onnxruntime. **[CHƯA CHẠY THẬT]**: chưa đo tải CPU/RAM khi chạy với RTSP thật.

**Q9. Dữ liệu có rời khỏi nhà không? Quyền riêng tư?**
Toàn bộ chạy cục bộ: hình camera chỉ đi từ camera vào máy chủ trong nhà, Hub nghe `127.0.0.1` ở cấu hình Production, không gửi lên đám mây. Hệ thống không lưu hình, chỉ lưu sự kiện (vùng, tư thế).

**Q10. Thế nào là "tự chủ"? Có cần Internet không?**
Mọi quyết định chạy cục bộ nên mất Internet vẫn chạy (kịch bản thử mất Internet nằm trong NV10). Riêng việc điều khiển ổ P100 qua LAN phụ thuộc giao thức hãng. **[CHƯA CHẠY THẬT]**.

**Q11. Ranh giới trách nhiệm giữa các thành phần được ghi ở đâu?**
`docs/mqtt-topics.md` mục "Quyết định ranh giới" và schema JSON trong `docs/schemas/`. Muốn đổi payload thì sửa tài liệu và schema trước, mã sau.

**Q12. 10 nhiệm vụ NV01–NV10 là gì, chia ra sao?**
NV01 AI camera, NV02 Occupancy, NV03 Habit, NV04 Automation, NV05 IoT, NV06 Alert, NV07 Dashboard, NV08 PostgreSQL và API, NV09 MATLAB, NV10 kiểm thử và triển khai (`docs/nhiem-vu.md`).

**Q13. Thứ tự làm có lý do gì?**
Làm lõi thuần và mô phỏng trước vì không cần phần cứng; ghi log dữ liệu sớm vì SC12 và MATLAB cần nhiều tuần dữ liệu; AI camera là việc rủi ro nhất nên quay video thật sớm.

**Q14. Điểm mới hoặc đóng góp của đề tài là gì?**
Ghép bằng chứng nhiều nguồn để kết luận "đi vắng" an toàn (cửa, hướng đi, hết người, thời gian xác minh), bảo vệ rơ-le, và học ngưỡng từ lần bật tay có người duyệt. Không nên nói "chính xác hơn X%" khi chưa đo.

**Q15. Vì sao ưu tiên "không báo sai" hơn "không bỏ sót"?**
Báo vắng sai sẽ làm người dùng mất tin tưởng và có thể tắt thiết bị khi đang có người; bỏ sót chỉ làm quên tắt điện thêm một lúc.

**Q16. Hệ thống có thể mở rộng thêm thiết bị hay phòng khác không?**
Hợp đồng MQTT dùng `{id}` và `{node}` nên thêm thiết bị là thêm topic. Nhưng hiện mới có phòng khách, hai thiết bị (đèn, quạt), nên mở rộng chưa làm.

### 1.4 Mã liên quan
`docs/mqtt-topics.md`, `docs/nhiem-vu.md`, `docs/huong-dan-thuc-hien.md`, `src/SmartHomeAI.Core/`, `src/SmartHomeAI.Hub/`, `ai/`, `sim/`.

### 1.5 Ghi chú chạy thật
Không phần cứng riêng; số đo CPU/RAM khi chạy toàn bộ cần RTSP thật (xem Phần 3 và 8).

---

<!-- PHẦN 2: MQTT và IoT -->
## Phần 2. MQTT và IoT

### 2.1 Kiến thức nền
**MQTT** là giao thức nhắn tin kiểu đăng ký (publish/subscribe) qua một **broker** (ở đây là Mosquitto). Mỗi tin có **topic** (đường dẫn như `home/device/fan/set`) và **payload** (nội dung JSON). Bên nào quan tâm thì đăng ký topic.
- **QoS 1**: tin được giao ít nhất một lần (có thể trùng), dùng cho mọi topic của đề tài.
- **Retained**: broker giữ tin cuối cùng của topic, người đến sau nhận ngay (dùng cho trạng thái thiết bị, trạng thái cửa).
- **LWT (Last Will and Testament)**: tin "offline" do broker tự phát khi nguồn chết đột ngột. Mỗi nguồn gửi `status online` retained và đặt LWT `offline` cùng topic.

**ESP32** là vi điều khiển có Wi-Fi, đọc cảm biến: SHT30 (nhiệt độ, độ ẩm), BH1750 (ánh sáng lux), MC-38 (công tắc từ báo cửa mở), và điều khiển buzzer. **Tapo P100** là ổ cắm thông minh điều khiển đèn và quạt trần.

### 2.2 Từng bước làm
1. Chạy `docker compose up -d` để có Mosquitto và PostgreSQL.
2. Xem tin: `mosquitto_sub -t 'home/#' -v`.
3. Chạy mô phỏng: `python -m sim run SC07 --speed 20` và quan sát tin.
4. Hub kết nối, đăng ký `home/#` QoS 1, tự nối lại khi mất kết nối (vòng lặp mỗi giây trong `MqttService`).
5. Bảo mật: `mosquitto.secure.conf` với `allow_anonymous false`, tệp mật khẩu và ACL; chạy `docker compose --profile secure up -d mosquitto-secure` (cổng 1884).
6. Ghép phần cứng từng thiết bị một, thay đúng phần mô phỏng tương ứng.

### 2.3 Câu hỏi hội đồng

**Q1. MQTT khác HTTP thế nào, vì sao hợp IoT?**
MQTT giữ một kết nối lâu dài, tiêu đề nhỏ, mô hình publish/subscribe tách người gửi khỏi người nhận, có QoS, retained, LWT. HTTP là yêu cầu/đáp ứng, phải hỏi liên tục (polling).

**Q2. Giải thích QoS 0, 1, 2. Vì sao chọn QoS 1?**
QoS 0 giao nhiều nhất một lần (có thể mất); QoS 1 ít nhất một lần (có thể trùng); QoS 2 đúng một lần nhưng chậm hơn. Chọn QoS 1 vì mất tin nguy hiểm hơn trùng tin; trùng được xử lý ở Hub bằng bỏ lệnh trùng và chống dồn lệnh (Phần 5).

**Q3. Retained dùng cho gì trong dự án?**
Cho `status`, trạng thái thiết bị và cửa: Hub khởi động lại vẫn biết trạng thái cuối. Có rủi ro: trạng thái retained cũ có thể bị hiểu nhầm là thay đổi mới, nên Hub có `StartupGrace` 30 giây và cửa sổ đồng bộ để không coi đó là lệnh tay.

**Q4. LWT là gì, hệ thống dùng thế nào?**
Khi một nguồn chết đột ngột, broker phát tin `offline`. Hub coi nguồn `offline` hoặc quá hạn là mất dữ liệu; nếu AI mất thì Occupancy về `Unknown` (SC10) và không suy ra nhà trống.

**Q5. Liệt kê các topic chính, ai gửi ai nhận.**
`home/sensor/{node}/env|door|status` (ESP32 → Hub), `home/sensor/{node}/buzzer/set` (Hub → ESP32), `home/device/{id}/set` (Hub → P100), `home/device/{id}/state` (P100 → Hub), `home/ai/livingroom/state|transition|status` (AI → Hub), `home/alert/{id}` và `/cancel` (Hub phát). Chi tiết ở bảng trong `docs/mqtt-topics.md`.

**Q6. Vì sao payload là JSON và có schema?**
JSON dễ đọc, dễ debug bằng `mosquitto_sub`. Schema trong `docs/schemas/` ép các thành phần thống nhất; Hub bỏ tin sai định dạng (`MqttParser`) và ghi cảnh báo thay vì sập.

**Q7. Trường `ts` để làm gì, múi giờ nào?**
Mốc thời gian ISO 8601 UTC. Hub dùng nó để biết tin có quá hạn không và để ghi log đúng giờ.

**Q8. Nếu broker chết thì sao?**
Các thành phần tự nối lại. Hub bỏ tin gửi đi khi chưa kết nối và ghi cảnh báo (`MqttBus`). Lệnh bị mất không được hứa là giao; nên dựa vào trạng thái thiết bị báo lại. Kịch bản thử mất MQTT thuộc NV10. **[CHƯA CHẠY THẬT]**

**Q9. Làm sao biết thiết bị đã thực sự bật sau khi gửi lệnh?**
Hub gửi lệnh bật rồi chờ `device/{id}/state` báo `on`. Nếu chưa, Automation gửi lại theo lịch (sau 60 giây, rồi 120 giây); hết lịch thì ghi "không phản hồi" (`DeviceUnresponsive`).

**Q10. Cảm biến cửa MC-38 hoạt động thế nào và vai trò gì?**
Là công tắc từ: nam châm tách ra thì mạch hở, báo "cửa mở". Một sự kiện cửa mở được coi là **bằng chứng rời nhà** trong Occupancy, nhưng chưa đủ để kết luận vắng.

**Q11. Vì sao dùng ESP32 mà không dùng Raspberry Pi hay Arduino?**
ESP32 có Wi-Fi sẵn, rẻ, tiêu thụ thấp, đủ cho đọc cảm biến và gửi MQTT. Raspberry Pi thừa sức nhưng đắt và nặng cho việc này. [Lựa chọn thiết kế.]

**Q12. Hub điều khiển P100 bằng cách nào?**
Qua một adapter nhận `home/device/{id}/set` và trả `state`, giống `sim/p100.py`. Nguy cơ: hãng có thể đổi giao thức; phương án dự phòng là dùng relay qua ESP32. **[CHƯA CHẠY THẬT]**: chưa thử với P100 thật.

**Q13. Bảo mật MQTT làm thế nào?**
Production bắt buộc tài khoản (Hub không khởi động nếu thiếu `Mqtt:User`/`Mqtt:Password`), broker dùng `allow_anonymous false` và ACL: chỉ Hub ghi `home/#`, `ai` chỉ ghi `home/ai/#`, `sensor` ghi `home/sensor/#`, `device` ghi `home/device/#`. Chưa có TLS (mã hóa đường truyền): nên nói thẳng đây là hạn chế trong mạng LAN tin cậy.

**Q14. Nếu ai đó trong mạng LAN gửi lệnh giả thì sao?**
Với cấu hình dev (ẩn danh), được. Với `mosquitto.secure.conf` thì bị từ chối do thiếu mật khẩu, và các tài khoản `ai`, `sensor` không có quyền ghi `home/device/#` (chỉ `hub` và `device` có). **[CHƯA CHẠY THẬT]**: chưa thử với Hub và ESP32 thật; firmware ESP32 cần hỗ trợ đăng nhập MQTT.

**Q15. Tần suất gửi tin là bao nhiêu, vì sao?**
`env` 10 giây (nhiệt độ đổi chậm), `ai/state` 1–2 Hz (đủ cho hiện diện), `door` khi đổi và retained. Gửi dày hơn chỉ tốn băng thông và ghi log.

**Q16. Làm sao kiểm tra các tin đúng định dạng khi ESP32 thật gửi lên?**
Xem bằng `mosquitto_sub`, đối chiếu schema; sim và test pytest kiểm schema (`sim/tests`). Với phần cứng thật, chạy kịch bản thật rồi đối chiếu và cập nhật sim bằng dữ liệu đã ghi.

### 2.4 Mã liên quan
`infra/mosquitto/`, `src/SmartHomeAI.Hub/MqttService.cs`, `MqttBus.cs`, `Messages.cs` (parser), `docs/schemas/`, `sim/esp32.py`, `sim/p100.py`.

### 2.5 Ghi chú chạy thật
- Ghép ESP32 (SHT30, BH1750, MC-38), firmware có LWT và mật khẩu MQTT.
- Thử P100 qua LAN sớm; nếu hỏng dùng relay.
- Thử `mosquitto_pub` ẩn danh bị từ chối trên broker `secure`.

---

<!-- PHẦN 3: Thị giác máy tính -->
## Phần 3. Thị giác máy tính (camera, phát hiện, tư thế, vùng)

### 3.1 Kiến thức nền
- **RTSP**: giao thức phát luồng video của camera IP. Camera Tapo C222 bật RTSP trong ứng dụng và đọc bằng OpenCV (`cv2.VideoCapture`).
- **Phát hiện người (detection)**: tìm hình chữ nhật (hộp) bao quanh mỗi người trong khung hình. Dự án có hai bộ:
  - **HOG + SVM** của OpenCV: mô tả hình dáng bằng "biểu đồ hướng gradient" rồi bộ phân loại tuyến tính SVM quyết định có phải người. Nhẹ, không cần tải mô hình, nhưng kém khi người nằm. Hiện là bản mặc định.
  - **YOLOv8n-pose (ONNX)** chạy bằng onnxruntime trên CPU: ngoài hộp còn cho 17 khớp xương (chuẩn COCO-17: mũi, mắt, vai, khuỷu, cổ tay, hông, gối, cổ chân).
- **Theo dõi (tracking)**: gán cùng một `track_id` cho cùng một người giữa các khung. Dự án ghép theo **IoU** (tỷ lệ giao trên hợp của hai hộp) rồi theo khoảng cách tâm; mất sau 8 khung.
- **Tư thế (posture)**: `standing`, `sitting`, `lying`, `moving`, `unknown`, suy ra bằng **quy tắc hình học**, không phải mô hình học.
- **Vùng (zone)**: đa giác chuẩn hóa 0–1 trên khung hình: `livingroom`, `sofa`, `door`, `other_exit`. Điểm nằm trong đa giác hay không được xác định bằng thuật toán **ray casting** (đếm số lần tia cắt cạnh).
- **Chuyển vùng (transition)**: khi một người đổi vùng và ổn định đủ số khung thì phát sự kiện kèm hướng `in`/`out`.

### 3.2 Từng bước làm
1. Cài phụ thuộc, chạy thử trên video ghi sẵn trước, RTSP thật sau: `python -m smarthome_ai --source video.mp4 --detector hog`.
2. Vẽ vùng bằng đa giác (tệp `config/zones.json`, hoặc chỉnh qua `PUT /zones` của máy chủ hình trong AI). Thứ tự vùng là thứ tự ưu tiên, `livingroom` đứng cuối làm dự phòng.
3. Chạy vòng xử lý (`pipeline.run`) ở 4 khung hình/giây:
   detect → track → tính tốc độ → phân loại tư thế → **bỏ phiếu số đông 8 khung** làm mượt → xác định vùng → phát `transition` nếu vùng mới ổn định 6 khung → phát `state` mỗi giây.
4. Khi luồng mất: phát `camera_ok=false` và thử mở lại mỗi 2 giây.
5. Quay video thật (ban ngày, ban đêm, nằm sofa, ra vào cửa, hai người) và đo.

### 3.3 Câu hỏi hội đồng

**Q1. Phát hiện người hoạt động thế nào ở mức khái quát?**
Mô hình nhận ảnh và trả về các hộp kèm độ tin cậy (confidence). Với HOG: cửa sổ trượt quét ảnh, mỗi cửa sổ tính đặc trưng HOG rồi SVM chấm điểm. Với YOLO: mạng nơ-ron tích chập dự đoán hộp và khớp xương trực tiếp từ ảnh.

**Q2. Vì sao chọn HOG làm bản mặc định chứ không phải YOLO?**
HOG không thêm phụ thuộc nặng (thư viện ultralytics kéo theo torch cỡ GB) và chạy CPU. Nó chỉ là mốc so sánh (baseline). Bản pose ONNX đã có sẵn làm tùy chọn `--detector pose`. Việc chọn bộ nào chính thức cần **đo trên video thật**. **[CHƯA CHẠY THẬT]**

**Q3. Điểm yếu của HOG là gì?**
Huấn luyện chủ yếu trên người đứng, nên kém khi người nằm hoặc ngồi co; dễ nhầm nền phức tạp. Đây chính là lý do "nằm sofa" là bài khó nhất.

**Q4. Chuẩn COCO-17 là gì?**
Bộ 17 điểm khớp phổ biến: mũi, 2 mắt, 2 tai, 2 vai, 2 khuỷu, 2 cổ tay, 2 hông, 2 gối, 2 cổ chân. Chỉ số 5,6 là vai, 11,12 là hông, 13,14 là gối; `posture.py` dùng các chỉ số này.

**Q5. Cách phân loại tư thế bằng quy tắc hình học?**
Nếu có khớp xương: so vị trí vai và hông; thân nằm ngang (độ lệch ngang lớn hơn dọc) hoặc đầu-vai-hông cùng độ cao thì `lying`; hông gần mức đầu gối thì `sitting`; còn lại `standing`. Nếu không có khớp: dùng tỉ lệ rộng/cao của hộp (trên 1,1 là nằm, dưới 0,55 là đứng, ở giữa là ngồi). Nếu tâm dịch chuyển nhanh (hơn 0,5 chiều cao hộp mỗi giây) thì `moving`.

**Q6. Các ngưỡng 1,1, 0,55, 0,5 lấy ở đâu ra?**
Là giá trị **đoán ban đầu**, ghi rõ trong mã là baseline và cần đo, chỉnh bằng video thật. Không nên nói chúng đã tối ưu. **[CHƯA CHẠY THẬT]**

**Q7. Vì sao phải làm mượt tư thế, làm bằng cách nào?**
Mỗi khung có nhiễu: tư thế nhảy lung tung sẽ làm quạt bật tắt sai. Dự án **bỏ phiếu số đông** trong 8 khung gần nhất; chưa đủ 4 khung thì trả `unknown`; hòa phiếu thì giữ tư thế đang phát.

**Q8. IoU là gì? Tính thế nào?**
Diện tích giao của hai hộp chia diện tích hợp. Bằng 1 là trùng khít, 0 là rời nhau. Tracker ghép track với detection có IoU lớn nhất, tối thiểu 0,2; không đạt thì xét khoảng cách tâm dưới 0,15.

**Q9. Khi hai người đi qua nhau, tracker có đổi nhầm `track_id` không?**
Có thể, vì tracker rất đơn giản (không có đặc trưng ngoại hình, không có bộ lọc Kalman). Hệ thống giảm tác động nhờ Occupancy dựa nhiều vào "có người trong vùng phòng" hơn là danh tính. Đây là hạn chế cần nói rõ.

**Q10. Vì sao tư thế nằm/ngồi dùng tâm hộp còn đứng dùng điểm chân để xác định vùng?**
Người nằm trên sofa có chân chạm đáy khung nên điểm chân trượt khỏi vùng sofa; dùng tâm hộp ổn định hơn. Người đứng thì điểm chân là chỗ chạm sàn nên chính xác hơn (`Zones.zone_for`).

**Q11. Ray casting hoạt động thế nào?**
Từ điểm cần xét kẻ tia nằm ngang; đếm số cạnh đa giác bị cắt. Lẻ là bên trong, chẵn là bên ngoài (`_inside` trong `zones.py`).

**Q12. Hướng `in`/`out` suy ra thế nào?**
Từ cặp vùng: đi từ vùng phòng (`livingroom`, `sofa`) sang vùng thoát (`door`, `other_exit`) là `out`; ngược lại là `in`; còn lại `unknown`. AI chỉ quan sát; **ý nghĩa** (ra cửa chính hay sang phòng khác) do Hub quyết.

**Q13. Vì sao phải ổn định 6 khung mới phát `transition`?**
Để lọc nhiễu: một người đứng sát ranh giới vùng sẽ nhảy qua lại; chỉ khi vùng mới giữ liên tục đủ khung mới coi là đã chuyển.

**Q14. Một camera có biết người đi sang phòng khác không?**
Không. Nó chỉ thấy phòng khách. Vì vậy hệ thống không kết luận "đi vắng" chỉ vì hết người trong hình; cần chuỗi bằng chứng (Phần 4). Đây là hạn chế nền tảng của một camera.

**Q15. Tốc độ xử lý bao nhiêu, có đủ real-time không?**
Cấu hình 4 khung/giây cho detect, phát `state` 1 lần/giây; người trong nhà di chuyển chậm nên đủ. Chưa đo thật. **[CHƯA CHẠY THẬT]**: chưa đo tải CPU/RAM và độ trễ khi chạy RTSP thật.

**Q16. Ban đêm, camera hồng ngoại (IR), có nhận diện được không?**
Chưa biết. Kịch bản R04 mô phỏng nhiễu tư thế do IR, nhưng đó là dữ liệu giả; độ chính xác thật cần quay video ban đêm. **[CHƯA CHẠY THẬT]**

**Q17. Độ chính xác nhận diện nằm sofa là bao nhiêu?**
Chưa có số. Phải nói thẳng: chưa đo vì chưa có video thật; kế hoạch là quay video, đo tỷ lệ dương tính giả và âm tính giả, rồi quyết có đổi HOG sang pose không. Cho đến khi có số, tính năng tự tắt (`AutoOffAllowed`) để rỗng. **[CHƯA CHẠY THẬT]**

**Q18. Khi camera mất kết nối, hệ thống làm gì?**
Pipeline phát `camera_ok=false`, thử mở lại mỗi 2 giây. Hub coi đó là mất dữ liệu: Occupancy `Unknown`, không suy ra nhà trống (SC10, R12).

**Q19. Nhiều người trong khung thì sao?**
Mỗi người một track; `state` chứa danh sách `persons`. Occupancy chỉ cần có ai trong vùng phòng thì `Occupied`. Quạt xét "có người nằm trên sofa". R09 mô phỏng khách đến và một người ra về.

**Q20. Tại sao làm sẵn máy chủ hình (MJPEG) trong AI?**
Để Dashboard hoặc người chỉnh vùng xem khung hình có lớp phủ (hộp, xương, vùng) và chỉnh vùng trực tiếp, thay vì đoán tọa độ mù.

### 3.4 Mã liên quan
`ai/smarthome_ai/detector.py`, `pose.py`, `posture.py`, `tracker.py`, `zones.py`, `transitions.py`, `pipeline.py`, `source.py`, `stream.py`, `__main__.py`.

### 3.5 Ghi chú chạy thật
- Quay video thật đúng góc camera dự kiến: ban ngày, ban đêm, nằm sofa, ra vào cửa, hai người.
- Bật RTSP trên Tapo C222, tạo tài khoản camera, thử đọc bằng OpenCV.
- Đo: độ chính xác nằm sofa, tỷ lệ báo vắng sai, CPU/RAM, độ trễ. Chỉnh ngưỡng tư thế bằng số đo.

---

<!-- PHẦN 4: Theo dõi hiện diện (Occupancy) -->
## Phần 4. Theo dõi hiện diện (Occupancy)

### 4.1 Kiến thức nền
**Máy trạng thái hữu hạn** (finite state machine): hệ thống luôn ở đúng một trạng thái trong tập hữu hạn, và chuyển trạng thái theo sự kiện và thời gian. Occupancy có 5 trạng thái:

| Trạng thái | Ý nghĩa |
|---|---|
| `Occupied` | Có người trong vùng phòng (`livingroom` hoặc `sofa`) |
| `OtherArea` | Có người trong hình nhưng ở vùng khác, hoặc không thấy ai nhưng chưa có bằng chứng ra khỏi nhà |
| `ExitPending` | Có bằng chứng ra khỏi nhà, hết người, đang chờ xác minh |
| `AwayConfirmed` | Đã xác minh đi vắng (hết người liên tục đủ `VerifyAway` = 30 giây sau bằng chứng) |
| `Unknown` | Mất dữ liệu (AI offline, camera lỗi, tin quá hạn) |

Hai nguồn **bằng chứng rời nhà**: (a) AI báo `transition` hướng `out` sang vùng `door`; (b) cảm biến cửa MC-38 báo mở. Sang `other_exit` **không** phải bằng chứng (người đó vẫn ở trong nhà, SC05).

Nguyên tắc: **không suy ra "vắng" từ dữ liệu thiếu**. Lớp trạng thái này là phần lõi thuần C#, không phụ thuộc đồng hồ thật (dùng `TimeProvider`, test được bằng đồng hồ giả).

### 4.2 Từng bước làm
1. Định nghĩa kiểu: `OccupancyState`, `Person`, `AiState`, `AiTransition`, `DoorEvent`, `OccupancyOptions` (`OccupancyTypes.cs`).
2. Viết `OccupancyEngine` với các hàm đầu vào: `OnAiStatus`, `OnAiState`, `OnTransition`, `OnDoor`; trạng thái được **tính lại mỗi lần đọc** (`State => Evaluate()`).
3. Viết test cho từng kịch bản SC05, SC06, SC07, SC08, SC10, SC11 và các nhiễu N03, N04 (`tests/SmartHomeAI.Tests`).
4. Cấu hình thời gian ở mục `Occupancy` của `appsettings.json`.
5. Chạy kịch bản mô phỏng đầu-cuối với Hub thật (`tests/e2e`).

### 4.3 Câu hỏi hội đồng

**Q1. Vì sao dùng máy trạng thái chứ không dùng `if` rời rạc?**
Số trạng thái và điều kiện chuyển rõ ràng, test từng đường chuyển, tránh trạng thái mâu thuẫn. Có thể vẽ lại thành sơ đồ trạng thái.

**Q2. Giải thích từng trạng thái và khi nào chuyển.**
Xem bảng ở 4.1. Ví dụ: có người trong vùng phòng thì `Occupied` ngay; có bằng chứng ra cửa và hết người thì `ExitPending`, sau 30 giây liên tục thì `AwayConfirmed`; AI offline hoặc không có tin quá 10 giây thì `Unknown`.

**Q3. Vì sao cần cả `OtherArea` lẫn `AwayConfirmed`?**
Vì một camera không thấy hết nhà. Hết người trong hình có thể là sang phòng khác. Chỉ khi có bằng chứng rời nhà (cửa chính) và qua thời gian xác minh mới kết luận đi vắng.

**Q4. SC05, SC06, SC08 là gì?**
SC05: người đi sang phòng khác (`other_exit`) thì **không** kết luận vắng. SC06: người ra cửa rồi quay lại thì hủy bằng chứng. SC08: còn người khác trong hình thì bằng chứng rời của một người bị hủy (hai người, một người ra).

**Q5. SC10 và nguyên tắc `Unknown`?**
Khi AI offline, camera lỗi hoặc dữ liệu quá hạn, trạng thái là `Unknown`, đồng thời bộ đếm xác minh bị đặt lại. Không bao giờ suy ra "vắng" từ thiếu dữ liệu. Đây là an toàn quan trọng nhất.

**Q6. `EmptyGrace` (10 giây) giải quyết vấn đề gì (N03)?**
Camera hay mất người 1–2 khung khi người ngồi yên. Nếu đổi trạng thái ngay thì đèn quạt nhảy. `EmptyGrace` giữ `Occupied` trong 10 giây hết người liên tục; riêng khi đã có bằng chứng rời thì bỏ qua thời gian chờ này.

**Q7. `AwayResume` (60 giây) giải quyết vấn đề gì (N04)?**
Sau khi đã `AwayConfirmed`, dữ liệu mất ngắn rồi trở lại, mà vẫn không có người và bằng chứng còn: giữ `AwayConfirmed` thay vì quay lại `ExitPending` (tránh bộ đếm cảnh báo bị reset oan).

**Q8. Vì sao 30 giây xác minh?**
Là giá trị **tạm**, cân bằng giữa phản ứng nhanh và tránh báo oan khi người chỉ ra cửa lấy hàng. Phải chỉnh theo dữ liệu thật và ghi lý do vào luận văn. **[CHƯA CHẠY THẬT]**

**Q9. Kết hợp camera với cảm biến cửa có lợi gì?**
Hai nguồn độc lập: cửa mở là bằng chứng không phụ thuộc AI; AI cho biết có còn người trong hình không. Thiếu một trong hai thì không kết luận.

**Q10. Điều gì xảy ra nếu AI nhìn thấy người ngoài vùng phòng (ví dụ vùng `door`)?**
Có người trong hình nhưng không trong `RoomZones`, nên trạng thái là `OtherArea`, không phải `Occupied` và không phải vắng.

**Q11. Vì sao tính trạng thái "mỗi lần đọc" mà không lưu trạng thái?**
Vì trạng thái phụ thuộc thời gian trôi (hết hạn dữ liệu, hết thời gian xác minh) nên phải tính theo đồng hồ hiện tại; lưu sẽ lỗi thời khi không có tin mới đến.

**Q12. `TimeProvider` có lợi gì khi kiểm thử?**
Test dùng đồng hồ giả, tua thời gian tức thì thay vì chờ 30 giây thật, kết quả lặp lại được. Kịch bản e2e nén thời gian bằng cách thu ngắn cấu hình.

**Q13. Hệ thống xử lý hai người thế nào?**
Chỉ cần ai đó còn trong vùng phòng là `Occupied`. Khi một người ra cửa mà người kia còn trong hình thì bằng chứng bị hủy (SC08, R09). R10 kiểm tra cả nhà ra cách nhau 10 giây thì vẫn kết luận `AwayConfirmed`.

**Q14. Còn trường hợp nào có thể báo vắng oan?**
Có. Mã ghi nhận: nếu người sang `other_exit` bị khuất hoàn toàn khỏi khung (`persons` rỗng) thì sau thời gian xác minh có thể thành `AwayConfirmed` dù chỉ vào nhà vệ sinh. R05 giữ người nhìn thấy nên không lộ. Cần xác định AI thật có giữ track ở `other_exit` không. **[CHƯA CHẠY THẬT]**

**Q15. R06 (nhận hàng 1 phút) có báo oan không?**
Không với cấu hình mô phỏng, vì cảnh báo đầu sau 2 phút. Nhưng vắng 1 phút đã đủ `AwayConfirmed` (xác minh 30 giây); nhận hàng quá khoảng 2,5 phút sẽ có cảnh báo oan. Đây là hạn chế đã ghi trong `docs/kich-ban-thuc-te.md`.

**Q16. Đo độ chính xác Occupancy thế nào?**
`matlab/c_occupancy_eval.m`: so `occupancy_log` với nhãn thật mỗi phút, ra accuracy, ma trận nhầm lẫn, tỷ lệ báo vắng sai và bỏ sót, phút `Unknown` đếm riêng. Chỉ chạy được khi có nhãn thật. **[CHƯA CHẠY THẬT]**

**Q17. Vì sao các hàm đầu vào tách `OnAiState`, `OnTransition`, `OnDoor`?**
Mỗi nguồn tin một hàm, dễ test từng nguồn và dễ thay nguồn (camera thật hay mô phỏng).

### 4.4 Mã liên quan
`src/SmartHomeAI.Core/Occupancy/OccupancyEngine.cs`, `OccupancyTypes.cs`; test `tests/SmartHomeAI.Tests/`; mô phỏng `sim/scenarios.py`; e2e `tests/e2e/test_e2e.py`.

### 4.5 Ghi chú chạy thật
- Quay video có nhãn thật để chạy `c_occupancy_eval.m`.
- Chỉnh `VerifyAway`, `EmptyGrace`, `StateStale` theo dữ liệu.
- Xác định AI thật có giữ track ở vùng `other_exit` không.

---

<!-- PHẦN 5: Tự động hóa và bảo vệ rơ-le -->
## Phần 5. Tự động hóa và bảo vệ rơ-le

### 5.1 Kiến thức nền
**Automation Engine** quyết định khi nào bật đèn, quạt. Quy tắc (chỉ **bật**, không tắt; tắt không phải việc của nó):
- **Đèn**: `Occupied` và lux dưới `DarkLux` (50) thì bật.
- **Quạt**: `Occupied`, có người trên **sofa**, tư thế **nằm** liên tục tối thiểu 5 giây, và nhiệt độ từ 28 °C trở lên thì bật. Nếu nhiệt độ thấp hơn nhưng luật thói quen đã duyệt cho phép thì vẫn bật (Phần 7).
- **Lệnh thủ công thắng lệnh tự động** (SC09): sau lệnh tay, thiết bị đó bị chặn tự động trong 30 phút (`ManualBlock`).

**Rơ-le** là công tắc cơ khí trong ổ cắm; bật tắt dồn dập làm hỏng nhanh. Hệ thống có nhiều lớp bảo vệ:
1. **Cooldown** 60 giây giữa hai lệnh tự động (`MinInterval`).
2. **MinHold** 10 giây giữ trạng thái giữa hai lần đổi tự động (`MinHoldOn`/`MinHoldOff`).
3. **CommandGate** ở Hub: khoảng cách tối thiểu 3 giây giữa hai lệnh tới cùng thiết bị (`MinCommandInterval`); lệnh thừa được **gộp**, chỉ gửi lệnh cuối; lệnh trùng trạng thái đã báo thì bỏ.
4. **Giới hạn mỗi giờ**: tối đa 6 lần đổi tự động/giờ; vượt thì **khóa tự động** 1 giờ (`AutoLocked`), lệnh tay mở khóa.
5. **Gửi lại có lịch** khi thiết bị chưa báo bật: chờ 60 rồi 120 giây, hết thì báo "không phản hồi".
6. **StartupGrace** 30 giây sau khởi động: không tự động, chờ thiết bị báo trạng thái.

Lệnh có ba nguồn: `auto`, `manual`, `alert`. Lệnh `manual` và `alert` không bị MinHold.

### 5.2 Từng bước làm
1. Viết `AutomationEngine` thuần: `Evaluate(input)` trả danh sách lệnh; `Diagnose` giải thích **vì sao** thiết bị bật hay không (mã `DiagnosisCode`, dùng cho Dashboard).
2. Nối vào Hub: `MessageRouter` gọi engine rồi gửi qua `CommandGate`, sau đó publish `home/device/{id}/set`.
3. Phát hiện lệnh tay: thiết bị đổi trạng thái mà không khớp lệnh Hub trong 10 giây thì coi là lệnh tay (`OnDeviceState`).
4. Test SC01–SC05, SC09 và bộ chống nhiễu, bật tắt dồn dập, bất biến rơ-le (`CommandGateTests`, `DebounceWiringTests`, `tests/e2e/test_safety_invariants.py`).
5. Bọc toàn bộ đường vào của `MessageRouter` bằng một khóa chung (`_flow`) vì bốn luồng cùng gọi (MQTT, Tick, HTTP).

### 5.3 Câu hỏi hội đồng

**Q1. Liệt kê luật tự động.**
Đèn: có người và tối. Quạt: có người nằm trên sofa, nóng (hoặc thói quen đã duyệt). Lệnh tay luôn thắng.

**Q2. Vì sao tự động chỉ bật mà không tắt?**
Tắt khi sai gây khó chịu hoặc nguy hiểm hơn (đang xem TV bị tắt đèn). Việc tắt giao cho người dùng hoặc cảnh báo; tự tắt khi đi vắng là tùy chọn, mặc định **tắt**. Đây là khoảng trống trải nghiệm đã ghi ở `docs/kich-ban-thuc-te.md` (mưa làm mát vẫn chạy quạt).

**Q3. Rơ-le là gì và vì sao phải bảo vệ?**
Công tắc cơ khí; mỗi lần đóng ngắt tạo hồ quang và mài mòn. Bật tắt nhanh nhiều lần làm giảm tuổi thọ, và dòng đột biến với quạt có thể gây hỏng.

**Q4. Kể các cơ chế chống bật tắt dồn dập.**
Xem 5.1: cooldown 60 giây, MinHold 10 giây, khoảng cách 3 giây ở CommandGate kèm gộp lệnh, tối đa 6 lần/giờ rồi khóa, gửi lại theo lịch, StartupGrace.

**Q5. `CommandGate` gộp lệnh như thế nào?**
Nếu lệnh đến khi chưa đủ khoảng cách tối thiểu kể từ lệnh trước, nó lưu thành lệnh chờ (`Pending`); lệnh sau ghi đè lệnh trước, đếm số lệnh đã gộp; đến hạn chỉ gửi lệnh cuối. Nếu trạng thái thiết bị đã báo giống lệnh thì bỏ như lệnh trùng.

**Q6. Làm sao hệ thống biết người dùng vừa bật tay?**
Thiết bị báo đổi trạng thái mà Hub không có lệnh tương ứng trong 10 giây (`CommandMatchWindow`) thì coi là thủ công (nút bấm hoặc ứng dụng hãng). Bỏ qua 5 giây sau khi thiết bị online lại (đồng bộ) và 30 giây đầu sau khởi động.

**Q7. Sau lệnh tay, bao lâu hệ thống không tự động lại?**
30 phút (`ManualBlock`); thời điểm lệnh tay được lưu (`ManualStore`) nên khởi động lại không mất. R13 (tắt quạt tay) và R14 (bật đèn tay ban ngày) kiểm tra điều này.

**Q8. Nếu P100 mất điện rồi về lại ở trạng thái tắt thì sao (R17)?**
Hub bật lại quạt 1 lần, không coi là lệnh tay, không dồn lệnh. Nhược điểm: nếu người dùng chủ ý cắt nguồn thì Hub tự bật lại. Đã ghi ở "Phát hiện 4".

**Q9. Nếu thiết bị không phản hồi thì sao?**
Gửi lại sau 60 giây, rồi sau 120 giây; sau đó ghi `DeviceUnresponsive` và không gửi nữa cho đến khi thiết bị báo trạng thái.

**Q10. `AutoLocked` là gì?**
Lưới an toàn: quá 6 lần tự động đổi trạng thái trong 1 giờ thì khóa tự động 1 giờ, tránh vòng lặp lỗi làm hỏng rơ-le. Lệnh thủ công mở khóa.

**Q11. Vì sao khởi động lại Hub không bật ngay quạt (R18)?**
`StartupGrace` 30 giây: chờ thiết bị báo trạng thái trước, để không bật chồng khi thiết bị vốn đã bật.

**Q12. `Diagnose` để làm gì?**
Cho biết lý do mỗi thiết bị bật hoặc không (ví dụ `FanTooCool`, `BlockedByManual`, `Cooldown`), không có tác dụng phụ. Dashboard dùng để giải thích với người dùng.

**Q13. Lỗi tranh chấp luồng trong Hub là gì và sửa thế nào?**
`MessageRouter` bị gọi đồng thời từ luồng nhận MQTT, vòng Tick mỗi giây và các yêu cầu HTTP; `CommandGate` dùng `Dictionary` không an toàn luồng nên có thể hỏng hoặc gửi lệnh trùng. Đã sửa bằng một khóa chung `_flow` bọc `Handle`, `Tick`, `TryPublishManual`, `Feedback`, kèm test chạy song song. Việc gửi MQTT không chờ mạng đồng bộ nên đặt trong khóa được.

**Q14. Vì sao dùng một khóa chung thay vì hàng đợi (Channel)?**
Tải của một căn nhà rất thấp, khóa chung đơn giản và đủ đúng; hàng đợi buộc đổi HTTP sang bất đồng bộ và chờ kết quả, nhiều mã hơn mà lợi ích không đáng kể.

**Q15. R15 (nhiệt dao động quanh 28 °C) kiểm gì?**
Nhiệt độ lên xuống quanh ngưỡng không được làm quạt bật tắt liên tục. Kết quả: quạt bật tối đa 1 lần, không tắt, nhờ cooldown và MinHold.

**Q16. Quạt bật khi nằm sofa nhưng người chỉ chợp mắt vài giây thì sao?**
Phải nằm liên tục tối thiểu 5 giây (`SofaLyingMin`); và tư thế đã được bỏ phiếu số đông để lọc nhiễu. Ngưỡng 5 giây là giá trị tạm.

**Q17. Ngưỡng 28 °C và 50 lux lấy ở đâu?**
Là giá trị tạm, phải chỉnh theo dữ liệu thật; luật thích nghi ở Phần 7 học ngưỡng từ lần bật tay của người dùng. **[CHƯA CHẠY THẬT]**

### 5.4 Mã liên quan
`src/SmartHomeAI.Core/Automation/AutomationEngine.cs`, `src/SmartHomeAI.Hub/CommandGate.cs`, `MessageRouter.cs`, `ManualStore.cs`, `tests/SmartHomeAI.Hub.Tests/CommandGateTests.cs`, `DebounceWiringTests.cs`, `tests/e2e/test_safety_invariants.py`, `test_burst.py`.

### 5.5 Ghi chú chạy thật
- Thử với ổ P100 thật: độ trễ từ cảm biến đến ổ cắm, rơ-le không đóng ngắt dồn dập.
- Chạy 48 giờ liên tục; thử mất mạng, mất DB không làm treo Hub.
- Chỉnh các ngưỡng thời gian theo hành vi thật.

---

<!-- PHẦN 6: Cảnh báo quên tắt điện -->
## Phần 6. Cảnh báo quên tắt điện

### 6.1 Kiến thức nền
Khi hệ thống đã **xác minh đi vắng** (`AwayConfirmed`) mà đèn hoặc quạt vẫn bật, Alert Manager phát cảnh báo theo mốc thời gian đếm từ lúc `AwayConfirmed`:

| Mốc | Việc |
|---|---|
| 2 phút (`FirstAfter`) | Cảnh báo đầu (`level: first`), mở buzzer |
| Mỗi 5 phút (`RemindEvery`) | Nhắc lại (`level: reminder`), **cùng một mã cảnh báo** |
| 10 phút (`AutoOffAfter`) | Tự tắt thiết bị, **chỉ khi** bật cấu hình `AutoOffEnabled` và thiết bị nằm trong danh sách cho phép `AutoOffAllowed` (mặc định tắt và rỗng) |

Một "đợt vắng" có đúng một mã cảnh báo (`alert-{tiền tố}-N`). Cảnh báo bị **hủy** với lý do: `person_returned` (người về), `devices_off` (đèn quạt đã tắt), `data_lost` (mất dữ liệu nhận diện). Người dùng có thể **phản hồi** đúng/sai cho từng cảnh báo (`POST /api/alerts/{id}/feedback`); dữ liệu này dùng để đánh giá về sau.

Nếu dữ liệu mất ngắn (dưới `UnknownFreeze` = 60 giây) sau khi đã vắng, bộ đếm **đóng băng** thay vì hủy, rồi tiếp tục.

### 6.2 Từng bước làm
1. Viết `AlertManager` thuần: `Evaluate(input)` trả `RaiseAlert`, `CancelAlert`, `AutoOff`.
2. Hub gọi sau mỗi tin và mỗi giây (`RunAlerts`), publish `home/alert/{id}` và buzzer `home/sensor/esp1/buzzer/set`.
3. Ghi lịch sử (lúc báo, lúc hủy, lý do, phản hồi) vào bộ nhớ và PostgreSQL (`alert_log`).
4. Test SC07 (quên tắt khi vắng) và SC11 (mất dữ liệu), R07, R08, R10–R12, R20.
5. Mã cảnh báo có tiền tố theo giờ khởi động (`AlertOptions.IdPrefix`) để không trùng sau khi Hub khởi động lại.

### 6.3 Câu hỏi hội đồng

**Q1. Cảnh báo hoạt động thế nào từ đầu đến cuối?**
Hết người, có bằng chứng ra cửa, qua 30 giây xác minh thì `AwayConfirmed`; nếu đèn hoặc quạt còn bật, sau 2 phút phát cảnh báo đầu và bật buzzer; nhắc lại mỗi 5 phút; hủy khi người về hoặc thiết bị tắt.

**Q2. Vì sao cảnh báo đầu sau 2 phút mà không báo ngay?**
Để tránh báo oan khi người chỉ ra cửa vài phút (nhận hàng, đổ rác). Con số 2 phút là giá trị tạm; chỉnh theo dữ liệu thật. **[CHƯA CHẠY THẬT]**

**Q3. Vì sao nhắc lại dùng cùng một mã?**
Một đợt vắng là một sự việc; người dùng phản hồi đúng/sai theo sự việc, không theo từng lần nhắc (R07, R08 kiểm tra).

**Q4. Tự tắt có bật mặc định không? Vì sao?**
Không. Tự tắt khi nhận diện sai (ví dụ báo vắng sai khi vẫn có người) gây nguy hiểm và mất tin cậy. Chỉ bật khi đã đo độ chính xác Occupancy và người dùng liệt kê thiết bị cho phép tự tắt. **[CHƯA CHẠY THẬT]**

**Q5. Điều gì làm hủy cảnh báo?**
Người quay lại (`person_returned`); đèn quạt đã tắt hết (`devices_off`); mất dữ liệu nhận diện quá 60 giây (`data_lost`). Hủy `devices_off` đặt `_done` để cùng đợt vắng không báo lại.

**Q6. Mất dữ liệu ngắn thì cảnh báo ra sao (SC11)?**
Dưới 60 giây: đóng băng bộ đếm, giữ cảnh báo, rồi cộng bù khoảng mất để tiếp tục. Từ 60 giây: hủy với `data_lost` và không báo thêm, vì không đủ bằng chứng.

**Q7. Dữ liệu phản hồi đúng/sai dùng để làm gì?**
Làm nhãn để tính tỷ lệ cảnh báo đúng và sai, chỉnh các mốc thời gian. Hiện mới lưu; việc dùng để tối ưu chưa làm tự động.

**Q8. Buzzer hoạt động thế nào?**
Mỗi lần phát cảnh báo Hub gửi `buzzer/set on:true` tới ESP32; khi hủy gửi `on:false`.

**Q9. R10 (cả nhà ra cách nhau 10 giây) cho kết quả gì?**
Vẫn `AwayConfirmed` và có cảnh báo `first`, nghĩa là bằng chứng của người ra sau không bị hủy bởi người ra trước.

**Q10. R06 (nhận hàng) và cảnh báo oan?**
Nhận hàng 1 phút không báo vì chưa tới 2 phút; quá khoảng 2,5 phút sẽ có cảnh báo oan. Đây là hạn chế đã biết (Phát hiện 3).

**Q11. R11, R12: AI offline hoặc camera lỗi khi đang xem TV thì sao?**
Occupancy `Unknown`, không `AwayConfirmed`, không cảnh báo, không tắt quạt: đúng nguyên tắc không suy ra vắng từ thiếu dữ liệu.

**Q12. Người dùng có thể tắt cảnh báo không?**
Cảnh báo tự hủy khi tắt đèn quạt hoặc người quay lại. Không có tùy chọn "tắt riêng cảnh báo" trong mã mà tôi đã kiểm; nếu hội đồng hỏi, nói thẳng đây là việc chưa làm và cần kiểm lại Dashboard.

**Q13. Vì sao mã cảnh báo thêm tiền tố giờ?**
Trước đây mã đếm lại từ 1 sau khi Hub khởi động lại, nên bản ghi trong PostgreSQL có thể trùng mã, cập nhật nhầm. Tiền tố `yyMMddHHmmss` giữ mã duy nhất mà không đổi cách đếm trong một lần chạy.

**Q14. Có điểm yếu nào còn lại?**
Cảnh báo chỉ là buzzer và thông điệp MQTT/Dashboard; chưa có thông báo đẩy ra điện thoại. Buzzer chỉ nghe được khi ở gần. Chưa đo trên phần cứng thật. **[CHƯA CHẠY THẬT]**

**Q15. Vì sao "đèn không tắt khi người rời" mà chỉ nhắc?**
Theo luật hiện hành hệ thống chỉ bật; tắt là quyết định của người hoặc tự tắt tùy chọn. Sang phòng khác (R05) đèn cũng giữ. Là khoảng trống trải nghiệm cần chủ đề tài quyết.

**Q16. Kiểm thử cảnh báo thế nào, thời gian có nén không?**
Test đơn vị dùng đồng hồ giả; e2e nén thời gian (FirstAfter 5 giây, RemindEvery 6 giây, VerifyAway 5 giây) nên con số mô phỏng chỉ kiểm logic, không phải thời gian thật.

### 6.4 Mã liên quan
`src/SmartHomeAI.Core/Alerts/AlertManager.cs`, `AlertTypes.cs`; `src/SmartHomeAI.Hub/MessageRouter.cs` (`RunAlerts`), `EventLog.cs`; `tests/SmartHomeAI.Tests/AlertManagerTests.cs`; `tests/e2e/`.

### 6.5 Ghi chú chạy thật
- Thử buzzer trên ESP32 thật; đo độ trễ từ lúc `AwayConfirmed` đến khi buzzer kêu.
- Thu phản hồi đúng/sai nhiều tuần để chỉnh mốc 2 phút, 5 phút.

---

<!-- PHẦN 7: Học thói quen -->
## Phần 7. Học thói quen

### 7.1 Kiến thức nền
Mục tiêu (NV03, SC12): thay vì dùng ngưỡng cố định (28 °C, 50 lux), **học ngưỡng từ chính hành vi người dùng**. Mỗi lần người dùng **bật tay** quạt hoặc đèn, hệ thống ghi một **quan sát** (thời gian, thiết bị, nhiệt độ, lux, tư thế, vùng). Khi đủ dữ liệu, đề xuất một **luật**: "bật quạt khi nhiệt độ từ X trở lên", "bật đèn khi lux dưới Y".

Thuật toán (`HabitEngine`):
- Chỉ lấy lần **bật tay** (không lấy lệnh tự động) trong 30 ngày gần nhất, tối đa 30 mẫu gần nhất.
- Quạt: chỉ lấy lần bật khi người **nằm trên sofa**.
- Cần **ít nhất 5 mẫu**; nếu các mẫu quá phân tán (nhiệt độ lệch hơn 3 °C, lux lệch hơn 40) thì **không** đề xuất, vì chưa có "thói quen" rõ.
- Ngưỡng đề xuất = **tứ phân vị dưới** (quartile thứ nhất) của mẫu: người dùng thường hành động ở đầu thấp của mức họ chịu được.
- Luật có ba trạng thái: `Proposed` (đề xuất), `Approved` (đã duyệt), `Disabled` (tắt). **Chỉ luật `Approved` được dùng.** Người dùng có thể duyệt, sửa ngưỡng, tắt từng luật. Luật mới duyệt thay thế luật cũ cùng thiết bị.
- Luật không thay đổi nếu ngưỡng mới chênh dưới dung sai (0,5 °C hoặc 5 lux) so với luật đã biết hoặc đã bị từ chối (không đề xuất lại thứ người dùng đã từ chối).

Tích hợp: `HabitEngine` cài đặt `IHabitAdvisor`; `AutomationEngine` hỏi `ShouldTurnOnFan` khi nhiệt độ thấp hơn ngưỡng cố định.

### 7.2 Từng bước làm
1. Ghi mọi lần bật tay vào `device_command_log` kèm nhiệt độ, lux, tư thế (bước ghi log từ đầu).
2. Cài `HabitEngine`: `Record`, `Approve`, `Disable`, `Edit`, `ShouldTurnOnFan`.
3. Test SC12 trong `tests/SmartHomeAI.Tests`.
4. Nối vào Hub và Dashboard (duyệt, sửa, tắt luật; lưu DB). **Chưa làm.**
5. Dùng MATLAB (`b_threshold.m`) so luật cố định với luật thích nghi trên dữ liệu thật.

### 7.3 Câu hỏi hội đồng

**Q1. "Học" ở đây là gì? Có phải học máy không?**
Là **học thống kê đơn giản** (ước lượng ngưỡng bằng tứ phân vị từ mẫu), không phải mạng nơ-ron. Đủ cho một ngưỡng và dễ giải thích; có người duyệt nên an toàn.

**Q2. Vì sao dùng tứ phân vị dưới mà không dùng trung bình hay trung vị?**
Người dùng bật quạt khi bắt đầu thấy nóng, tức là đầu thấp của dải nhiệt độ họ chấp nhận; dùng trung vị hay trung bình sẽ bật trễ hơn so với thói quen. Lưu ý: script MATLAB `b_threshold.m` ước lượng bằng **trung vị**, là cách tính độc lập dùng để phân tích; hai cách khác nhau và nên nói rõ khi so sánh.

**Q3. Vì sao cần tối thiểu 5 mẫu?**
Ít mẫu thì ngưỡng không đáng tin; 5 là giá trị tạm, chỉnh được (`MinSupport`).

**Q4. Vì sao bỏ qua nếu mẫu phân tán rộng?**
Nghĩa là người dùng không có thói quen rõ (bật quạt lúc 26 °C, lúc 31 °C); đề xuất ngưỡng trung bình sẽ sai cả hai.

**Q5. Vì sao chỉ học từ lần bật tay?**
Lệnh tự động phản ánh luật của hệ thống, không phải ý muốn người dùng; học từ nó sẽ tự củng cố chính nó (vòng phản hồi).

**Q6. Vì sao cần người duyệt luật?**
Để tránh học sai và để người dùng kiểm soát; luật chưa duyệt không ảnh hưởng thiết bị. Đây là nguyên tắc an toàn và tin cậy.

**Q7. Luật học được đưa vào Automation thế nào?**
Qua `IHabitAdvisor.ShouldTurnOnFan`: khi người nằm sofa và nhiệt độ từ ngưỡng đã duyệt trở lên thì quạt bật dù thấp hơn 28 °C. Các điều kiện an toàn khác (cooldown, ManualBlock...) vẫn áp dụng.

**Q8. Phân biệt luật cố định và luật thích nghi bằng số đo nào?**
`b_threshold.m` đo (chỉ lúc có người ở nhà): số phút luật lệch hành vi người dùng, và số lần bật/tắt sai. **[CHƯA CHẠY THẬT]**: mới thử trên dữ liệu giả; cần vài tuần dữ liệu thật.

**Q9. Hệ thống có "quên" thói quen cũ không?**
Có cửa sổ 30 ngày và 30 mẫu gần nhất; thói quen đổi (mùa hè sang đông) thì đề xuất mới xuất hiện và có thể thay luật cũ sau khi duyệt.

**Q10. SC12 "đổi thói quen" kiểm gì?**
Sau khi đủ dữ liệu mới, đề xuất được cập nhật (ngưỡng mới) mà không đè luật đã duyệt cho tới khi người dùng duyệt luật mới.

**Q11. Hiện đã nối vào Hub chưa?**
Chưa. `HabitEngine` đã có và có test, nhưng chưa nối Hub và Dashboard (không có giao diện duyệt, chưa lưu DB). Nên nói thẳng đây là phần đang dang dở.

**Q12. Có thể học thêm thói quen khác (giờ giấc, đèn tắt)?**
Hiện chỉ có hai luật: quạt theo nhiệt độ và đèn theo lux (chỉ bật). Học theo giờ hoặc luật tắt là hướng phát triển, chưa làm.

**Q13. Dữ liệu nhiều tuần thu thế nào?**
Hub ghi log PostgreSQL liên tục (cảm biến, lệnh, hiện diện); xuất CSV bằng `tools/export_csv.py`. Vì vậy cần bật ghi log sớm.

**Q14. Rủi ro riêng tư khi học thói quen?**
Dữ liệu hành vi sinh hoạt nhạy cảm (khi nào có nhà). Dữ liệu chỉ ở máy chủ trong nhà; Production không mở API ra ngoài (nghe `127.0.0.1`). API đọc hiện chưa cần khóa nên cần giới hạn mạng.

**Q15. Có đánh giá được luật học đúng hay sai không?**
Dựa trên phản hồi người dùng (số lần họ tắt tay ngay sau lệnh tự động) và `b_threshold.m`. Chưa có số thật. **[CHƯA CHẠY THẬT]**

### 7.4 Mã liên quan
`src/SmartHomeAI.Core/Habits/HabitEngine.cs`; `matlab/b_threshold.m`; `tools/export_csv.py`; test SC12.

### 7.5 Ghi chú chạy thật
- Cần vài tuần dữ liệu bật tay thật để có luật đề xuất đáng tin.
- Nối `HabitEngine` vào Hub và Dashboard trước khi thử thật.

---

<!-- PHẦN 8: Backend, dữ liệu, bảo mật -->
## Phần 8. Backend, dữ liệu, bảo mật

### 8.1 Kiến thức nền
**Hub** là ứng dụng ASP.NET Core (Minimal API) gồm:
- `MqttService`: kết nối broker, nhận tin, vòng `Tick` mỗi giây.
- `MessageRouter`: nhận tin, cập nhật trạng thái, chạy Automation và Alert, gửi lệnh.
- `HubState`: trạng thái tổng hợp, lịch sử, nhật ký quyết định.
- API: `GET /health`, `/api/state`, `/api/history`, `/api/events`, `/api/alerts`; ghi: `POST /api/mode`, `/api/device/{id}`, `/api/alerts/{id}/feedback`.
- `PgEventLog`: ghi PostgreSQL qua **hàng đợi** chạy nền để xử lý MQTT không phải chờ cơ sở dữ liệu.

**Bảo mật:**
- Các API ghi cần header `X-Api-Key`; so sánh khóa bằng băm SHA-256 và hàm so sánh **hằng thời gian** (`FixedTimeEquals`) để chống tấn công đo thời gian.
- Ngoài môi trường Development, thiếu `Api:Key`, `Mqtt:User` hoặc `Mqtt:Password` thì Hub **không khởi động**.
- Production nghe `127.0.0.1:5080`; mật khẩu DB không nằm trong tệp cấu hình chung mà qua biến môi trường (`/etc/smarthome/hub.env`).
- Mosquitto bản `secure`: mật khẩu và ACL theo vai trò.

**Dữ liệu:** PostgreSQL các bảng `sensor_reading`, `door_event`, `device_command_log`, `ai_event`, `occupancy_log`, `alert_log`. Hàng đợi tối đa 10.000 bản ghi; đầy hoặc DB hỏng thì **bỏ bản ghi** và đếm (`dbDropped` trên `/health`).

### 8.2 Từng bước làm
1. Dựng Hub, đăng ký các dịch vụ trong `Program.cs`.
2. Viết `MessageRouter` và nối các engine lõi.
3. Viết `PgEventLog` với hàng đợi và tạo bảng từ các tệp `sql/*.sql` khi khởi động.
4. Bảo vệ API ghi bằng `ApiKey.Filter`; kiểm cấu hình bắt buộc ở Production.
5. Triển khai bằng systemd: `smarthome-hub.service` (đọc `EnvironmentFile`, nghe `127.0.0.1`), `smarthome-ai.service`.
6. Thử lỗi: mất MQTT, mất DB, khởi động lại server; xem `dbDropped`.

### 8.3 Câu hỏi hội đồng

**Q1. Vì sao ghi PostgreSQL bằng hàng đợi nền?**
Để xử lý MQTT không bị chặn khi DB chậm hoặc hỏng; nếu ghi đồng bộ, một lần DB treo sẽ làm Hub bỏ lỡ tin và có thể dồn lệnh. Đánh đổi: có thể mất log.

**Q2. Mất log có chấp nhận được không?**
Với an toàn: có, vì quyết định không phụ thuộc DB. Với nghiên cứu: không lý tưởng, nên có bộ đếm `dbDropped` trên `/health` và cảnh báo ghi nhật ký (bản ghi đầu tiên và mỗi 100 bản ghi). Nếu cần lossless thì phải ghi đệm ra đĩa; đã chọn không làm vì tăng độ phức tạp.

**Q3. API key được so sánh như thế nào, vì sao?**
Băm cả hai vế bằng SHA-256 rồi so bằng `CryptographicOperations.FixedTimeEquals`. Hằng thời gian để không lộ vị trí ký tự sai; băm để hai vế cùng độ dài và không lộ độ dài khóa.

**Q4. Vì sao Production không khởi động khi thiếu khóa?**
Trước đây thiếu khóa là mở: mọi người trong mạng gửi lệnh được. Đổi sang "đóng mặc định": nếu quên cấu hình thì hệ thống lỗi to chứ không chạy hở âm thầm.

**Q5. Các API đọc có cần khóa không?**
Chưa. `/api/state`, `/api/history`... không cần khóa, và lộ thông tin "nhà đang vắng". Giảm rủi ro bằng cách Hub chỉ nghe `127.0.0.1` ở Production; cần khóa API đọc khi mở ra ngoài. Đã hoãn, nên nói rõ.

**Q6. Dashboard có đăng nhập không?**
Chưa. Dashboard tự gắn khóa API khi gọi Hub, nên ai mở được Dashboard thì điều khiển được thiết bị. Chấp nhận khi Dashboard chỉ truy cập trong mạng nội bộ; thêm đăng nhập nếu mở ra ngoài.

**Q7. Mật khẩu DB được quản lý thế nào?**
Bản dev nằm ở `appsettings.Development.json` (chỉ dùng cục bộ); Production đặt qua biến `ConnectionStrings__Postgres` trong `/etc/smarthome/hub.env`, không nằm trong kho mã.

**Q8. Tranh chấp luồng là gì, hệ thống bị ở đâu?**
Xem Phần 5 Q13: ba nguồn (MQTT, Tick, HTTP) cùng gọi `MessageRouter`. Đã sửa bằng khóa chung và có test song song 4 luồng, mỗi luồng 2000 lần.

**Q9. Vì sao `lock` là đủ, có gây nghẽn không?**
Mỗi lần xử lý rất ngắn (tính toán trong bộ nhớ, gửi MQTT không chờ mạng), tải một căn nhà thấp, nên không nghẽn.

**Q10. Hub khởi động lại thì mất gì?**
Trạng thái trong bộ nhớ (lịch sử, nhật ký quyết định). Thời điểm lệnh tay được lưu tệp (`ManualStore`) nên vẫn giữ. Cảnh báo đang mở mất; sau khởi động lại lấy lại từ tin MQTT retained và `StartupGrace`.

**Q11. Vì sao dùng PostgreSQL mà không dùng SQLite hay tệp?**
Khối lượng dữ liệu vài tuần đến vài tháng, truy vấn theo thời gian, xuất cho MATLAB; PostgreSQL có kiểu thời gian và JSONB (dùng cho `ai_event`). SQLite cũng đủ; đây là lựa chọn thiết kế. [Nói thẳng đây là cân nhắc, không phải bắt buộc.]

**Q12. API `/health` để làm gì?**
Cho giám sát (systemd, người vận hành) biết Hub sống và có bao nhiêu bản ghi log bị bỏ.

**Q13. Triển khai 24/7 bằng cách nào, nếu Hub chết?**
systemd với `Restart=always`, `RestartSec=5`. **[CHƯA CHẠY THẬT]**: systemd chưa chạy thử thật; mục tiêu thử 48 giờ liên tục.

**Q14. Cảnh báo lỗ hổng NU1903 trong build là gì?**
Cảnh báo NuGet về gói có lỗ hổng đã công bố (Dashboard WebAssembly Server 10.0.10, Microsoft.OpenApi 2.0.0 ở Hub và test). Chưa nâng cấp; phải xử lý trước khi triển khai (nằm trong NV10).

**Q15. Hệ thống xử lý tin MQTT sai định dạng thế nào?**
`MqttParser` ném lỗi định dạng; Hub ghi cảnh báo và bỏ tin đó, không sập; trạng thái giữ nguyên.

**Q16. Điều gì xảy ra nếu hai lệnh `POST /api/device` đến cùng lúc?**
Vào `MessageRouter` qua khóa chung nên tuần tự; lệnh thứ hai bị `CommandGate` gộp hoặc giữ chờ nếu quá gần lệnh trước.

**Q17. Phân quyền người dùng có chưa?**
Chưa. Chỉ có một khóa API chung; không có tài khoản người dùng, vai trò. NV08 nêu phân quyền, hiện chưa làm.

### 8.4 Mã liên quan
`src/SmartHomeAI.Hub/Program.cs`, `ApiKey.cs`, `MessageRouter.cs`, `CommandGate.cs`, `EventLog.cs`, `HubState.cs`, `MqttService.cs`; `infra/systemd/`, `infra/mosquitto/`; `docker-compose.yml`.

### 8.5 Ghi chú chạy thật
- Chạy systemd 48 giờ; thử khởi động lại server, mất DB, mất MQTT.
- Chạy MQTT bảo mật với Hub và ESP32 thật.
- Thêm đăng nhập Dashboard và khóa cho API đọc nếu mở ra ngoài nhà.
- Nâng gói dính NU1903.

---

<!-- PHẦN 9: Kiểm thử và đánh giá -->
## Phần 9. Kiểm thử và đánh giá (SC, R, MATLAB)

### 9.1 Kiến thức nền
Có bốn tầng kiểm thử:
1. **Test đơn vị lõi** (`tests/SmartHomeAI.Tests`, 72 test): Occupancy, Automation, Alert, Habit chạy với **đồng hồ giả**, không cần mạng.
2. **Test Hub** (`tests/SmartHomeAI.Hub.Tests`, 70 test): bộ định tuyến, `CommandGate`, chống dồn lệnh, bất biến rơ-le, khóa API, kiểm cấu hình Production, tranh chấp luồng.
3. **Mô phỏng và test đầu-cuối** (`sim/`, `tests/e2e`): chạy Hub thật với broker thật, mô phỏng phát tin như ESP32, P100 và camera theo **kịch bản**.
   - **SC01–SC11**: kịch bản chức năng (vào phòng tối, nằm sofa nóng, đi vắng quên tắt, mất camera...). Khoảng 3 phút.
   - **R01–R20**: kịch bản thực tế (trời mưa, mất điện, Hub khởi động lại, trẻ chạy nhảy, ngày dài...). Lần chạy 15/11/2026: 20/20 đạt, 525 giây.
   - **Nhiễu (N)** và **bất biến an toàn**: bắn lệnh dồn dập, kiểm khoảng cách tối thiểu giữa hai lệnh.
4. **Phân tích MATLAB** trên dữ liệu thực nghiệm (xem Q9–Q15 bên dưới).

**Điều cần hiểu đúng:** mô phỏng chạy qua chỉ chứng minh logic **đúng với đầu vào giả**, chưa chứng minh hệ thống đúng ngoài đời.

### 9.2 Từng bước làm
1. Mỗi kịch bản SC có một test tự động; viết test trước, sửa mã cho qua.
2. Chạy: `dotnet test` (lõi và Hub), `sim/.venv/bin/python -m pytest sim`, rồi e2e theo `tests/e2e/README.md`.
3. Xem kịch bản: `python -m sim list`; chạy một kịch bản: `python -m sim run SC07 --speed 20`.
4. Kịch bản thực tế nén thời gian (speed 40, cấu hình VerifyAway 5 giây...) nên kết quả chỉ kiểm logic.
5. Xuất CSV, tạo nhãn thật, chạy MATLAB (xem Q9–Q15).

### 9.3 Câu hỏi hội đồng

**Q1. Bạn kiểm thử hệ thống như thế nào?**
Bốn tầng ở 9.1: đơn vị, Hub, đầu-cuối với mô phỏng, và phân tích dữ liệu. Mỗi kịch bản SC01–SC11 có một test tự động.

**Q2. Vì sao dùng đồng hồ giả?**
Để chạy kịch bản dài hàng phút hoặc giờ trong mili-giây, lặp lại được, và kiểm các mốc thời gian chính xác (30 giây, 2 phút).

**Q3. Liệt kê SC01–SC12.**
Xem bảng "Kịch bản phải qua" trong `docs/nhiem-vu.md`; ví dụ SC01 vào phòng tối thì đèn bật, SC02 vào phòng sáng thì giữ nguyên, SC03 nằm sofa nóng thì bật quạt, SC07 đi vắng quên tắt thì cảnh báo, SC12 đổi thói quen thì cập nhật đề xuất.

**Q4. Kết quả kiểm thử hiện tại?**
72 test lõi và 70 test Hub qua; kịch bản thực tế R01–R20 đạt 20/20 trong lần chạy 15/11/2026; e2e SC01–SC11 có test. Bộ `pytest sim` có một test đỏ khi chạy chung (`test_p100_publishes_state_on_set`), nghi tranh chấp cổng broker 1883 với phiên khác; cần chạy riêng để xác nhận. **Phải nói thẳng điều này nếu bị hỏi.**

**Q5. Mô phỏng khác thực tế ở đâu?**
Đầu vào do ta viết nên sạch hơn thật: tư thế không nhiễu như camera thật, thời gian bị nén, không có độ trễ mạng và phần cứng. Vì vậy phải kiểm lại bằng video thật và phần cứng thật.

**Q6. Kịch bản R có "chỉnh cho dễ qua" không?**
Không. Kỳ vọng phản ánh luật hiện hành của các engine (`docs/kich-ban-thuc-te.md`); các kịch bản còn **phát hiện** khoảng trống hành vi (không có luật tắt, R06 phụ thuộc cấu hình, R17 tự bật lại sau mất điện).

**Q7. Kể vài phát hiện từ kịch bản thực tế.**
(1) Không có luật tắt: mưa làm mát vẫn chạy quạt. (2) Đèn không tắt khi người rời. (3) R06: nhận hàng quá ~2,5 phút sẽ có cảnh báo oan. (4) R17: sau mất điện Hub bật lại quạt, kể cả khi người dùng chủ ý cắt nguồn. (5) Nghi: người khuất hẳn khỏi hình ở `other_exit` có thể thành `AwayConfirmed` (chưa đo).

**Q8. Bất biến an toàn của rơ-le được kiểm thế nào?**
`tests/e2e/test_safety_invariants.py` và test Hub kiểm: hai lệnh tới cùng thiết bị cách nhau không dưới khoảng tối thiểu; số lần đổi tự động không vượt giới hạn; thiết bị không tự bật lại trái lệnh tay.

**Q9. MATLAB phân tích những gì?**
Ba script: (a) khảo sát: nhiệt độ, lux trung bình theo giờ, tương quan với bật quạt và đèn; (b) so luật cố định với luật thích nghi (số phút lệch hành vi, số lần bật/tắt sai khi có người); (c) đánh giá Occupancy so với nhãn thật mỗi phút: accuracy, ma trận nhầm lẫn, tỷ lệ báo vắng sai và bỏ sót, phút `Unknown`.

**Q10. Ma trận nhầm lẫn là gì, đọc thế nào?**
Bảng 2×2: hàng là sự thật (ở nhà, vắng), cột là dự đoán. Chéo chính là đúng; ô "thật ở nhà nhưng báo vắng" là **báo vắng sai**, ô "thật vắng nhưng báo ở nhà" là **bỏ sót vắng**. Với đề tài, báo vắng sai nguy hiểm hơn.

**Q11. Dữ liệu cho MATLAB lấy ở đâu?**
Từ PostgreSQL qua `tools/export_csv.py` thành CSV; nhãn thật tự ghi vào `truth_labels.csv` (cột `ts`, `truth` = `home`|`away`).

**Q12. Các script MATLAB đã chạy thật chưa?**
Chưa. Máy chưa có MATLAB hay Octave; script mới được viết và thử định dạng cột bằng dữ liệu giả (`tools/make_synthetic.py`, 14 ngày, thư mục `SYNTHETIC_DU_LIEU_GIA`). **Dữ liệu giả không dùng cho kết luận luận văn.** **[CHƯA CHẠY THẬT]**

**Q13. Vì sao phải có nhãn thật?**
Không có "sự thật" thì không tính được độ chính xác. Nhãn là lúc bạn thực sự vào hay ra khỏi nhà, ghi tay hoặc từ cảm biến phụ.

**Q14. Có đánh giá `ai_event` (nhận diện hành động) theo từng sự kiện chưa?**
Chưa. `c_occupancy_eval.m` mới đánh giá Occupancy mỗi phút; đánh giá từng sự kiện hành động là hướng phát triển.

**Q15. Số liệu nào bạn sẽ đưa vào luận văn?**
Số từ dữ liệu thật: độ chính xác, tỷ lệ báo vắng sai, thời gian cảnh báo trễ. Hiện chưa có; cần thu vài tuần log và quay video có nhãn.

**Q16. Hệ thống thử với lỗi nào?**
Mất MQTT, mất camera (SC10), mất AI, ổ cắm offline, Hub khởi động lại, trẻ chạy nhảy gây nhiễu, mất DB. Trong mô phỏng đã có (R11, R12, R17, R18, R19). Thử trên phần cứng thật thuộc NV10. **[CHƯA CHẠY THẬT]**

**Q17. Bạn đo hiệu năng (CPU, RAM, độ trễ) chưa?**
Chưa. Cần chạy AI với RTSP thật; kế hoạch đo CPU/RAM khi chạy AI và độ trễ từ cảm biến đến ổ cắm (NV10). **[CHƯA CHẠY THẬT]**

### 9.4 Mã liên quan
`tests/SmartHomeAI.Tests/`, `tests/SmartHomeAI.Hub.Tests/`, `tests/e2e/`, `sim/` (`scenarios.py`, `realistic.py`, `noise.py`, `fuzz.py`), `docs/kich-ban-thuc-te.md`, `matlab/`, `tools/`.

### 9.5 Ghi chú chạy thật
- Cài MATLAB hoặc Octave và chạy `run_all.m` trên dữ liệu giả để kiểm script; sau đó với dữ liệu thật.
- Thu vài tuần log PostgreSQL và nhãn thật; quay video có nhãn.
- Chạy lại e2e với phần cứng thật và cập nhật mô phỏng bằng dữ liệu đã ghi.
- Xác nhận test đỏ riêng trong `pytest sim` bằng cách chạy với broker riêng.

---

<!-- PHẦN 10: Câu hỏi hóc búa và hạn chế -->
## Phần 10. Câu hỏi hóc búa và hạn chế

Phần này dành cho những câu hội đồng thường hỏi để thử độ trung thực và độ sâu. Nguyên tắc trả lời: **nêu hạn chế trước khi bị hỏi, nêu cách đo hoặc kế hoạch, không bịa số.**

### 10.1 Câu hỏi hội đồng

**Q1. Hệ thống đã chạy ngoài đời chưa?**
Chưa. Toàn bộ kiểm chứng bằng mô phỏng và test; chưa có video thật, ESP32, P100, C222. **[CHƯA CHẠY THẬT]** Kế hoạch ghép từng thiết bị thay đúng phần mô phỏng tương ứng.

**Q2. Độ chính xác của hệ thống là bao nhiêu?**
Chưa có số. Test qua chứng minh logic với đầu vào giả. Độ chính xác nhận diện cần video thật có nhãn; sẽ báo tỷ lệ dương tính giả và âm tính giả.

**Q3. Điều gì xảy ra nếu AI nhận sai tư thế?**
Quạt có thể bật nhầm hoặc không bật. Tác hại thấp (quạt). Với cảnh báo và tự tắt, sai nghiêm trọng hơn, nên tự tắt mặc định **tắt** và Occupancy không kết luận vắng khi thiếu bằng chứng.

**Q4. Nếu hệ thống báo vắng sai khi đang có người nằm sofa?**
Lớp phòng vệ: cần bằng chứng rời nhà (cửa chính) cộng 30 giây xác minh; người nằm vẫn trong hình sẽ `Occupied`. Rủi ro còn lại: khuất khỏi hình ở vùng `other_exit` (đã ghi nhận, chưa đo).

**Q5. Một camera có đủ không?**
Không đủ để biết người ở phòng khác. Vì vậy hệ thống chỉ kết luận vắng khi đủ chuỗi bằng chứng, và để trạng thái `Unknown` hoặc `OtherArea` khi thiếu. Muốn tốt hơn cần thêm cảm biến hoặc camera.

**Q6. Vì sao không dùng cảm biến PIR rẻ hơn camera?**
PIR chỉ báo chuyển động, không biết người ngồi hay nằm yên (sẽ "mất" người đang nằm), và không cho tư thế. Camera cho vùng và tư thế, đổi lại phức tạp và nhạy riêng tư. [Có thể nêu: kết hợp PIR là hướng cải thiện.]

**Q7. Quyền riêng tư: camera trong nhà có an toàn không?**
Hình chỉ đi vào máy chủ trong nhà, không lưu hình, không gửi lên đám mây, API Production nghe `127.0.0.1`. Hạn chế: máy chủ hình MJPEG của AI nghe `0.0.0.0` theo mặc định (`StreamServer`), nên cần giới hạn mạng. Cần kiểm lại trước khi triển khai.

**Q8. Đã phát hiện những lỗ hổng bảo mật nào và xử lý ra sao?**
Cấu hình mặc định mở (khóa API tùy chọn, MQTT ẩn danh, nghe mọi giao diện, mật khẩu DB trong tệp cấu hình), và tranh chấp luồng trong Hub. Đã sửa: Production bắt buộc khóa và tài khoản MQTT, nghe `127.0.0.1`, bỏ mật khẩu khỏi cấu hình chung, khóa chung cho `MessageRouter`. Còn lại: Dashboard không đăng nhập, API đọc không khóa, MQTT không TLS.

**Q9. Nếu ổ P100 đổi giao thức?**
Phương án dự phòng: relay điều khiển qua ESP32. Vì vậy có lớp adapter tách riêng, thay được mà không đổi Hub. Chưa thử thật. **[CHƯA CHẠY THẬT]**

**Q10. Hệ thống khác gì các sản phẩm thương mại (Google Home, Home Assistant)?**
Home Assistant cũng có tự động hóa theo hiện diện nhưng thường dựa vào cảm biến sẵn có; đề tài tập trung vào **suy luận từ camera và chuỗi bằng chứng đi vắng**, bảo vệ rơ-le và học thói quen có duyệt. Không nên khẳng định tốt hơn khi chưa đo.

**Q11. Vì sao chọn giá trị 28 °C, 50 lux, 30 giây, 2 phút?**
Là giá trị tạm, từ kinh nghiệm thông thường; đã ghi rõ phải chỉnh theo dữ liệu thật và nêu lý do trong luận văn. Luật thích nghi (Phần 7) là cách thay ngưỡng cố định bằng thói quen.

**Q12. Nếu có nhiều người dùng với thói quen khác nhau?**
Hiện chưa phân biệt người. Tracker chỉ gán số hiệu theo khung; luật học là chung cho cả nhà. Phân biệt người là giới hạn và hướng phát triển.

**Q13. Hệ thống có thể gây nguy hiểm không (cháy, hỏng thiết bị)?**
Rủi ro chính là bật tắt rơ-le dồn dập; đã có nhiều lớp (Phần 5). Quạt bật khi không có người chỉ xảy ra khi Occupancy sai; tự tắt khi vắng mặc định tắt. Chưa thử độ bền rơ-le thật.

**Q14. Bạn làm gì nếu mất mạng nội bộ hoặc Wi-Fi?**
Thiết bị mất kết nối thì LWT báo offline; Hub coi là mất dữ liệu và không suy ra gì; không gửi lệnh được (bị bỏ với cảnh báo). Khi về lại, thiết bị đồng bộ trạng thái và Hub không coi đó là lệnh tay (cửa sổ đồng bộ 5 giây).

**Q15. Điểm yếu lớn nhất của đề tài?**
Độ chính xác nhận diện nằm sofa và nhà vắng chưa đo trên dữ liệu thật; mọi kết luận về độ chính xác phụ thuộc video thật. Đó là rủi ro lớn nhất và là việc ưu tiên tiếp theo.

**Q16. Nếu làm lại, bạn sẽ đổi gì?**
Quay video thật từ sớm để đo AI trước khi hoàn thiện các tầng trên; thêm cảm biến phụ cho phòng khác; ghi log từ đầu để có nhiều tuần dữ liệu; đưa Habit vào Hub sớm.

**Q17. Hướng phát triển?**
Đo và thay bộ nhận diện (pose hoặc mô hình tốt hơn), thêm cảm biến/camera phòng khác, luật tắt thông minh, thông báo điện thoại, đăng nhập và phân quyền, TLS cho MQTT, đánh giá `ai_event` từng sự kiện, nhiều người dùng.

**Q18. Đóng góp của bạn khác gì "ghép thư viện có sẵn"?**
Phần lõi: máy trạng thái hiện diện với chuỗi bằng chứng và an toàn khi thiếu dữ liệu, bảo vệ rơ-le nhiều lớp, quản lý cảnh báo có phản hồi, học ngưỡng có duyệt, bộ kịch bản kiểm thử (SC, R, N) và bộ phân tích MATLAB. Phần dùng sẵn: OpenCV, mô hình YOLO, MQTT, ASP.NET Core.

### 10.2 Ghi chú chạy thật
Mọi câu trả lời gắn **[CHƯA CHẠY THẬT]** ở các phần trước là danh sách việc cần làm trước buổi bảo vệ nếu có thể: quay video có nhãn, đo độ chính xác, thử P100 và ESP32, chạy 48 giờ, chạy MATLAB trên dữ liệu thật.
