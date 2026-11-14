# Kiến thức nền tảng cho người chưa biết gì về hệ thống

Tài liệu giải thích **từng công nghệ và khái niệm** dùng trong đồ án Smart Home AI, theo thứ tự từ dễ đến khó. Mỗi mục trả lời bốn câu: **Nó là gì? Hoạt động ra sao? Trong dự án nó làm gì? Xem mã ở đâu?**

Cách dùng: đọc Mục 0 để có bức tranh chung, đọc tiếp theo thứ tự. Gặp từ lạ thì tra Mục 19 (bảng thuật ngữ). Tài liệu ôn câu hỏi hội đồng nằm ở `docs/on-tap-bao-ve.md`; tài liệu này là phần **lý thuyết** đứng trước nó.

Quy ước: những chỗ ghi **[chưa chạy thật]** nghĩa là hệ thống mới chạy bằng mô phỏng, chưa thử với phần cứng hay video thật.

## Mục lục
0. Bức tranh chung qua một buổi tối
1. IoT và nhà thông minh
2. Mạng máy tính tối thiểu cần biết
3. MQTT (chi tiết)
4. JSON và schema
5. Phần cứng: ESP32, cảm biến, ổ cắm, rơ-le
6. Camera, RTSP và ảnh số
7. Thị giác máy tính: phát hiện, theo dõi, tư thế, vùng
8. Máy trạng thái và theo dõi hiện diện
9. Điều khiển tự động an toàn: chống dồn lệnh, bảo vệ rơ-le
10. Lập trình đồng thời và tranh chấp luồng
11. Thời gian, kiểm thử và mô phỏng
12. Máy chủ Hub: HTTP, API, khóa API, Blazor
13. Cơ sở dữ liệu PostgreSQL và ghi log
14. Triển khai: Docker, systemd, môi trường
15. Thống kê cơ bản dùng trong đồ án
16. MATLAB và phân tích thực nghiệm
17. Học thói quen
18. Từ đầu đến cuối: một lệnh bật quạt đi qua những đâu
19. Bảng thuật ngữ A–Z

---

## 0. Bức tranh chung qua một buổi tối

Tối thứ sáu, bạn về nhà, mở cửa, vào phòng khách tối om, ngồi xuống sofa rồi nằm xem TV. Trời nóng.

Hệ thống làm những việc sau, mà bạn không bấm gì:

1. **Cảm biến cửa** (gắn ở cửa chính) báo "cửa mở".
2. **Camera** nhìn thấy một người đi vào. Chương trình **AI** (Python) phát hiện người, biết người đang ở vùng nào và tư thế gì, rồi **báo cho Hub** qua mạng.
3. **Hub** (máy chủ trung tâm) ghép các tin: "có người trong phòng khách" và "cảm biến ánh sáng đọc 18 lux (tối)". Luật: có người và tối thì **bật đèn**. Hub gửi lệnh bật cho ổ cắm đèn.
4. Bạn nằm xuống sofa. Cảm biến nhiệt đo 31 °C. Luật: nằm sofa và nóng thì **bật quạt**. Hub gửi lệnh cho ổ cắm quạt.
5. Sáng hôm sau bạn đi làm, quên tắt quạt. Cửa mở, camera thấy bạn ra, rồi không còn ai. Hub chờ 30 giây để chắc là bạn đã ra khỏi nhà, rồi chờ thêm 2 phút. Quạt vẫn bật nên Hub **phát cảnh báo** (buzzer kêu, thông báo trên Dashboard).

Các khối và đường đi của thông tin:

```
 Camera ──RTSP──► AI (Python) ──┐
                                │  MQTT (qua broker Mosquitto)
 ESP32 (nhiệt, sáng, cửa) ──────┼────────────► HUB (C#) ──► PostgreSQL (lưu log)
                                │                │  ▲
 Ổ cắm P100 (đèn, quạt) ◄───────┘  MQTT          │  └── Dashboard (Blazor, trình duyệt)
        ▲                                        │
        └────────── lệnh bật/tắt ◄───────────────┘
                                       MATLAB đọc CSV xuất từ PostgreSQL để phân tích
```

Nguyên tắc quan trọng nhất của thiết kế: **AI chỉ nhìn và báo cáo; Hub mới là bộ não ra quyết định.** Lý do ở Mục 8.

Phần cứng thật chưa có, nên mọi thứ đang chạy với **mô phỏng** (thư mục `sim/`): chương trình giả làm ESP32, ổ cắm và camera, phát các tin y như thật. Cách này cho phép viết và kiểm thử toàn bộ phần mềm trước.

---

## 1. IoT và nhà thông minh

**Nó là gì.** IoT (Internet of Things, "Internet vạn vật") là các thiết bị vật lý (cảm biến, ổ cắm, camera) có kết nối mạng để gửi dữ liệu hoặc nhận lệnh.

**Hoạt động ra sao.** Một hệ IoT thường có ba lớp:
- **Cảm nhận** (sensing): cảm biến đo thế giới thật (nhiệt độ, ánh sáng, cửa, hình ảnh).
- **Xử lý** (processing): phần mềm hiểu dữ liệu và quyết định.
- **Tác động** (actuation): thiết bị làm việc (bật đèn, bật quạt, kêu buzzer).

**Trong dự án.** Cảm nhận là camera và ESP32 với ba cảm biến; xử lý là AI cộng Hub; tác động là hai ổ cắm P100 (đèn, quạt) và buzzer. Điểm đặc biệt: đa số "nhà thông minh" chỉ **hẹn giờ hoặc điều khiển từ xa**. Đề tài này làm hệ thống **tự hiểu hoàn cảnh** (có người không, đang nằm hay ngồi, đã đi vắng chưa) rồi mới hành động.

**Xem mã.** Kiến trúc tổng thể: `docs/nhiem-vu.md`; cách các khối nói chuyện: `docs/mqtt-topics.md`.

---

## 2. Mạng máy tính tối thiểu cần biết

**Địa chỉ IP và cổng (port).** Mỗi máy trong mạng có một địa chỉ IP (ví dụ `192.168.1.10`). Trên một máy có nhiều chương trình cùng nhận kết nối, nên mỗi chương trình nghe ở một **cổng** (số từ 0–65535). Địa chỉ đầy đủ là `IP:cổng`. Trong dự án: broker MQTT nghe cổng `1883`, Hub nghe `5080` (Production), PostgreSQL `5432`, luồng hình của AI `8090`.

**LAN và `localhost`.** LAN là mạng nội bộ trong nhà. `localhost` (hay `127.0.0.1`) nghĩa là "chính máy này", chỉ chương trình trên cùng máy mới gọi được.

**Nghe trên giao diện nào.** Một chương trình nghe `0.0.0.0` tức là nhận kết nối từ **mọi** thiết bị trong mạng; nghe `127.0.0.1` thì chỉ chính máy đó. Đây là điểm bảo mật: Hub ở Production nghe `127.0.0.1:5080`, nên chỉ chương trình cùng máy (ví dụ Dashboard) gọi được.

**Máy khách và máy chủ (client/server).** Máy chủ (server) chờ và phục vụ; máy khách (client) chủ động kết nối. Hub là server cho Dashboard và là **client** của broker MQTT.

**TCP.** Giao thức nền đảm bảo dữ liệu đến đủ và đúng thứ tự. MQTT và HTTP chạy trên TCP. Nếu kết nối đứt, hai bên biết và có thể nối lại.

**Xem mã.** `infra/systemd/smarthome-hub.service`, `docker-compose.yml`.

---

## 3. MQTT (chi tiết)

### 3.1 MQTT là gì
MQTT (Message Queuing Telemetry Transport) là giao thức nhắn tin **nhẹ**, thiết kế cho thiết bị nhỏ và mạng yếu. Mô hình của nó là **đăng-nhận (publish/subscribe)** qua một trạm trung gian gọi là **broker**.

So sánh đời thường: broker là **bảng thông báo** của tòa nhà chia thành nhiều ngăn có nhãn. Ai muốn báo gì thì dán giấy vào ngăn đúng nhãn (publish). Ai quan tâm ngăn nào thì đăng ký nhận (subscribe) và được báo mỗi khi có giấy mới. Người dán không cần biết ai đọc; người đọc không cần biết ai dán.

### 3.2 Các khái niệm
- **Broker**: chương trình trung gian nhận mọi tin và chuyển cho người đăng ký. Dự án dùng **Mosquitto** (chạy bằng Docker).
- **Client**: mọi chương trình nối vào broker (AI, Hub, ESP32, ổ cắm, mô phỏng).
- **Topic**: "nhãn ngăn", chuỗi phân cấp bằng dấu `/`. Ví dụ `home/device/fan/set`.
- **Payload**: nội dung tin, trong dự án là JSON.
- **Wildcard**: ký tự đại diện khi đăng ký. `+` thay đúng một cấp (`home/sensor/+/env` khớp `home/sensor/esp1/env` và `esp2`); `#` thay mọi cấp phía sau (`home/#` khớp mọi tin của nhà). Hub đăng ký `home/#` để nhận hết.
- **QoS (Quality of Service)**, mức đảm bảo giao tin:
  - QoS 0: gửi một lần, có thể mất.
  - QoS 1: đảm bảo ít nhất một lần, có thể trùng. **Dự án dùng mức này cho mọi topic.**
  - QoS 2: đúng một lần, chậm hơn.
- **Retained (giữ lại)**: broker nhớ tin cuối cùng của topic; client đến sau nhận ngay tin đó. Dùng cho **trạng thái** (cửa đang mở hay đóng, thiết bị đang bật hay tắt) để ai kết nối sau cũng biết trạng thái hiện tại.
- **LWT (Last Will and Testament, "di chúc")**: khi kết nối, client dặn broker "nếu tôi chết đột ngột thì hãy phát tin này". Dự án: mỗi nguồn gửi `status = online` (retained) và đặt LWT `offline` cùng topic. Nguồn mất điện hay mất mạng thì broker tự phát `offline`, và Hub biết ngay nguồn đó đã mất.
- **Keep-alive**: client định kỳ gửi tín hiệu "tôi còn sống"; quá hạn thì broker coi như đã đứt.

### 3.3 Trong dự án MQTT làm gì
Toàn bộ các khối nói chuyện qua MQTT. Bảng topic (trích `docs/mqtt-topics.md`):

| Topic | Ai gửi | Ai nhận | Nội dung |
|---|---|---|---|
| `home/sensor/esp1/env` | ESP32 | Hub | nhiệt độ, độ ẩm, lux, mỗi 10 giây |
| `home/sensor/esp1/door` | ESP32 | Hub | cửa mở/đóng, retained |
| `home/ai/livingroom/state` | AI | Hub | danh sách người trong hình, 1–2 lần/giây |
| `home/ai/livingroom/transition` | AI | Hub | một người vừa đổi vùng, có hướng vào/ra |
| `home/device/fan/set` | Hub | ổ cắm quạt | lệnh bật/tắt |
| `home/device/fan/state` | ổ cắm quạt | Hub | trạng thái thực tế, retained |
| `home/alert/{id}` | Hub | ai quan tâm | cảnh báo đang diễn ra |
| `home/*/status` | từng nguồn | Hub | online/offline (có LWT) |

**Vì sao chọn MQTT cho dự án này:** nhẹ cho vi điều khiển ESP32; một nguồn gửi, nhiều nơi nhận (Hub, ghi log, Dashboard) mà không phải sửa nguồn; có retained và LWT để biết trạng thái và biết ai chết; mô phỏng chỉ cần "gửi tin giả" là thay được camera và cảm biến.

### 3.4 Ví dụ cụ thể
Hub quyết định bật quạt. Nó gửi:
- Topic: `home/device/fan/set`
- Payload: `{"ts":"2026-11-14T12:00:01Z","on":true,"source":"auto"}`

Ổ cắm quạt đã đăng ký topic này, nhận tin, bật rơ-le, rồi gửi lại:
- Topic: `home/device/fan/state` (retained)
- Payload: `{"ts":"2026-11-14T12:00:02Z","on":true}`

Hub nhận `state` và biết quạt **thực sự** đã bật. Nếu không nhận được `state`, Hub gửi lại lệnh sau 60 giây rồi 120 giây, sau đó ghi "thiết bị không phản hồi".

### 3.5 Tự thử
Chạy `docker compose up -d`, rồi một cửa sổ chạy `mosquitto_sub -t 'home/#' -v` (xem mọi tin), cửa sổ khác chạy `python -m sim run SC07 --speed 20` (mô phỏng kịch bản đi vắng quên tắt điện) để thấy các tin trôi qua.

### 3.6 Bảo mật MQTT
Bản dev cho phép **ẩn danh** (`allow_anonymous true`): ai trong mạng cũng gửi được lệnh. Bản `secure` bắt buộc **tên và mật khẩu**, kèm **ACL** (danh sách quyền: tài khoản nào được đọc/ghi topic nào). Ví dụ tài khoản `ai` chỉ được ghi `home/ai/#`; `sensor` chỉ ghi `home/sensor/#`. Chưa có **TLS** (mã hóa đường truyền), nên chỉ tin cậy trong mạng LAN riêng. Hub ở Production bắt buộc có tài khoản MQTT mới khởi động.

**Xem mã.** `infra/mosquitto/`, `src/SmartHomeAI.Hub/MqttService.cs` (kết nối, đăng ký, tự nối lại), `MqttBus.cs` (gửi tin), `docs/mqtt-topics.md`.

---

## 4. JSON và schema

**JSON** là định dạng văn bản mô tả dữ liệu bằng cặp `"khóa": giá trị`, dễ đọc cho cả người lẫn máy. Ví dụ tin AI báo có một người nằm trên sofa:

```json
{"ts":"2026-11-14T12:00:00Z","seq":4812,"camera_ok":true,
 "persons":[{"track_id":3,"zone":"sofa","posture":"lying","conf":0.82}]}
```

Giải nghĩa: `ts` là thời điểm (ISO 8601, múi giờ UTC); `seq` là số thứ tự tin; `camera_ok` cho biết camera còn nhìn được không; `persons` là danh sách người; mỗi người có `track_id` (số hiệu theo dõi), `zone` (vùng), `posture` (tư thế), `conf` (độ tin cậy 0–1).

**Schema** là bản "hợp đồng" mô tả hình dạng hợp lệ của JSON: trường nào bắt buộc, kiểu gì, giá trị nào cho phép. Dự án để schema trong `docs/schemas/`. Tác dụng: mọi bên (AI, Hub, ESP32, mô phỏng) thống nhất định dạng; tin sai định dạng bị Hub **bỏ qua và ghi cảnh báo** thay vì làm sập chương trình.

**Quy tắc làm việc:** muốn đổi định dạng tin thì sửa tài liệu và schema **trước**, sửa mã **sau**.

**Xem mã.** `docs/schemas/`, `src/SmartHomeAI.Hub/Messages.cs` (bộ đọc tin `MqttParser`).

---

## 5. Phần cứng: ESP32, cảm biến, ổ cắm, rơ-le

**ESP32** là vi điều khiển (một máy tính nhỏ, rẻ, tiêu thụ ít điện) có sẵn Wi-Fi. Nó đọc cảm biến rồi gửi tin MQTT. Dự án dùng hai ESP32 (`esp1`, `esp2`; chưa chốt cảm biến nào gắn vào ESP nào).

**Các cảm biến:**
- **SHT30**: đo **nhiệt độ** (°C) và **độ ẩm** (%).
- **BH1750**: đo **ánh sáng** theo đơn vị **lux** (50 lux ≈ phòng khá tối; ngưỡng "tối" mặc định của dự án).
- **MC-38**: **công tắc từ** báo cửa. Một nam châm gắn cánh cửa, một cảm biến gắn khung; khi cửa mở, nam châm rời ra và mạch hở, ESP32 gửi `{"open":true}`.
- **Buzzer**: loa nhỏ kêu bíp, Hub bật khi có cảnh báo.

**Ổ cắm thông minh Tapo P100.** Cắm đèn hoặc quạt vào ổ; ổ có Wi-Fi và nhận lệnh bật/tắt. Bên trong là một **rơ-le**.

**Rơ-le** là công tắc **cơ khí** điều khiển bằng điện: cuộn dây hút một tiếp điểm để đóng hoặc ngắt mạch. Đóng ngắt tạo tia lửa nhỏ và mài mòn tiếp điểm, nên **bật tắt dồn dập làm hỏng rơ-le nhanh** và có thể nguy hiểm với tải như quạt. Đó là lý do Mục 9 có nhiều lớp bảo vệ.

**Adapter P100.** Hub không nói trực tiếp giao thức hãng. Một chương trình trung gian (adapter) nhận `home/device/{id}/set`, điều khiển ổ qua LAN, rồi trả `state`. Rủi ro: hãng có thể đổi giao thức; dự phòng là dùng relay riêng nối vào ESP32. **[chưa chạy thật]**

**Quạt trần** (treo trên trần phòng khách, không phải quạt đứng) và **đèn** là hai thiết bị id `fan` và `light`.

**Xem mã.** Mô phỏng phần cứng: `sim/esp32.py`, `sim/p100.py`.

---

## 6. Camera, RTSP và ảnh số

**Ảnh số.** Một ảnh là lưới điểm ảnh (pixel), mỗi điểm có giá trị màu. Video là chuỗi ảnh gọi là **khung hình (frame)**, phát với tốc độ **fps** (khung/giây). Camera dự định là **Tapo C222**.

**RTSP (Real Time Streaming Protocol).** Giao thức camera IP dùng để phát luồng video qua mạng, địa chỉ có dạng `rtsp://tài_khoản:mật_khẩu@IP/stream`. Phải bật RTSP trong ứng dụng của camera và tạo tài khoản camera. Chương trình đọc luồng bằng **OpenCV** (`cv2.VideoCapture`).

**OpenCV** là thư viện mã nguồn mở về xử lý ảnh và thị giác máy tính; dự án dùng nó để đọc video, thay đổi kích thước, vẽ hộp và chạy HOG.

**Tọa độ chuẩn hóa.** Thay vì dùng pixel (phụ thuộc độ phân giải), dự án dùng tọa độ từ 0 đến 1: `(0,0)` là góc trên trái, `(1,1)` là góc dưới phải. Nhờ vậy vùng và hộp không đổi khi đổi độ phân giải camera.

**Không chạy mỗi khung.** Người trong nhà di chuyển chậm nên AI chỉ phân tích khoảng **4 khung/giây** và gửi trạng thái **1 lần/giây**; đủ dùng và tiết kiệm CPU (máy chủ không có GPU).

**Luồng hình có lớp phủ.** AI mở một máy chủ nhỏ (cổng 8090) phát hình MJPEG có vẽ hộp, khớp xương và vùng, để người chỉnh vùng nhìn thấy. Mặc định nghe mọi giao diện, cần giới hạn mạng khi triển khai thật.

**Xem mã.** `ai/smarthome_ai/source.py` (đọc video/RTSP, tự mở lại khi mất), `stream.py`.

---

## 7. Thị giác máy tính: phát hiện, theo dõi, tư thế, vùng

Đây là phần AI. Chuỗi xử lý cho mỗi khung hình (`ai/smarthome_ai/pipeline.py`):

```
khung hình → phát hiện người → theo dõi (gán số hiệu) → tư thế → vùng → chuyển vùng → gửi tin
```

### 7.1 Phát hiện người (detection)
**Là gì:** tìm vị trí mỗi người trong ảnh, dưới dạng **hộp chữ nhật** bao quanh (bounding box) kèm **độ tin cậy** (confidence, 0–1).

Dự án có hai bộ phát hiện (cắm đổi được, vì cùng giao diện `detect(frame)`):

**HOG + SVM (OpenCV), bản mặc định.**
- HOG (Histogram of Oriented Gradients): chia ảnh thành các ô nhỏ, đo **hướng cạnh** trong mỗi ô, ghép thành "dấu vân tay hình dáng".
- SVM (Support Vector Machine): bộ phân loại học sẵn, nhìn dấu vân tay và quyết định "đây là người hay không".
- Một cửa sổ trượt quét khắp ảnh ở nhiều cỡ.
- Ưu: nhẹ, không cần tải mô hình, chạy CPU. Nhược: được học chủ yếu từ **người đứng**, kém với người **nằm**.

**YOLOv8n-pose (ONNX), tùy chọn `--detector pose`.**
- YOLO là mạng nơ-ron tích chập (CNN) nhìn **cả ảnh một lần** và dự đoán luôn hộp. Bản "pose" còn dự đoán **17 khớp xương** của từng người.
- Chạy bằng **onnxruntime** trên CPU. File mô hình `.onnx` tải riêng.
- **NMS (Non-Maximum Suppression):** mạng thường cho nhiều hộp chồng lên cùng một người; NMS giữ hộp tốt nhất và bỏ hộp trùng dựa trên IoU (7.2).
- Chuẩn **COCO-17** gồm: mũi, 2 mắt, 2 tai, 2 vai, 2 khuỷu, 2 cổ tay, 2 hông, 2 gối, 2 cổ chân.

**Hiện trạng.** HOG chỉ là mốc so sánh (baseline). Chọn bộ nào chính thức cần **đo trên video thật**. **[chưa chạy thật]**

### 7.2 Theo dõi (tracking) và IoU
**Vấn đề:** detection chỉ nói "khung này có 2 người ở đây"; nó không biết người ở khung này **là người nào** ở khung trước. Tracking gán cho mỗi người một `track_id` ổn định qua các khung.

**IoU (Intersection over Union):** đo độ chồng của hai hộp = diện tích phần giao chia diện tích phần hợp. Bằng 1 là trùng khít, 0 là rời nhau.

**Thuật toán đơn giản của dự án** (`tracker.py`):
1. Với mỗi track đang có, tìm detection có IoU lớn nhất với hộp cũ; cần IoU tối thiểu 0,2.
2. Nếu không đủ IoU thì xét **khoảng cách tâm** (dưới 0,15 theo tọa độ chuẩn hóa).
3. Detection chưa ghép với track nào thì tạo track mới.
4. Track không ghép được thì đếm "mất"; mất quá 8 khung thì xóa.

**Hạn chế:** không có đặc trưng ngoại hình hay dự đoán chuyển động (như bộ lọc Kalman), nên hai người đi qua nhau có thể bị đổi số hiệu. Hệ thống giảm tác động vì Occupancy chủ yếu quan tâm "có ai trong phòng" chứ không quan tâm "ai".

### 7.3 Tư thế (posture)
Có năm giá trị: `standing` (đứng), `sitting` (ngồi), `lying` (nằm), `moving` (đang đi), `unknown`. Dự án suy ra bằng **quy tắc hình học**, không phải mô hình học riêng:

1. Nếu tâm hộp dịch nhanh (hơn 0,5 chiều cao hộp mỗi giây) thì `moving`.
2. Nếu có khớp xương: so vị trí **vai** và **hông**. Thân nằm ngang (chênh ngang lớn hơn chênh dọc) hoặc đầu-vai-hông ngang bằng nhau thì `lying`. Hông ngang với gối thì `sitting`, chân duỗi thẳng xuống thì `standing`.
3. Nếu không có khớp xương: dùng **tỉ lệ rộng/cao của hộp**: lớn hơn 1,1 là nằm, nhỏ hơn 0,55 là đứng, ở giữa là ngồi.

**Làm mượt bằng bỏ phiếu:** mỗi khung có nhiễu nên tư thế có thể nhảy lung tung. Hệ thống lưu 8 khung gần nhất và lấy **tư thế chiếm đa số**. Chưa đủ 4 khung thì `unknown`; hòa phiếu thì giữ tư thế đang có.

**Lưu ý trung thực:** các ngưỡng 1,1, 0,55, 0,5 là **số đoán ban đầu**, cần chỉnh bằng video thật. **[chưa chạy thật]**

### 7.4 Vùng (zone) và ray casting
Người dùng vẽ trên khung hình các **đa giác** (tọa độ chuẩn hóa) cho bốn vùng:
- `livingroom`: phòng khách (vùng dự phòng, nằm cuối danh sách ưu tiên);
- `sofa`: sofa;
- `door`: khu vực cửa chính;
- `other_exit`: lối đi sang khu khác trong nhà.

Câu hỏi "điểm này nằm trong đa giác nào?" được trả lời bằng thuật toán **ray casting**: từ điểm kẻ một tia ngang, đếm số cạnh đa giác bị cắt; **lẻ là bên trong, chẵn là bên ngoài**.

**Lấy điểm nào của người để thử?** Người đứng: **điểm chân** (đáy hộp). Người nằm hoặc ngồi: **tâm hộp**, vì chân người nằm trên sofa chạm đáy khung nên có thể trượt khỏi đa giác sofa.

### 7.5 Chuyển vùng (transition)
Khi một người đổi vùng, và vùng mới giữ **ổn định 6 khung liên tiếp** (lọc nhiễu khi đứng sát ranh giới) thì AI phát tin `transition` kèm hướng:
- từ vùng phòng (`livingroom`, `sofa`) sang vùng thoát (`door`, `other_exit`) là `out`;
- ngược lại là `in`.

AI **chỉ báo** "người số 3 đi từ phòng khách ra cửa". Việc hiểu "ra cửa chính nghĩa là có thể rời nhà" là của Hub.

### 7.6 Giới hạn phải nhớ
- Một camera chỉ thấy phòng khách; **không biết người ở phòng khác**.
- Camera ban đêm (hồng ngoại) làm hình khác thường, nhận diện có thể kém.
- Độ chính xác nhận diện nằm sofa **chưa đo**. **[chưa chạy thật]**

**Xem mã.** `ai/smarthome_ai/` gồm `detector.py`, `pose.py`, `posture.py`, `tracker.py`, `zones.py`, `transitions.py`, `pipeline.py`.

---

## 8. Máy trạng thái và theo dõi hiện diện

### 8.1 Máy trạng thái hữu hạn
Hệ thống luôn ở **đúng một trạng thái** trong một tập cố định, và chuyển trạng thái khi có **sự kiện** hoặc khi **thời gian trôi** đủ lâu. Ví dụ đèn giao thông: xanh → vàng → đỏ. Lợi ích: không thể ở hai trạng thái mâu thuẫn, mọi đường chuyển liệt kê được và kiểm thử được.

### 8.2 Năm trạng thái hiện diện
`OccupancyEngine` quyết định:

| Trạng thái | Ý nghĩa | Khi nào |
|---|---|---|
| `Occupied` | Có người trong phòng | Có ai ở vùng `livingroom` hoặc `sofa` |
| `OtherArea` | Có người trong hình nhưng ở vùng khác; hoặc không thấy ai nhưng chưa có bằng chứng rời nhà | Mặc định khi không có người trong phòng và chưa có bằng chứng ra khỏi nhà |
| `ExitPending` | Đang chờ xác minh đi vắng | Có bằng chứng ra cửa, hết người, chưa đủ 30 giây |
| `AwayConfirmed` | Đã xác nhận đi vắng | Đủ 30 giây liên tục sau bằng chứng |
| `Unknown` | Mất dữ liệu | AI offline, camera lỗi, hoặc tin quá 10 giây chưa mới |

### 8.3 Bằng chứng rời nhà
Hai nguồn độc lập:
1. AI báo `transition` hướng `out` sang vùng **`door`** (cửa chính).
2. Cảm biến cửa MC-38 báo **mở**.

Sang `other_exit` **không phải** bằng chứng, vì người đó vẫn ở trong nhà. Nếu người quay lại, hoặc còn người khác trong hình, bằng chứng bị hủy.

### 8.4 Nguyên tắc "không đoán từ thiếu dữ liệu"
Khi camera hay AI mất kết nối, hệ thống ở `Unknown` và **không** kết luận nhà vắng. Báo vắng sai nguy hiểm hơn bỏ sót: nó có thể tắt thiết bị khi vẫn có người, làm người dùng mất tin tưởng.

### 8.5 Các lớp chống nhiễu
- **EmptyGrace (10 giây):** camera hay "mất" người ngồi yên 1–2 khung. Hết người chưa quá 10 giây vẫn giữ `Occupied`.
- **AwayResume (60 giây):** đã `AwayConfirmed`, dữ liệu mất ngắn rồi trở lại mà vẫn không có người thì giữ `AwayConfirmed`, không quay lại bước chờ.
- **Tính trạng thái mỗi lần đọc:** trạng thái phụ thuộc thời gian trôi (hết hạn, hết thời gian xác minh) nên phải tính lại theo đồng hồ hiện tại thay vì lưu sẵn.

### 8.6 Vì sao AI không tự kết luận, mà Hub mới kết luận
Kết luận "đi vắng" cần ghép **nhiều nguồn** (camera, cửa, thời gian). Nếu AI tự quyết thì trạng thái nằm ở hai nơi, khó kiểm thử. Để trạng thái ở **một chỗ** (Hub, mã C# thuần) thì kiểm thử được bằng đồng hồ giả, và mô phỏng chỉ cần gửi sự kiện giả để thay camera.

**Xem mã.** `src/SmartHomeAI.Core/Occupancy/OccupancyEngine.cs`, `OccupancyTypes.cs`.

---

## 9. Điều khiển tự động an toàn: chống dồn lệnh, bảo vệ rơ-le

### 9.1 Luật tự động
`AutomationEngine` chỉ **bật** thiết bị (tắt không phải việc của nó):
- **Đèn:** `Occupied` và lux nhỏ hơn 50 thì bật.
- **Quạt:** `Occupied`, có người **trên sofa** ở tư thế **nằm** liên tục tối thiểu 5 giây, và nhiệt độ từ 28 °C trở lên thì bật. Hoặc nhiệt độ thấp hơn nhưng có **luật thói quen đã duyệt** cho phép (Mục 17).
- **Lệnh tay luôn thắng lệnh tự động:** sau khi người dùng bật hoặc tắt tay, thiết bị đó không bị tự động động tới trong 30 phút.

### 9.2 Phát hiện "lệnh tay"
Bạn bấm nút trên ổ cắm hoặc dùng app hãng, ổ báo `state` đổi mà Hub **không** vừa gửi lệnh tương ứng trong 10 giây gần nhất. Hub kết luận đó là lệnh tay. Hub bỏ qua 5 giây sau khi thiết bị kết nối lại (đồng bộ) và 30 giây sau khi Hub khởi động (trạng thái cũ giữ lại).

### 9.3 Các lớp bảo vệ rơ-le và chống nhấp nháy
Mỗi lớp trả lời một nguy cơ khác nhau:

| Lớp | Giá trị | Chống điều gì |
|---|---|---|
| Cooldown tự động | 60 giây giữa hai lệnh tự động | Lệnh lặp liên tục |
| MinHold | giữ 10 giây giữa hai lần đổi tự động ngược nhau | Bật rồi tắt ngay |
| CommandGate | tối thiểu 3 giây giữa hai lệnh tới cùng thiết bị; lệnh thừa bị **gộp**, chỉ gửi lệnh cuối | Dồn lệnh từ nhiều nguồn |
| Bỏ lệnh trùng | lệnh giống trạng thái thiết bị đã báo | Gửi thừa |
| Giới hạn mỗi giờ | tối đa 6 lần đổi tự động/giờ, vượt thì khóa tự động 1 giờ | Vòng lặp lỗi |
| Gửi lại có lịch | sau 60 giây rồi 120 giây nếu chưa thấy `state` bật; hết thì ghi "không phản hồi" | Mất lệnh, nhưng không gửi mãi |
| StartupGrace | 30 giây đầu sau khởi động không tự động | Bật chồng khi thiết bị vốn đã bật |

Lệnh `manual` và `alert` không bị MinHold, vì đó là ý định rõ ràng.

### 9.4 Chẩn đoán
`Diagnose` trả lời "vì sao thiết bị này bật hoặc không bật lúc này?" bằng mã (ví dụ `FanTooCool`, `BlockedByManual`, `Cooldown`, `AutoLocked`). Không có tác dụng phụ; Dashboard dùng để giải thích.

### 9.5 Cảnh báo quên tắt (liên quan chặt)
`AlertManager` đếm thời gian từ lúc `AwayConfirmed`: cảnh báo đầu sau **2 phút**, nhắc lại mỗi **5 phút** (cùng mã), tùy chọn **tự tắt** sau 10 phút (mặc định **tắt**, và chỉ với thiết bị người dùng liệt kê). Cảnh báo bị **hủy** khi người về, khi đèn quạt đã tắt, hoặc khi mất dữ liệu quá 60 giây. Mất dữ liệu ngắn hơn 60 giây thì **đóng băng** bộ đếm rồi tiếp tục. Người dùng có thể **phản hồi đúng/sai** cho từng cảnh báo.

**Xem mã.** `src/SmartHomeAI.Core/Automation/AutomationEngine.cs`, `Alerts/AlertManager.cs`, `src/SmartHomeAI.Hub/CommandGate.cs`.

---

## 10. Lập trình đồng thời và tranh chấp luồng

**Luồng (thread).** Một chương trình có thể làm nhiều việc "cùng lúc" bằng nhiều luồng. Hub có ít nhất ba luồng cùng đụng vào bộ định tuyến `MessageRouter`:
1. luồng **nhận tin MQTT** (mỗi tin đến gọi `Handle`);
2. vòng **`Tick` mỗi giây** (xử lý thời gian trôi: hết hạn, đến hạn gửi lệnh chờ);
3. luồng **HTTP** (Dashboard gửi lệnh tay, phản hồi cảnh báo).

**Tranh chấp (race condition).** Hai luồng cùng đọc và sửa một dữ liệu không được bảo vệ (ví dụ một `Dictionary`) có thể làm dữ liệu hỏng hoặc cho kết quả sai tùy thứ tự chạy ngẫu nhiên. Hậu quả có thể là **lệnh bị nuốt hoặc gửi trùng tới rơ-le**. Lỗi loại này hiếm và rất khó tái hiện, và test chạy một luồng không bắt được.

**Khóa (lock).** Cho phép chỉ một luồng vào đoạn mã tại một thời điểm, luồng khác chờ. Dự án đặt **một khóa chung** (`_flow`) bao quanh mọi đường vào của `MessageRouter` (`Handle`, `Tick`, `TryPublishManual`, `Feedback`). Làm vậy được vì mỗi lần xử lý rất ngắn (tính trong bộ nhớ, gửi MQTT không chờ mạng đồng bộ) và tải của một căn nhà thấp.

**Test tranh chấp.** Một test chạy bốn luồng, mỗi luồng 2000 lần gọi xen kẽ và kiểm tra không có ngoại lệ.

**Xem mã.** `src/SmartHomeAI.Hub/MessageRouter.cs`, `tests/SmartHomeAI.Hub.Tests/HubTests.cs` (`RouterConcurrencyTests`).

---

## 11. Thời gian, kiểm thử và mô phỏng

### 11.1 Vì sao thời gian khó kiểm thử
Nhiều luật phụ thuộc thời gian (xác minh 30 giây, cảnh báo sau 2 phút). Nếu test phải chờ thật thì chạy hàng giờ.

### 11.2 TimeProvider và đồng hồ giả
Mã lõi **không** gọi đồng hồ hệ thống trực tiếp mà nhận một `TimeProvider`. Khi chạy thật là đồng hồ thật; khi test là **đồng hồ giả** mà test có thể "tua" tức thì. Nhờ đó kịch bản dài vài phút chạy trong mili-giây và cho kết quả lặp lại được.

### 11.3 Các tầng kiểm thử
1. **Test đơn vị** (lõi và Hub): từng hàm, từng luật, bằng đồng hồ giả.
2. **Test đầu-cuối (e2e):** chạy **Hub thật** cùng broker thật; chương trình mô phỏng phát tin như ESP32, ổ cắm và camera theo **kịch bản**. Thời gian được **nén** (rút ngắn cấu hình) để chạy nhanh.
3. **Phân tích dữ liệu** bằng MATLAB (Mục 16).

### 11.4 Các loại kịch bản
- **SC01–SC12:** kịch bản chức năng (ví dụ SC01 vào phòng tối thì đèn bật; SC07 đi vắng quên tắt thì cảnh báo; SC10 mất camera thì không kết luận vắng; SC12 đổi thói quen thì cập nhật đề xuất).
- **R01–R20:** kịch bản thực tế (mưa làm mát, mất điện, Hub khởi động lại, trẻ chạy nhảy, một ngày dài...).
- **N (nhiễu)** và các **bất biến an toàn**: bắn lệnh dồn dập rồi kiểm khoảng cách tối thiểu giữa hai lệnh.

### 11.5 Điều phải nhớ
Mô phỏng chạy qua **chỉ chứng minh logic đúng với đầu vào giả**. Đầu vào thật có nhiễu hơn, có độ trễ mạng và phần cứng. Vì thế vẫn phải kiểm bằng video và phần cứng thật. **[chưa chạy thật]**

**Xem mã.** `sim/`, `tests/e2e/`, `tests/SmartHomeAI.Tests/`, `docs/kich-ban-thuc-te.md`.

---

## 12. Máy chủ Hub: HTTP, API, khóa API, Blazor

### 12.1 HTTP và API
**HTTP** là giao thức trình duyệt dùng: gửi **yêu cầu** (GET để đọc, POST để ghi) và nhận **đáp ứng**. **API** là tập "cửa" mà chương trình khác gọi được. Hub dùng **ASP.NET Core Minimal API**:

| Đường dẫn | Việc |
|---|---|
| `GET /health` | Hub còn sống không, có bao nhiêu bản ghi log bị bỏ (`dbDropped`) |
| `GET /api/state` | Trạng thái hiện tại (hiện diện, thiết bị, cảm biến) |
| `GET /api/history`, `/api/events` | Lịch sử và nhật ký quyết định |
| `GET /api/alerts` | Danh sách cảnh báo |
| `POST /api/device/{id}` | Bật/tắt tay đèn hoặc quạt |
| `POST /api/mode` | Chuyển Auto/Manual |
| `POST /api/alerts/{id}/feedback` | Phản hồi đúng/sai |

Các lệnh **ghi** (POST) cần khóa API.

### 12.2 Khóa API
Máy khách gửi header `X-Api-Key: <khóa>`. Hub so khóa như sau: **băm cả hai vế bằng SHA-256** rồi so bằng hàm **so sánh hằng thời gian**. Hằng thời gian để kẻ tấn công không đo được "ký tự đầu đúng thì trả lời nhanh hơn một chút" rồi đoán dần khóa; băm để hai vế cùng độ dài và không lộ độ dài khóa.

**"Đóng mặc định".** Ngoài môi trường Development, nếu thiếu `Api:Key`, `Mqtt:User` hoặc `Mqtt:Password` thì Hub **từ chối khởi động** (thay vì chạy hở âm thầm).

**Chưa làm:** API đọc chưa cần khóa; Dashboard chưa có đăng nhập (tự gắn khóa khi gọi Hub); chưa có phân quyền người dùng.

### 12.3 Blazor Dashboard
**Blazor** viết giao diện web bằng C# thay vì JavaScript. Dashboard hiển thị trạng thái người, nhiệt độ, độ ẩm, lux, cửa, đèn quạt, chế độ Auto/Manual và chạy kịch bản mô phỏng. Hiện bản **tối thiểu**; chưa có biểu đồ, cấu hình ngưỡng, xem camera và duyệt thói quen.

**Xem mã.** `src/SmartHomeAI.Hub/Program.cs`, `ApiKey.cs`; `src/SmartHomeAI.Dashboard/`.

---

## 13. Cơ sở dữ liệu PostgreSQL và ghi log

**Cơ sở dữ liệu quan hệ** lưu dữ liệu thành các **bảng** (hàng và cột) và truy vấn bằng SQL. **PostgreSQL** là một hệ như vậy; dự án chạy nó bằng Docker.

**Các bảng ghi log** (do Hub tạo từ `sql/*.sql` khi khởi động):
- `sensor_reading`: nhiệt độ, độ ẩm, lux theo thời gian;
- `door_event`: cửa mở/đóng;
- `device_command_log`: mọi lệnh bật/tắt, kèm nguồn (`auto`, `manual`, `alert`);
- `ai_event`: sự kiện từ AI (kiểu JSONB, tức JSON lưu có cấu trúc);
- `occupancy_log`: mỗi lần trạng thái hiện diện đổi;
- `alert_log`: lúc báo, lúc hủy, lý do, phản hồi.

**Vì sao ghi log từ sớm.** Học thói quen và MATLAB cần **nhiều tuần dữ liệu**; bật muộn sẽ thiếu dữ liệu.

**Ghi bằng hàng đợi nền.** Hub không ghi thẳng vào DB trong lúc xử lý tin MQTT. Mỗi bản ghi vào một **hàng đợi** (tối đa 10.000), một luồng nền lấy ra ghi. Ưu điểm: DB chậm hoặc hỏng không làm Hub nghẽn hay bỏ lỡ tin. Đánh đổi: có thể **mất log**. Khi hàng đầy hoặc DB hỏng, bản ghi bị bỏ, được đếm (`dbDropped` trên `/health`) và có cảnh báo (ở bản ghi bỏ đầu tiên và cứ 100 bản ghi một lần). Quyết định an toàn không phụ thuộc DB.

**Mã cảnh báo duy nhất.** Mã cảnh báo có dạng `alert-{yyMMddHHmmss}-{n}`, trong đó tiền tố là giờ Hub khởi động, nên không trùng nhau giữa các lần khởi động.

**Xem mã.** `src/SmartHomeAI.Hub/EventLog.cs`, `docker-compose.yml`, `tools/export_csv.py`.

---

## 14. Triển khai: Docker, systemd, môi trường

**Docker.** Chạy chương trình trong "container" đóng gói sẵn. `docker compose up -d` dựng PostgreSQL và Mosquitto; profile `secure` dựng thêm broker bảo mật ở cổng 1884.

**systemd.** Trình quản lý dịch vụ của Linux. File `smarthome-hub.service` mô tả cách chạy Hub: chạy khi máy khởi động, **tự khởi động lại** khi chết (`Restart=always`, chờ 5 giây), chạy dưới một tài khoản riêng. Mục tiêu: chạy 24/7. **[chưa chạy thật]**

**Môi trường (Development, Production).** Cùng một chương trình có hai chế độ cấu hình. **Development:** thoải mái, có cấu hình mẫu và chuỗi kết nối DB cục bộ. **Production:** chặt chẽ, thiếu khóa hoặc tài khoản thì không khởi động, nghe `127.0.0.1`, bí mật nằm ở biến môi trường.

**Biến môi trường và tệp bí mật.** Bí mật (khóa API, mật khẩu MQTT, mật khẩu DB) **không** ghi trong kho mã. Production đặt trong `/etc/smarthome/hub.env` (systemd đọc qua `EnvironmentFile`). Tên có dạng `Api__Key`, `Mqtt__User`, `Mqtt__Password`, `ConnectionStrings__Postgres` (hai gạch dưới thay cho dấu `:` của cấu hình).

**Xem mã.** `infra/systemd/`, `docker-compose.yml`, `.env.example`, `docs/huong-dan-thuc-hien.md` (mục triển khai).

---

## 15. Thống kê cơ bản dùng trong đồ án

- **Trung bình, trung vị, tứ phân vị.** Sắp mẫu từ nhỏ đến lớn. **Trung vị** là giá trị ở giữa (ít bị ảnh hưởng bởi giá trị cực đoan). **Tứ phân vị dưới** (Q1) là giá trị mà 25% mẫu nằm dưới nó. Habit Engine dùng Q1 làm ngưỡng; script `b_threshold.m` dùng trung vị để ước lượng độc lập.
- **Độ phân tán.** Hiệu giữa mẫu lớn nhất và nhỏ nhất. Nếu phân tán quá lớn thì chưa có "thói quen" rõ.
- **Tương quan.** Số từ −1 đến 1 đo hai đại lượng cùng tăng giảm đến mức nào (ví dụ nhiệt độ và việc bật quạt). `a_survey.m` tính hệ số tương quan giữa nhiệt độ và bật quạt, lux và bật đèn.
- **Ma trận nhầm lẫn (confusion matrix).** Bảng 2×2 so dự đoán với sự thật. Ví dụ với hiện diện (hàng là sự thật):

| | Dự đoán: ở nhà | Dự đoán: vắng |
|---|---|---|
| Thật: ở nhà | đúng | **báo vắng sai** (nguy hiểm nhất) |
| Thật: vắng | **bỏ sót vắng** | đúng |

- **Độ chính xác (accuracy)** = số dự đoán đúng chia tổng. **Tỷ lệ báo vắng sai** = (thật ở nhà nhưng báo vắng) chia tổng số phút thật ở nhà. **Tỷ lệ bỏ sót** tính tương tự cho phía vắng.
- **Dương tính giả (false positive) và âm tính giả (false negative).** Dương tính giả là báo "có" khi thực tế "không" (báo vắng khi có người). Âm tính giả là báo "không" khi thực tế "có". Đề tài **ưu tiên giảm dương tính giả** của việc báo vắng.
- **Nhãn thật (ground truth).** "Sự thật" do bạn tự ghi (lúc thực sự ra hoặc vào nhà). Không có nhãn thì không tính được độ chính xác.

**Xem mã.** `matlab/a_survey.m`, `b_threshold.m`, `c_occupancy_eval.m`, `src/SmartHomeAI.Core/Habits/HabitEngine.cs`.

---

## 16. MATLAB và phân tích thực nghiệm

**MATLAB** là phần mềm tính toán và vẽ biểu đồ, phổ biến trong nghiên cứu. Dự án dùng nó **sau khi có dữ liệu** để phân tích, không nằm trong luồng chạy thời gian thực.

**Quy trình:**
1. Hub ghi dữ liệu vào PostgreSQL.
2. `tools/export_csv.py` xuất các bảng ra CSV.
3. Bạn tự tạo `truth_labels.csv` (cột `ts` và `truth` = `home` hoặc `away`), mỗi dòng là lúc trạng thái thật đổi.
4. Chạy `run_all.m` trong thư mục `matlab/`; kết quả (PNG, CSV) vào `matlab/out/`.

**Ba script:**
- `a_survey.m`: nhiệt độ và lux trung bình theo giờ; tương quan và tỷ lệ bật quạt theo nhiệt độ, bật đèn theo lux.
- `b_threshold.m`: ngưỡng thích nghi (trung vị nhiệt độ hoặc lux lúc bật tay) so với luật cố định (28 °C, 50 lux), đo số phút lệch hành vi người dùng và số lần bật/tắt sai khi có người.
- `c_occupancy_eval.m`: so `occupancy_log` với nhãn thật mỗi phút, ra accuracy, ma trận nhầm lẫn, tỷ lệ báo vắng sai và bỏ sót, phút `Unknown` đếm riêng.

**Hiện trạng trung thực.** Máy chưa có MATLAB hay Octave nên script chưa chạy thật; mới chỉ thử định dạng cột bằng **dữ liệu giả** (`tools/make_synthetic.py`, 14 ngày, nằm trong thư mục `SYNTHETIC_DU_LIEU_GIA`). Dữ liệu giả **không dùng cho kết luận luận văn**. Cần vài tuần dữ liệu thật và nhãn thật. **[chưa chạy thật]**

**Chưa có:** đánh giá nhận diện hành động (`ai_event`) theo từng sự kiện.

**Xem mã.** `matlab/`, `tools/`.

---

## 17. Học thói quen

**Mục tiêu.** Thay ngưỡng cố định (28 °C, 50 lux) bằng ngưỡng **học từ chính người dùng**.

**Cách học** (`HabitEngine`):
1. Mỗi lần người dùng **bật tay** quạt hoặc đèn, ghi một quan sát: thời gian, nhiệt độ, lux, tư thế, vùng.
2. Chỉ dùng lần bật tay (lệnh tự động phản ánh luật của hệ thống, học từ đó sẽ tự củng cố chính nó). Quạt chỉ tính lần người **nằm trên sofa**. Xét trong 30 ngày, tối đa 30 mẫu gần nhất.
3. Cần **ít nhất 5 mẫu**; nếu mẫu quá phân tán (nhiệt độ chênh quá 3 °C, lux chênh quá 40) thì **không** đề xuất.
4. Ngưỡng đề xuất = **tứ phân vị dưới** của mẫu: người dùng thường bật quạt lúc bắt đầu thấy nóng, tức đầu thấp của dải họ chịu được.
5. Luật mới ở trạng thái **Proposed**. Người dùng **duyệt** (Approved), **sửa ngưỡng**, hoặc **tắt** (Disabled). **Chỉ luật đã duyệt mới được dùng.** Thứ người dùng đã từ chối thì không đề xuất lại.

**Gắn với Automation.** `HabitEngine` cài đặt giao diện `IHabitAdvisor`; khi nhiệt độ thấp hơn 28 °C mà người đang nằm trên sofa, Automation hỏi "luật đã duyệt có cho bật quạt không?".

**Đây là học máy không?** Là **học thống kê đơn giản** (ước lượng ngưỡng), không phải mạng nơ-ron. Đủ cho một ngưỡng, dễ giải thích và có người duyệt nên an toàn.

**Hiện trạng.** `HabitEngine` đã có và có test, nhưng **chưa nối vào Hub và Dashboard** (chưa có giao diện duyệt, chưa lưu DB). Cần dữ liệu thật nhiều tuần. **[chưa chạy thật]**

**Xem mã.** `src/SmartHomeAI.Core/Habits/HabitEngine.cs`.

---

## 18. Từ đầu đến cuối: một lệnh bật quạt đi qua những đâu

Tình huống: 12:00:00, bạn đang nằm trên sofa, trời 31 °C, quạt đang tắt.

1. **Camera → AI.** Camera phát khung hình qua RTSP. AI cứ 0,25 giây phân tích một khung: phát hiện một hộp người, tracker gán `track_id=3`, tư thế (sau bỏ phiếu 8 khung) là `lying`, điểm tâm nằm trong đa giác `sofa`.
2. **AI → broker.** Mỗi giây AI gửi: `home/ai/livingroom/state` với `persons=[{track_id:3, zone:"sofa", posture:"lying", conf:0.82}]`.
3. **ESP32 → broker.** Mỗi 10 giây ESP32 gửi `home/sensor/esp1/env` với `temp_c:31.4, lux:18`.
4. **Broker → Hub.** Hub đã đăng ký `home/#`, nhận cả hai tin. `MqttParser` kiểm định dạng; đúng thì chuyển cho `MessageRouter.Handle` (đi qua khóa `_flow`).
5. **Occupancy.** Có người ở vùng `sofa` thuộc vùng phòng nên trạng thái là `Occupied`.
6. **Automation.** Điều kiện quạt: `Occupied`, có người trên sofa, tư thế `lying`, nằm đủ 5 giây, nhiệt độ 31,4 ≥ 28. Không bị chặn bởi lệnh tay (30 phút), cooldown, giới hạn giờ hay `StartupGrace` nên chẩn đoán là `WillTurnOn`.
7. **CommandGate.** Khoảng cách từ lệnh trước tới thiết bị này đủ 3 giây nên cho gửi (nếu chưa đủ, lệnh bị giữ và gộp).
8. **Hub → broker.** Hub gửi `home/device/fan/set` với `{"on":true,"source":"auto"}` và ghi lệnh vào hàng đợi log PostgreSQL.
9. **Broker → ổ cắm quạt.** Adapter P100 nhận, bật rơ-le, rồi gửi `home/device/fan/state` với `{"on":true}` (retained).
10. **Hub xác nhận.** Hub nhận `state=on`, thôi chờ gửi lại, ghi vào nhật ký quyết định ("Quạt: tự bật vì bạn nằm sofa và 31,4 °C"). Dashboard hiện quạt đang bật.
11. **Sau đó.** Nếu bạn tắt quạt bằng tay, ổ báo `state=off` mà Hub không vừa gửi lệnh nào, nên Hub coi đó là lệnh tay và 30 phút sau đó không tự bật lại quạt.

Mọi bước từ 2 đến 10 đã được kiểm bằng mô phỏng; mô phỏng thay đúng ba nguồn camera, ESP32, ổ cắm. **[chưa chạy thật với phần cứng thật]**

---

## 19. Bảng thuật ngữ A–Z

| Thuật ngữ | Giải thích ngắn |
|---|---|
| ACL | Danh sách quyền: tài khoản nào được đọc hay ghi topic nào |
| Actuator | Bộ phận tác động (ổ cắm, buzzer) |
| Adapter | Chương trình trung gian dịch giao thức hãng sang MQTT |
| API | Tập "cửa" để chương trình khác gọi vào |
| Baseline | Phương án đơn giản làm mốc so sánh |
| Blazor | Công nghệ viết giao diện web bằng C# |
| Bounding box | Hộp chữ nhật bao quanh một đối tượng trong ảnh |
| Broker | Trạm trung gian MQTT nhận và phân phát tin |
| COCO-17 | Chuẩn 17 khớp xương người |
| Confidence (conf) | Độ tin cậy 0–1 của một phát hiện |
| Cooldown | Thời gian chờ tối thiểu giữa hai lần làm cùng việc |
| CNN | Mạng nơ-ron tích chập, dùng cho xử lý ảnh |
| Detection | Tìm vị trí đối tượng trong ảnh |
| Docker | Công cụ chạy chương trình trong container |
| ESP32 | Vi điều khiển có Wi-Fi |
| fps | Số khung hình mỗi giây |
| FSM | Máy trạng thái hữu hạn |
| Gộp lệnh (coalesce) | Hợp nhiều lệnh sát nhau thành một, chỉ gửi lệnh cuối |
| Ground truth | Nhãn sự thật do người ghi để chấm độ chính xác |
| HOG | Biểu đồ hướng gradient, đặc trưng hình dáng |
| Hub | Máy chủ trung tâm ASP.NET Core, bộ não hệ thống |
| IoT | Internet vạn vật |
| IoU | Tỷ lệ giao trên hợp của hai hộp |
| JSON | Định dạng dữ liệu dạng khóa-giá trị |
| Lock | Khóa cho phép một luồng vào mã tại một thời điểm |
| Lux | Đơn vị đo độ sáng |
| LWT | Tin "di chúc" broker phát khi nguồn chết đột ngột |
| MC-38 | Cảm biến cửa từ |
| MinHold | Thời gian tối thiểu giữ một trạng thái |
| MQTT | Giao thức nhắn tin đăng-nhận nhẹ cho IoT |
| NMS | Loại bỏ hộp trùng, giữ hộp tốt nhất |
| Occupancy | Trạng thái có người trong phòng hay không |
| ONNX | Định dạng mô hình AI có thể chạy bằng nhiều bộ chạy |
| OpenCV | Thư viện xử lý ảnh |
| Payload | Nội dung của một tin MQTT |
| Pixel | Điểm ảnh |
| Pose | Tư thế, ở đây là vị trí các khớp xương |
| PostgreSQL | Hệ cơ sở dữ liệu quan hệ |
| Publish/Subscribe | Gửi tin lên topic / đăng ký nhận tin từ topic |
| QoS | Mức đảm bảo giao tin MQTT (0, 1, 2) |
| Race condition | Lỗi do hai luồng tranh nhau sửa một dữ liệu |
| Ray casting | Thuật toán xét điểm nằm trong đa giác |
| Retained | Tin được broker giữ lại làm trạng thái cuối |
| Rơ-le | Công tắc cơ khí điều khiển bằng điện |
| RTSP | Giao thức phát luồng video từ camera IP |
| Schema | Bản mô tả hình dạng hợp lệ của JSON |
| SHA-256 | Hàm băm một chiều |
| SHT30 | Cảm biến nhiệt độ và độ ẩm |
| BH1750 | Cảm biến ánh sáng |
| SVM | Bộ phân loại dùng cùng HOG |
| systemd | Trình quản lý dịch vụ Linux |
| TLS | Mã hóa đường truyền |
| Topic | "Nhãn ngăn" phân cấp của MQTT |
| Tracking | Gán số hiệu ổn định cho một người qua các khung |
| Transition | Sự kiện một người đổi vùng |
| Unknown | Trạng thái mất dữ liệu, không kết luận gì |
| YOLO | Họ mô hình phát hiện đối tượng bằng CNN |
| Zone | Vùng đa giác trên khung hình |
