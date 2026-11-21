<div align="center">

**[TÊN TRƯỜNG ĐẠI HỌC]**

**[TÊN KHOA]**

---

# ĐỒ ÁN TỐT NGHIỆP

## NGHIÊN CỨU, THIẾT KẾ HỆ THỐNG NHÀ THÔNG MINH TỰ ĐỘNG ĐIỀU KHIỂN THIẾT BỊ THEO HIỆN DIỆN VÀ TƯ THẾ CỦA NGƯỜI SỬ DỤNG CAMERA AI

Giảng viên hướng dẫn: **[Học hàm, học vị, họ tên GVHD]**

Sinh viên thực hiện: **[Họ tên sinh viên]**

Mã số sinh viên: **[MSSV]**

Lớp: **[Lớp]** — Ngành: **[Ngành học]**

**[Địa danh], tháng [..] năm 2026**

</div>

---

## LỜI CAM ĐOAN

Đồ án tốt nghiệp với đề tài "Nghiên cứu, thiết kế hệ thống nhà thông minh tự động điều khiển thiết bị theo hiện
diện và tư thế của người sử dụng camera AI" là công trình được thực hiện dưới sự hướng dẫn của thầy/cô
[họ tên GVHD]. Các số liệu và kết quả nêu trong đồ án là trung thực, được ghi lại trong quá trình thực hiện đề
tài. Các tài liệu được sử dụng trong quá trình thực hiện được liệt kê ở phần Tài liệu tham khảo.

Sinh viên thực hiện xin chịu hoàn toàn trách nhiệm về nội dung của đồ án.

<div align="right">

[Địa danh], ngày … tháng … năm 2026

Sinh viên thực hiện

[Họ tên sinh viên]

</div>

## LỜI CẢM ƠN

Em xin chân thành cảm ơn thầy/cô [họ tên GVHD] đã hướng dẫn, góp ý và giúp em tháo gỡ những khó khăn trong
quá trình thực hiện đồ án.

Em cũng xin cảm ơn quý thầy cô Khoa [tên khoa], Trường [tên trường] đã giảng dạy và trang bị cho em những kiến
thức cần thiết trong suốt quá trình học tập. Xin cảm ơn gia đình và bạn bè đã động viên, hỗ trợ em trong thời
gian làm đồ án.

Do thời gian và kinh nghiệm còn hạn chế, đồ án vẫn còn những thiếu sót. Em mong nhận được ý kiến góp ý của
quý thầy cô để tiếp tục hoàn thiện đề tài.

Xin chân thành cảm ơn!

## TÓM TẮT ĐỒ ÁN

Đồ án trình bày việc thiết kế và xây dựng một hệ thống nhà thông minh cho phòng khách. Hệ thống sử dụng một
camera IP kết hợp với mô hình ước lượng tư thế người để nhận biết trong phòng có người hay không, người đang ở
khu vực nào và đang đứng, ngồi hay nằm. Khối cảm biến được thiết kế với ESP32 để đo nhiệt độ, độ ẩm, cường độ ánh sáng và theo dõi trạng
thái cửa. Từ các thông tin thu được, bộ xử lý trung tâm tự động bật đèn khi có người và trời tối, bật quạt khi
có người nằm nghỉ và trời nóng, phát cảnh báo khi cả nhà đã đi vắng mà thiết bị vẫn còn bật.

Hệ thống gồm khối AI viết bằng Python, bộ xử lý trung tâm và giao diện web viết bằng C#, firmware ESP32 viết
bằng C++. Các khối trao đổi dữ liệu với nhau qua giao thức MQTT. Kết quả đạt được: hệ thống đã chạy được với
camera Tapo C222 và ổ cắm Tapo P100 thật, 166 ca kiểm thử đơn vị đều đạt, 20/20 kịch bản thực tế chạy trên mô
phỏng đều đạt. Phần mạch ESP32 và việc đo độ chính xác nhận diện tư thế chưa hoàn thành.

**Từ khóa:** nhà thông minh, IoT, MQTT, ESP32, ước lượng tư thế, YOLO.

## MỤC LỤC

- Lời cam đoan
- Lời cảm ơn
- Tóm tắt đồ án
- Danh mục từ viết tắt
- Danh mục bảng
- Danh mục hình
- Mở đầu
- Chương 1. Cơ sở lý thuyết
- Chương 2. Phân tích và thiết kế hệ thống
- Chương 3. Xây dựng hệ thống
- Chương 4. Kết quả thử nghiệm và đánh giá
- Kết luận và hướng phát triển
- Tài liệu tham khảo
- Phụ lục

## DANH MỤC TỪ VIẾT TẮT

| Từ viết tắt | Tiếng Anh | Ý nghĩa |
|---|---|---|
| AI | Artificial Intelligence | Trí tuệ nhân tạo |
| API | Application Programming Interface | Giao diện lập trình ứng dụng |
| CSDL | | Cơ sở dữ liệu |
| GPIO | General Purpose Input/Output | Chân vào/ra đa dụng |
| I2C | Inter-Integrated Circuit | Chuẩn giao tiếp nối tiếp hai dây |
| IoT | Internet of Things | Internet vạn vật |
| JSON | JavaScript Object Notation | Định dạng trao đổi dữ liệu |
| k-NN | k-Nearest Neighbors | k láng giềng gần nhất |
| LAN | Local Area Network | Mạng cục bộ |
| LWT | Last Will and Testament | Tin "di chúc" của MQTT |
| MQTT | Message Queuing Telemetry Transport | Giao thức nhắn tin cho IoT |
| NMS | Non-Maximum Suppression | Loại bỏ hộp trùng |
| ONNX | Open Neural Network Exchange | Định dạng mô hình mạng nơ-ron |
| RTSP | Real Time Streaming Protocol | Giao thức truyền hình ảnh thời gian thực |

## DANH MỤC BẢNG

- Bảng 1.1. So sánh MQTT và HTTP trong bài toán của đồ án
- Bảng 2.1. Yêu cầu chức năng của hệ thống
- Bảng 2.2. Danh mục linh kiện
- Bảng 2.3. Sơ đồ nối chân ESP32
- Bảng 2.4. Các chủ đề MQTT của hệ thống
- Bảng 2.5. Năm trạng thái hiện diện
- Bảng 2.6. Các lớp bảo vệ khi điều khiển thiết bị
- Bảng 2.7. Các bảng trong cơ sở dữ liệu
- Bảng 3.1. Môi trường phát triển
- Bảng 3.2. Khó khăn gặp phải và cách giải quyết
- Bảng 4.1. Kết quả kiểm thử đơn vị
- Bảng 4.2. Kết quả 12 kịch bản cơ bản
- Bảng 4.3. Kết quả 20 kịch bản thực tế
- Bảng 4.4. So sánh các mô hình trên camera thật
- Bảng 4.5. Đối chiếu mục tiêu và kết quả

## DANH MỤC HÌNH

- Hình 2.1. Sơ đồ khối tổng quát của hệ thống
- Hình 2.2. Chuỗi xử lý của khối AI
- Hình 2.3. Sơ đồ chuyển trạng thái hiện diện
- Hình 3.1. Lưu đồ thuật toán firmware ESP32
- Hình 3.2. Giao diện trang chính
- Hình 3.3. Giao diện trang cài đặt và chức năng dạy tư thế
- Hình 4.1. Kết quả nhận diện người ngồi và nằm trên camera thật

---

# MỞ ĐẦU

## 1. Lý do chọn đề tài

Các thiết bị như ổ cắm Wi-Fi, đèn thông minh và camera IP ngày càng được sử dụng nhiều trong gia đình. Với
những thiết bị này, người dùng có thể theo dõi hoặc bật tắt từ xa. Tuy nhiên, nếu chỉ điều khiển bằng điện thoại
hoặc đặt lịch cố định, người dùng vẫn cần tự thao tác và có thể quên tắt thiết bị khi ra khỏi nhà.

Một cách tự động hóa là sử dụng cảm biến chuyển động để bật đèn khi có người. Tuy nhiên, cảm biến PIR có thể
khó phát hiện người ngồi yên hoặc nằm nghỉ trong thời gian dài. Camera kết hợp với mô hình ước lượng tư thế có
thể cung cấp thêm thông tin về vị trí và tư thế của người trong phòng, từ đó hỗ trợ hệ thống đưa ra quyết định
phù hợp hơn.

Từ ý tưởng đó, em lựa chọn đề tài "Nghiên cứu, thiết kế hệ thống nhà thông minh tự động điều khiển thiết bị
theo hiện diện và tư thế của người sử dụng camera AI". Đồ án tập trung xây dựng và thử nghiệm hệ thống trong
phạm vi một phòng, sử dụng camera IP và máy tính xử lý không có card đồ họa rời.

## 2. Mục tiêu của đề tài

Mục tiêu của đề tài là thiết kế và xây dựng một hệ thống nhà thông minh cho một phòng, có khả năng tự động điều
khiển đèn, quạt theo sự hiện diện và tư thế của người, đồng thời cảnh báo khi quên tắt thiết bị lúc đi vắng.
Cụ thể:

- Đọc được hình ảnh từ camera IP, phát hiện người, xác định khu vực và tư thế của người trong phòng.
- Thu thập được nhiệt độ, độ ẩm, cường độ ánh sáng và trạng thái cửa bằng ESP32.
- Xây dựng được bộ xử lý trung tâm có nhiệm vụ tổng hợp dữ liệu và ra quyết định điều khiển.
- Điều khiển được ổ cắm Wi-Fi qua mạng nội bộ.
- Xây dựng được giao diện web để theo dõi, điều khiển và cài đặt hệ thống.
- Xây dựng được các kịch bản để kiểm thử hệ thống.

## 3. Đối tượng và phạm vi nghiên cứu

Đối tượng nghiên cứu:

- Giao thức MQTT.
- Vi điều khiển ESP32 và các cảm biến SHT30, BH1750, MC-38.
- Các mô hình ước lượng tư thế người YOLOv8-pose, YOLO26-pose, RTMO.
- Phương pháp xác định trạng thái hiện diện bằng máy trạng thái.

Phạm vi nghiên cứu:

- Hệ thống áp dụng cho một phòng với một camera.
- Thiết bị được điều khiển là thiết bị bật/tắt qua ổ cắm (đèn, quạt).
- Hệ thống không nhận dạng danh tính của từng người.
- Phần học thói quen của người dùng mới dừng ở mức thuật toán, chưa được tích hợp vào Hub.

## 4. Phương pháp nghiên cứu

- Phương pháp nghiên cứu lý thuyết: tìm hiểu tài liệu về MQTT, ESP32, các mô hình ước lượng tư thế người.
- Phương pháp mô phỏng: viết chương trình giả lập ESP32, ổ cắm và camera để xây dựng phần mềm khi chưa có
  phần cứng.
- Phương pháp thực nghiệm: lắp đặt camera và ổ cắm trong phòng, chạy thử và hiệu chỉnh.

## 5. Ý nghĩa khoa học và thực tiễn

Đề tài tìm hiểu cách kết hợp thông tin từ camera, cảm biến cửa và thời gian chờ để xác định trạng thái hiện
diện. Đây là cơ sở để thử nghiệm các tình huống điều khiển và cảnh báo trong phòng.

Về ứng dụng, hệ thống được xây dựng từ các thiết bị phổ thông và xử lý hình ảnh trong mạng nội bộ. Kết quả của
đồ án có thể dùng làm cơ sở để tiếp tục hoàn thiện một mô hình nhà thông minh phù hợp với điều kiện thực tế.

## 6. Bố cục của đồ án

Ngoài phần mở đầu và kết luận, nội dung đồ án gồm 4 chương:

- Chương 1: Cơ sở lý thuyết.
- Chương 2: Phân tích và thiết kế hệ thống.
- Chương 3: Xây dựng hệ thống.
- Chương 4: Kết quả thử nghiệm và đánh giá.

---

# CHƯƠNG 1. CƠ SỞ LÝ THUYẾT

Chương này trình bày các kiến thức cơ sở được sử dụng trong đồ án, bao gồm: tổng quan về IoT và nhà thông minh,
giao thức MQTT, vi điều khiển ESP32 và các cảm biến, camera IP, bài toán phát hiện người và ước lượng tư thế,
thuật toán k láng giềng gần nhất, máy trạng thái hữu hạn và các công nghệ phần mềm.

## 1.1. Tổng quan về IoT và nhà thông minh

### 1.1.1. Khái niệm IoT

Internet vạn vật (Internet of Things - IoT) là mạng lưới các thiết bị được gắn cảm biến, phần mềm và có khả năng
kết nối mạng để thu thập và trao đổi dữ liệu với nhau. Một hệ thống IoT thường gồm 3 thành phần chính:

- Khối cảm biến: đo các đại lượng vật lý như nhiệt độ, độ ẩm, ánh sáng.
- Khối xử lý: nhận dữ liệu từ cảm biến, xử lý và đưa ra quyết định.
- Khối chấp hành: sử dụng các thiết bị như rơ-le, còi hoặc động cơ để thực hiện lệnh điều khiển.

### 1.1.2. Nhà thông minh

Nhà thông minh (Smart Home) là ngôi nhà được trang bị các thiết bị điện, điện tử có thể điều khiển tự động hoặc
bán tự động. Trong phạm vi đồ án, có thể xét một số mức độ điều khiển thường gặp:

- Điều khiển từ xa: người dùng điều khiển thiết bị qua điện thoại.
- Tự động theo lịch hoặc theo một cảm biến: ví dụ hẹn giờ bật đèn, bật đèn khi có chuyển động.
- Tự động theo ngữ cảnh: hệ thống nhận biết được tình huống đang diễn ra trong nhà để điều khiển.

Hệ thống trong đồ án hướng đến điều khiển theo ngữ cảnh, dựa trên thông tin thu thập được trong phòng.

## 1.2. Giao thức MQTT

### 1.2.1. Giới thiệu

MQTT (Message Queuing Telemetry Transport) là giao thức truyền tin theo mô hình xuất bản/đăng ký
(publish/subscribe), được thiết kế cho các thiết bị có tài nguyên hạn chế và đường truyền không ổn định [1].
Các thành phần chính của MQTT gồm:

- Broker: máy chủ trung gian, nhận tin từ thiết bị gửi và chuyển đến các thiết bị nhận.
- Publisher: thiết bị gửi tin.
- Subscriber: thiết bị nhận tin.
- Topic (chủ đề): địa chỉ của tin, có dạng phân cấp, ví dụ `home/sensor/esp1/env`.

Nguyên lý hoạt động: thiết bị gửi không gửi tin trực tiếp cho thiết bị nhận mà gửi lên broker kèm theo một chủ
đề. Thiết bị nào cần nhận tin thuộc chủ đề nào thì đăng ký chủ đề đó với broker. Khi có tin mới, broker sẽ
chuyển tin đến tất cả các thiết bị đã đăng ký.

### 1.2.2. Một số cơ chế của MQTT được sử dụng

- QoS (Quality of Service): mức 0 gửi tin không cần xác nhận; mức 1 gửi lại khi cần để tin được chuyển đến
  ít nhất một lần.
- Retained: broker lưu lại tin cuối cùng của một chủ đề và gửi ngay cho thiết bị mới đăng ký. Trong đồ án cơ
  chế này được dùng cho trạng thái cửa và trạng thái thiết bị.
- LWT (Last Will and Testament): thiết bị đăng ký trước với broker một tin báo mất kết nối. Khi thiết bị bị mất
  kết nối đột ngột thì broker sẽ tự động phát tin này. Nhờ đó bộ xử lý trung tâm biết được thiết bị nào đang
  không hoạt động.

### 1.2.3. So sánh với HTTP

**Bảng 1.1. So sánh MQTT và HTTP trong bài toán của đồ án**

| Tiêu chí | MQTT | HTTP |
|---|---|---|
| Mô hình | Xuất bản/đăng ký qua broker | Yêu cầu/đáp ứng trực tiếp |
| Thiết bị cần biết địa chỉ nhau | Không, chỉ cần biết broker | Có |
| Phát hiện thiết bị mất kết nối | Có sẵn (LWT) | Phải tự làm |
| Phù hợp vi điều khiển | Tốt, gói tin nhỏ | Nặng hơn |
| Một nguồn, nhiều nơi nhận | Tự nhiên | Phải gửi nhiều lần |

Từ bảng so sánh trên, MQTT được chọn để truyền dữ liệu giữa các khối trong hệ thống, HTTP chỉ được dùng giữa
giao diện web và bộ xử lý trung tâm.

## 1.3. Vi điều khiển ESP32 và các cảm biến

### 1.3.1. ESP32 DevKit V1

ESP32 là dòng vi điều khiển của hãng Espressif có tích hợp sẵn Wi-Fi và Bluetooth [6]. Đồ án sử dụng bo mạch
ESP32 DevKit V1 loại 30 chân.

Thông số kỹ thuật chính:

- Vi xử lý: 2 nhân, tốc độ tối đa 240 MHz.
- Kết nối không dây: Wi-Fi 2.4 GHz, Bluetooth.
- Điện áp hoạt động của chân GPIO: 3,3 V.
- Nguồn cấp: 5 V qua cổng USB hoặc chân VIN.
- Hỗ trợ các chuẩn giao tiếp: I2C, SPI, UART.

ESP32 được lựa chọn vì có giá thành rẻ, có sẵn Wi-Fi, có mạch nạp qua cổng USB và có nhiều thư viện hỗ trợ.

### 1.3.2. Cảm biến nhiệt độ, độ ẩm SHT30

SHT30 là cảm biến đo nhiệt độ và độ ẩm của hãng Sensirion [7].

Thông số kỹ thuật chính:

- Điện áp hoạt động: 2,4 V đến 5,5 V.
- Chuẩn giao tiếp: I2C, địa chỉ mặc định 0x44.
- Dải đo nhiệt độ: -40 °C đến 125 °C.
- Dải đo độ ẩm: 0 đến 100 %RH.

Mỗi lần đo cảm biến trả về 6 byte, gồm 2 byte nhiệt độ, 1 byte kiểm tra CRC, 2 byte độ ẩm và 1 byte kiểm tra
CRC. Giá trị nhiệt độ và độ ẩm được tính theo công thức của nhà sản xuất:

- T (°C) = -45 + 175 × S_T / 65535
- RH (%) = 100 × S_RH / 65535

Trong đó S_T và S_RH là giá trị 16 bit đọc được từ cảm biến.

### 1.3.3. Cảm biến cường độ ánh sáng BH1750

BH1750 là cảm biến đo cường độ ánh sáng của hãng ROHM, cho kết quả trực tiếp theo đơn vị lux [8].

Thông số kỹ thuật chính:

- Điện áp hoạt động: 2,4 V đến 3,6 V.
- Chuẩn giao tiếp: I2C, địa chỉ mặc định 0x23.
- Dải đo: 1 đến 65535 lux.

Cường độ ánh sáng được tính theo công thức: lux = giá trị đọc được / 1,2. Do SHT30 và BH1750 có địa chỉ I2C
khác nhau nên hai cảm biến được nối chung trên một đường I2C.

### 1.3.4. Cảm biến cửa MC-38

MC-38 là công tắc từ, gồm một công tắc lưỡi gà và một nam châm. Nguyên lý hoạt động: khi nam châm ở gần công
tắc (cửa đóng) thì hai tiếp điểm chạm vào nhau, mạch kín. Khi nam châm rời xa (cửa mở) thì mạch hở. Cảm biến
chỉ cần nối vào một chân GPIO có điện trở kéo lên.

### 1.3.5. Còi Buzzer 5V

Đồ án sử dụng còi loại chủ động, chỉ cần cấp điện áp 5 V là còi kêu. Do dòng tiêu thụ của còi lớn hơn dòng mà
chân GPIO của ESP32 cấp được nên còi được điều khiển thông qua một transistor NPN.

## 1.4. Camera IP và giao thức RTSP

Camera IP là loại camera truyền hình ảnh qua mạng. Đồ án sử dụng camera TP-Link Tapo C222, độ phân giải 4MP,
kết nối Wi-Fi, có hồng ngoại để quan sát ban đêm và hỗ trợ giao thức RTSP.

RTSP (Real Time Streaming Protocol) là giao thức điều khiển phiên truyền dữ liệu đa phương tiện theo thời gian thực. Máy tính đọc hình từ
camera thông qua một địa chỉ có dạng:

`rtsp://tài_khoản:mật_khẩu@địa_chỉ_IP:554/stream2`

Camera Tapo C222 có 2 luồng hình: luồng chính (stream1) có độ phân giải cao và luồng phụ (stream2) có độ phân
giải 640×360. Đồ án sử dụng luồng phụ để giảm băng thông và chi phí xử lý khi đọc ảnh. Khung hình sau đó được chuẩn bị
về kích thước 640×640 trước khi đưa vào mô hình.

## 1.5. Phát hiện người và ước lượng tư thế

### 1.5.1. Bài toán

Phát hiện đối tượng (object detection) là bài toán xác định vị trí của đối tượng trong ảnh, kết quả là một hộp
chữ nhật bao quanh đối tượng (bounding box) kèm theo độ tin cậy. Ước lượng tư thế (pose estimation) là bài toán
xác định vị trí các khớp trên cơ thể người. Theo bộ dữ liệu COCO, cơ thể người được biểu diễn bằng 17 điểm khớp
gồm: mũi, 2 mắt, 2 tai, 2 vai, 2 khuỷu tay, 2 cổ tay, 2 hông, 2 đầu gối và 2 cổ chân [3].

### 1.5.2. Các mô hình được sử dụng

- HOG kết hợp SVM [5]: là phương pháp truyền thống, chạy nhanh nhưng chỉ phát hiện tốt người đang đứng và không
  cho thông tin về khớp. Phương pháp này chỉ được dùng để thử nghiệm ở giai đoạn đầu.
- YOLOv8-pose [2]: là mô hình mạng nơ-ron tích chập, xử lý toàn bộ ảnh trong một lần chạy. Đầu ra của mô hình
  có kích thước [1, 56, 8400], trong đó mỗi cột gồm 4 giá trị của hộp bao, 1 giá trị độ tin cậy và 51 giá trị
  của 17 khớp (mỗi khớp gồm tọa độ x, y và độ tin cậy). Sau khi có đầu ra cần thực hiện thêm bước loại bỏ các
  hộp trùng nhau (NMS).
- YOLO26-pose: là phiên bản mới hơn, đã tích hợp bước loại bỏ hộp trùng vào trong mô hình. Đầu ra có kích thước
  [1, 300, 57].
- RTMO [4]: là mô hình ước lượng tư thế nhiều người của nhóm OpenMMLab. Đầu ra gồm 2 mảng: mảng hộp bao và mảng
  khớp.

Các mô hình ước lượng tư thế được sử dụng ở định dạng ONNX và chạy bằng thư viện ONNX Runtime trên CPU [12].

## 1.6. Thuật toán k láng giềng gần nhất

k láng giềng gần nhất (k-Nearest Neighbors, k-NN) là thuật toán phân loại đơn giản [13]. Nguyên lý hoạt động:
để phân loại một mẫu mới, thuật toán tính khoảng cách từ mẫu đó đến tất cả các mẫu đã biết nhãn, chọn ra k mẫu
gần nhất và lấy nhãn xuất hiện nhiều nhất làm kết quả.

Ưu điểm của thuật toán là không cần huấn luyện, khi thêm hoặc xóa mẫu thì có tác dụng ngay. Nhược điểm là khi
số mẫu lớn thì tốc độ tính toán chậm. Trong phạm vi đồ án, số lượng mẫu dự kiến chỉ từ vài chục đến vài trăm nên chi phí tính toán được giữ ở mức
thấp.

## 1.7. Máy trạng thái hữu hạn

Máy trạng thái hữu hạn (Finite State Machine) là mô hình trong đó hệ thống tại mỗi thời điểm chỉ ở một trạng
thái trong một tập hữu hạn các trạng thái. Hệ thống chuyển từ trạng thái này sang trạng thái khác khi có sự
kiện xảy ra hoặc khi hết một khoảng thời gian. Mô hình này giúp cho việc mô tả và kiểm tra hoạt động của hệ
thống được rõ ràng.

## 1.8. Các công nghệ phần mềm sử dụng

- .NET 10 và ASP.NET Core [9]: dùng để xây dựng bộ xử lý trung tâm.
- Blazor: dùng để xây dựng giao diện web bằng ngôn ngữ C#.
- Python và OpenCV [14]: dùng để xây dựng khối AI, đọc hình từ camera và xử lý ảnh.
- Mosquitto [10]: phần mềm MQTT broker mã nguồn mở.
- PostgreSQL [11]: hệ quản trị cơ sở dữ liệu, dùng để lưu lịch sử hoạt động.
- Docker: dùng để đóng gói và triển khai hệ thống.
- PlatformIO: môi trường lập trình cho ESP32.

## 1.9. Kết luận chương 1

Chương 1 đã giới thiệu các kiến thức và công nghệ được sử dụng trong đồ án. Trên cơ sở đó, chương 2 trình bày
cách phân chia các khối và thiết kế hệ thống.

---

# CHƯƠNG 2. PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG

Chương này trình bày yêu cầu của hệ thống, sơ đồ khối tổng quát và thiết kế của từng khối trong hệ thống.

## 2.1. Yêu cầu của hệ thống

### 2.1.1. Yêu cầu chức năng

**Bảng 2.1. Yêu cầu chức năng của hệ thống**

| STT | Yêu cầu | Mô tả |
|---|---|---|
| 1 | Nhận biết người | Phát hiện, theo dõi nhiều người; xác định khu vực (phòng khách, chỗ nằm nghỉ, cửa chính, lối sang khu khác) và tư thế (đứng, ngồi, nằm, di chuyển) |
| 2 | Theo dõi hiện diện | Kết luận phòng có người, người sang khu khác, đang chờ xác minh đi vắng, đã đi vắng hoặc chưa rõ |
| 3 | Thu thập môi trường | Đo nhiệt độ, độ ẩm, ánh sáng mỗi 10 giây; báo trạng thái cửa ngay khi thay đổi |
| 4 | Tự động điều khiển | Có người và tối thì bật đèn; nằm nghỉ và nóng thì bật quạt |
| 5 | Ưu tiên lệnh tay | Người dùng bật/tắt bằng tay thì hệ thống không tự làm ngược lại |
| 6 | Cảnh báo | Đi vắng mà thiết bị còn bật thì kêu còi và hiện cảnh báo, có nhắc lại |
| 7 | Giao diện | Xem camera, trạng thái, nhật ký; điều khiển; cài đặt camera, mô hình, ổ cắm |
| 8 | Lưu trữ | Lưu số liệu cảm biến, sự kiện, lệnh điều khiển để phân tích về sau |

### 2.1.2. Yêu cầu phi chức năng

- Khi mất dữ liệu từ camera thì hệ thống không được kết luận là nhà vắng.
- Không bật tắt thiết bị liên tục trong thời gian ngắn để tránh hỏng rơ-le của ổ cắm.
- Hình ảnh camera chỉ được xử lý trong mạng nội bộ, không lưu lại và không gửi lên Internet.
- Hệ thống chạy được trên máy tính không có card đồ họa rời.
- Việc cài đặt hệ thống trên máy tính mới phải đơn giản.

## 2.2. Sơ đồ khối của hệ thống

Dựa trên các yêu cầu đã nêu, sơ đồ khối của hệ thống được xây dựng như sau:

```
 Camera Tapo C222 ──RTSP──► Khối AI (Python) ──┐
                                               │
 ESP32 + SHT30, BH1750, MC-38 ─────────────────┤   MQTT (broker Mosquitto)
        ▲ lệnh còi                             │
        └──────────────────────────────────────┼──► HUB (C#) ──► PostgreSQL
                                               │      ▲  │
 Ổ cắm Tapo P100 ◄── bộ nối P100 ◄─────────────┘      │  └─ lệnh bật/tắt
                                                      │
                                   Trang web Dashboard (Blazor) ⇄ HTTP
```

**Hình 2.1. Sơ đồ khối tổng quát của hệ thống**

Chức năng của từng khối:

- Khối camera: quan sát căn phòng và truyền hình ảnh về máy tính qua giao thức RTSP.
- Khối AI: đọc hình ảnh từ camera, phát hiện người, xác định khu vực và tư thế của người rồi gửi kết quả lên
  MQTT broker.
- Khối cảm biến (ESP32): đo nhiệt độ, độ ẩm, ánh sáng, đọc trạng thái cửa và điều khiển còi.
- Khối xử lý trung tâm (Hub): nhận toàn bộ dữ liệu, xác định trạng thái hiện diện, ra quyết định điều khiển,
  gửi lệnh đến thiết bị và lưu dữ liệu vào cơ sở dữ liệu.
- Khối điều khiển ổ cắm: nhận lệnh từ Hub và điều khiển ổ cắm Tapo P100 qua mạng nội bộ.
- Khối giao diện (Dashboard): hiển thị thông tin và nhận thao tác của người dùng.

Trong quá trình hoạt động, camera truyền hình ảnh về khối AI. Khối AI xử lý các khung hình và
mỗi giây gửi một tin lên MQTT broker cho biết trong hình có bao nhiêu người, mỗi người ở khu vực nào và tư thế
gì. Đồng thời ESP32 cứ 10 giây gửi một tin chứa nhiệt độ, độ ẩm, ánh sáng. Hub nhận các tin này, xác định trạng
thái hiện diện của căn phòng, sau đó so sánh với các điều kiện điều khiển. Nếu thỏa mãn điều kiện thì Hub gửi
lệnh bật đến khối điều khiển ổ cắm. Khi phát hiện cả nhà đã đi vắng mà thiết bị vẫn còn bật thì Hub gửi lệnh
bật còi đến ESP32 và hiển thị cảnh báo trên giao diện.

Trong hệ thống, khối AI chỉ có nhiệm vụ quan sát và gửi kết quả, còn việc xác định trạng thái hiện diện và ra quyết định điều khiển do Hub thực hiện. Cách phân chia này được chọn vì để kết luận nhà vắng cần kết hợp
dữ liệu từ camera, cảm biến cửa và thời gian chờ. Ngoài ra khi chưa có camera thì có thể dùng chương trình mô
phỏng gửi tin thay cho khối AI để kiểm tra hoạt động của Hub.

## 2.3. Thiết kế phần cứng

### 2.3.1. Lựa chọn linh kiện

**Bảng 2.2. Danh mục linh kiện**

| STT | Linh kiện | Số lượng | Chức năng |
|---|---|---|---|
| 1 | Camera TP-Link Tapo C222 4MP | 1 | Quan sát phòng, phát luồng RTSP |
| 2 | Ổ cắm Wi-Fi Tapo P100 | 1 | Đóng cắt nguồn cho đèn |
| 3 | ESP32 DevKit V1 30 chân | 1 | Vi điều khiển của nút cảm biến |
| 4 | SHT30 I2C dạng đầu dò | 1 | Đo nhiệt độ, độ ẩm |
| 5 | BH1750 GY-302 | 1 | Đo cường độ ánh sáng |
| 6 | MC-38 loại NC | 1 | Phát hiện cửa mở/đóng |
| 7 | Buzzer 5V | 1 | Phát âm thanh cảnh báo |
| 8 | Máy tính Intel Core i7-10610U, RAM 16 GB, không card đồ họa rời | 1 | Chạy Hub, AI, cơ sở dữ liệu |

### 2.3.2. Sơ đồ kết nối

**Bảng 2.3. Sơ đồ nối chân ESP32**

| Linh kiện | Chân linh kiện | Chân ESP32 |
|---|---|---|
| SHT30, BH1750 | VCC | 3V3 |
| SHT30, BH1750 | GND | GND |
| SHT30, BH1750 | SDA | GPIO21 |
| SHT30, BH1750 | SCL | GPIO22 |
| MC-38 | Dây 1 | GPIO27 (kéo lên bên trong) |
| MC-38 | Dây 2 | GND |
| Buzzer | Cực (+) | VIN (5V) |
| Buzzer | Cực (−) | Cực C của transistor NPN; cực B qua điện trở 1 kΩ về GPIO25; cực E về GND |

Giải thích sơ đồ kết nối:

- Hai cảm biến SHT30 và BH1750 dùng chung đường I2C của ESP32 (GPIO21 là SDA, GPIO22 là SCL).
- Cảm biến cửa MC-38 nối giữa GPIO27 và GND, chân GPIO27 được cấu hình điện trở kéo lên. Khi cửa đóng chân
  GPIO27 ở mức thấp, khi cửa mở chân GPIO27 ở mức cao. Với cách nối này nếu dây cảm biến bị đứt thì hệ thống
  hiểu là cửa đang mở.
- Còi được cấp nguồn 5 V từ chân VIN và được đóng cắt bằng transistor NPN, chân B của transistor nối với GPIO25
  qua điện trở 1 kΩ.

Camera được gắn ở góc trần của căn phòng để quan sát được toàn bộ phòng.

## 2.4. Thiết kế giao thức truyền dữ liệu

Các khối trong hệ thống trao đổi dữ liệu qua MQTT. Dữ liệu trong mỗi tin có định dạng JSON. Các chủ đề được quy
định như sau:

**Bảng 2.4. Các chủ đề MQTT của hệ thống**

| Chủ đề | Chiều | Tần suất | Nội dung |
|---|---|---|---|
| `home/sensor/esp1/env` | ESP32 → Hub | 10 giây | `ts`, `temp_c`, `humidity`, `lux` |
| `home/sensor/esp1/door` | ESP32 → Hub | khi đổi, retained | `ts`, `open` |
| `home/sensor/esp1/status` | ESP32 → Hub | retained, LWT | `ts`, `online` |
| `home/sensor/esp1/buzzer/set` | Hub → ESP32 | khi cần | `ts`, `on` |
| `home/device/{id}/set` | Hub → bộ nối P100 | khi cần | `ts`, `on`, `source` |
| `home/device/{id}/state` | bộ nối → Hub | khi đổi, retained | `ts`, `on` |
| `home/ai/livingroom/state` | AI → Hub | 1 lần/giây | `ts`, `seq`, `camera_ok`, danh sách người |
| `home/ai/livingroom/transition` | AI → Hub | khi có | người nào đi từ vùng nào sang vùng nào |
| `home/ai/status` | AI → Hub | retained, LWT | `ts`, `online` |
| `home/alert/{id}` | Hub → mọi nơi | khi cảnh báo | mã cảnh báo, mức, thiết bị đang bật |

Tất cả các tin đều có trường `ts` là thời điểm gửi tin theo giờ UTC. Tọa độ của người trong tin của khối AI
được chuẩn hóa về khoảng từ 0 đến 1 để không phụ thuộc vào độ phân giải của camera.

Ví dụ một tin môi trường do ESP32 gửi:

```json
{"ts": "2026-11-21T11:28:44Z", "temp_c": 28.0, "humidity": 60.0, "lux": 150}
```

## 2.5. Thiết kế khối AI

Quá trình xử lý của khối AI được thực hiện theo các bước sau:

```
khung hình → phát hiện người + khớp xương → theo dõi (gán số hiệu) → tư thế → vùng → chuyển vùng → gửi tin
```

**Hình 2.2. Chuỗi xử lý của khối AI**

- Bước 1 - Phát hiện người: khung hình được đưa vào mô hình ước lượng tư thế, kết quả là hộp bao và 17 khớp của
  từng người. Những kết quả có độ tin cậy nhỏ hơn 0,3 bị loại bỏ.
- Bước 2 - Theo dõi: mỗi người được gán một số thứ tự. Số thứ tự này được giữ qua các khung hình bằng cách so
  sánh độ chồng lấp của hộp bao giữa hai khung hình liên tiếp.
- Bước 3 - Xác định tư thế: tư thế được chia thành 4 loại là đứng, ngồi, nằm và di chuyển. Tư thế di chuyển
  được xác định dựa vào tốc độ dịch chuyển của tâm hộp bao. Ba tư thế còn lại được xác định dựa vào vị trí các
  khớp. Kết quả được lấy theo đa số trong 8 khung hình gần nhất để tránh bị thay đổi liên tục.
- Bước 4 - Xác định khu vực: căn phòng được chia thành 4 khu vực bằng các đa giác do người dùng vẽ trên giao
  diện, gồm: `livingroom` (phòng khách), `sofa` (chỗ nằm nghỉ), `door` (cửa chính), `other_exit` (lối sang khu
  khác). Với người đang đứng thì lấy điểm chân để xét, với người đang ngồi hoặc nằm thì lấy tâm hộp bao.
- Bước 5 - Gửi tin: mỗi giây khối AI gửi một tin trạng thái. Khi một người chuyển sang khu vực khác và ở khu
  vực mới đủ 6 khung hình thì khối AI gửi thêm một tin chuyển vùng.

## 2.6. Thiết kế khối xác định trạng thái hiện diện

Trạng thái hiện diện của căn phòng được mô tả bằng một máy trạng thái gồm 5 trạng thái:

**Bảng 2.5. Năm trạng thái hiện diện**

| Trạng thái | Ý nghĩa | Điều kiện |
|---|---|---|
| `Occupied` | Có người trong phòng | Có người ở vùng phòng khách hoặc chỗ nằm nghỉ |
| `OtherArea` | Người ở khu khác | Không có người trong phòng và chưa có bằng chứng rời nhà |
| `ExitPending` | Đang chờ xác minh đi vắng | Có bằng chứng rời nhà, hết người, chưa đủ 30 giây |
| `AwayConfirmed` | Đã xác nhận đi vắng | Đủ 30 giây liên tục sau bằng chứng rời nhà |
| `Unknown` | Chưa rõ | Khối AI dừng, camera lỗi hoặc dữ liệu quá 10 giây chưa cập nhật |

Sơ đồ chuyển trạng thái:

```
            có người                      hết người, có bằng chứng rời nhà
 OtherArea ◄────────► Occupied ─────────────────────────────► ExitPending
     ▲                    ▲                                        │ đủ 30 giây
     │                    └──────────── người quay lại ────────────┤
     │                                                             ▼
     └─────────────────── người quay lại ──────────────────── AwayConfirmed

 Mất dữ liệu ở bất kỳ trạng thái nào ───► Unknown ───► trở lại khi có dữ liệu mới
```

**Hình 2.3. Sơ đồ chuyển trạng thái hiện diện**

Giải thích sơ đồ:

- Khi có người ở khu vực phòng khách hoặc chỗ nằm nghỉ thì trạng thái là `Occupied`.
- Khi không còn người trong phòng và chưa có bằng chứng người rời khỏi nhà thì trạng thái là `OtherArea`.
- Bằng chứng người rời khỏi nhà gồm 2 trường hợp: khối AI báo có người đi ra theo hướng cửa chính, hoặc cảm
  biến MC-38 báo cửa mở. Khi có bằng chứng và trong hình không còn người thì trạng thái chuyển sang
  `ExitPending`.
- Sau 30 giây liên tục ở `ExitPending` thì chuyển sang `AwayConfirmed`. Nếu trong thời gian này có người xuất
  hiện lại thì quay về `Occupied`.
- Khi khối AI ngừng hoạt động, camera lỗi hoặc quá 10 giây không có dữ liệu mới thì trạng thái là `Unknown`.
  Ở trạng thái này hệ thống không kết luận nhà vắng.

Để hạn chế ảnh hưởng khi camera bỏ sót người trong một vài khung hình, hệ thống tạm giữ trạng thái `Occupied`
trong khoảng 10 giây sau khi không còn phát hiện người.

## 2.7. Thiết kế khối tự động điều khiển

Điều kiện điều khiển:

- Bật đèn khi: trạng thái là `Occupied` và cường độ ánh sáng nhỏ hơn 50 lux.
- Bật quạt khi: trạng thái là `Occupied`, có người nằm ở khu vực nằm nghỉ liên tục từ 5 giây trở lên và nhiệt
  độ từ 28 °C trở lên.
- Khi người dùng bật hoặc tắt thiết bị bằng tay thì trong 30 phút tiếp theo hệ thống không tự động điều khiển
  thiết bị đó.

Hệ thống chỉ tự động bật thiết bị, việc tắt thiết bị do người dùng thực hiện.

Để nhận biết thao tác trên nút của ổ cắm, Hub kiểm tra lịch sử lệnh gần nhất. Nếu ổ cắm báo đổi trạng thái
mà Hub không gửi lệnh tương ứng trong 10 giây trước đó, thay đổi được xem là thao tác bằng tay.

Ngoài ra để bảo vệ thiết bị, khối tự động điều khiển có thêm các giới hạn sau:

**Bảng 2.6. Các lớp bảo vệ khi điều khiển thiết bị**

| Lớp bảo vệ | Giá trị | Chống hiện tượng |
|---|---|---|
| Khoảng cách tối thiểu giữa hai lệnh tới cùng thiết bị | 3 giây, lệnh thừa được gộp | Dồn lệnh từ nhiều nguồn |
| Giữ trạng thái tối thiểu | 10 giây | Bật rồi tắt ngay |
| Khoảng nghỉ giữa hai lệnh tự động | 60 giây | Lệnh lặp |
| Giới hạn số lần đổi tự động | 6 lần/giờ, vượt thì khóa tự động 1 giờ | Vòng lặp lỗi |
| Không tự động khi mới khởi động | 30 giây đầu | Bật chồng khi thiết bị vốn đang bật |
| Bỏ lệnh trùng trạng thái | | Gửi thừa |

## 2.8. Thiết kế khối cảnh báo

Nguyên lý hoạt động: khi trạng thái là `AwayConfirmed` mà vẫn còn thiết bị đang bật, sau 2 phút Hub phát cảnh
báo lần đầu và gửi lệnh bật còi đến ESP32. Sau đó cứ 5 phút Hub nhắc lại một lần. Cảnh báo được hủy khi xảy ra
một trong các trường hợp sau:

- Có người trở về.
- Thiết bị đã được tắt.
- Mất dữ liệu quá 60 giây.

Hệ thống có thiết kế chức năng tự động tắt thiết bị sau 10 phút, tuy nhiên chức năng này mặc định không bật.

## 2.9. Thiết kế cơ sở dữ liệu

Cơ sở dữ liệu dùng để lưu lịch sử hoạt động của hệ thống, gồm các bảng sau:

**Bảng 2.7. Các bảng trong cơ sở dữ liệu**

| Tên bảng | Nội dung |
|---|---|
| `sensor_reading` | Nhiệt độ, độ ẩm, ánh sáng theo thời gian |
| `door_event` | Các lần cửa mở, đóng |
| `ai_event` | Sự kiện từ khối AI (trạng thái, chuyển vùng) |
| `occupancy_log` | Các lần đổi trạng thái hiện diện |
| `device_command_log` | Lệnh điều khiển thiết bị kèm nguồn (tự động, tay, cảnh báo) |
| `alert_log` | Lịch sử cảnh báo và phản hồi của người dùng |

## 2.10. Thiết kế giao diện

Giao diện của hệ thống gồm 2 trang:

- Trang chính: hiển thị hình ảnh camera, trạng thái hiện diện, danh sách thiết bị, nhật ký hoạt động và danh
  sách cảnh báo. Trên trang này người dùng có thể chọn chế độ tự động (Auto) hoặc bằng tay (Manual) và bật tắt
  thiết bị.
- Trang cài đặt: dùng để nhập địa chỉ IP của camera, chọn mô hình nhận người, đăng nhập ổ cắm và dạy tư thế.

## 2.11. Kết luận chương 2

Chương 2 đã trình bày yêu cầu và thiết kế các khối của hệ thống. Các ngưỡng 50 lux, 28 °C và các khoảng thời
gian 30 giây, 2 phút được chọn ban đầu theo kinh nghiệm và có thể điều chỉnh trong cấu hình. Việc xây dựng các
khối theo thiết kế được trình bày ở chương 3.

---

# CHƯƠNG 3. XÂY DỰNG HỆ THỐNG

Chương này trình bày quá trình xây dựng các khối của hệ thống theo thiết kế ở chương 2, cách triển khai hệ
thống và các khó khăn gặp phải trong quá trình thực hiện.

## 3.1. Môi trường và công cụ

**Bảng 3.1. Môi trường phát triển**

| Thành phần | Phiên bản |
|---|---|
| Hệ điều hành | Ubuntu 26.04 LTS |
| Máy tính | Intel Core i7-10610U, RAM 16 GB |
| .NET SDK | 10.0 |
| Python | 3.14 |
| ONNX Runtime | 1.32 |
| OpenCV | 4.14 |
| PostgreSQL | 17 |
| Eclipse Mosquitto | 2 |
| Docker | 29 |
| PlatformIO (ESP32, khung Arduino) | bo `esp32doit-devkit-v1` |

Mã nguồn của hệ thống được chia thành các thư mục: `ai` (khối AI), `src/SmartHomeAI.Core` (các thuật toán xác
định hiện diện, tự động điều khiển, cảnh báo), `src/SmartHomeAI.Hub` (Hub), `src/SmartHomeAI.Dashboard` (giao
diện), `firmware/esp32` (chương trình cho ESP32), `sim` (chương trình mô phỏng), `tests` (kiểm thử).

## 3.2. Xây dựng chương trình cho ESP32

Chương trình cho ESP32 được viết bằng ngôn ngữ C++ trên PlatformIO. Lưu đồ thuật toán như sau:

```
                 ┌───────────────────────┐
                 │ Khởi tạo GPIO, I2C,   │
                 │ Wi-Fi, lấy giờ NTP    │
                 └──────────┬────────────┘
                            ▼
                 ┌───────────────────────┐   chưa
        ┌───────►│ Đã kết nối MQTT?      ├────────► tắt còi, thử nối lại mỗi 5 giây ──┐
        │        └──────────┬────────────┘                                            │
        │                   │ rồi                                                     │
        │                   ▼                                                         │
        │        Nhận lệnh còi (nếu có) → bật/tắt còi                                 │
        │                   ▼                                                         │
        │        Cửa đổi trạng thái (ổn định 50 ms)? → gửi tin door                   │
        │                   ▼                                                         │
        │        Đủ 10 giây? → đọc SHT30, BH1750 → gửi tin env                        │
        └───────────────────┴─────────────────────────────────────────────────────────┘
```

**Hình 3.1. Lưu đồ thuật toán firmware ESP32**

Giải thích lưu đồ:

- ESP32 khởi tạo các chân GPIO, đường I2C, kết nối Wi-Fi và lấy thời gian từ máy chủ NTP.
- Trong vòng lặp chính, ESP32 kiểm tra kết nối MQTT. Nếu chưa kết nối thì tắt còi và thử kết nối lại sau mỗi
  5 giây.
- Khi đã kết nối, nếu nhận được lệnh điều khiển còi từ Hub thì bật hoặc tắt còi theo lệnh.
- ESP32 đọc trạng thái chân GPIO27. Nếu trạng thái thay đổi và giữ ổn định trong 50 ms thì gửi tin trạng thái
  cửa.
- Sau mỗi 10 giây, ESP32 đọc cảm biến SHT30 và BH1750 rồi gửi tin môi trường. Nếu một trong hai cảm biến không
  phản hồi thì không gửi tin.

Hai cảm biến được đọc trực tiếp bằng thư viện Wire. Với SHT30, chương trình có kiểm tra mã CRC của dữ liệu,
nếu sai thì bỏ qua lần đo đó. Còi được tắt khi mất kết nối MQTT để tránh trường hợp còi đang kêu mà Hub gặp sự
cố thì không có lệnh tắt.

Chương trình đã biên dịch thành công. Do hạn chế về thời gian nên chương trình chưa được nạp và chạy thử trên
mạch thật. Trong các lần thử nghiệm, dữ liệu cảm biến được lấy từ chương trình mô phỏng ESP32.

## 3.3. Xây dựng khối AI

### 3.3.1. Đọc hình ảnh và chạy mô hình

Khối AI được viết bằng Python và đọc hình ảnh từ camera bằng thư viện OpenCV. Mỗi khung hình được thay đổi
kích thước, giữ tỷ lệ và thêm viền màu xám để có ảnh đầu vào 640×640. Ảnh sau đó được đưa vào mô hình thông
qua ONNX Runtime.

Để có thể thử nhiều mô hình khác nhau, chương trình được viết phần xử lý kết quả cho cả 3 loại mô hình. Loại
mô hình được nhận biết dựa vào kích thước đầu ra:

- Đầu ra là 1 mảng [1, 56, N]: mô hình YOLOv8-pose, cần thực hiện thêm bước loại bỏ hộp trùng.
- Đầu ra là 1 mảng [1, 300, 57]: mô hình YOLO26-pose.
- Đầu ra là 2 mảng: mô hình RTMO.

Các tệp mô hình được tải từ Hugging Face và kiểm tra mã SHA-256 trước khi sử dụng.

### 3.3.2. Xác định tư thế theo vị trí các khớp

Ban đầu tư thế được xác định theo quy tắc sau:

- Nếu đường nối giữa vai và hông gần nằm ngang trên ảnh thì là nằm.
- Nếu hông ở gần ngang với đầu gối thì là ngồi.
- Các trường hợp còn lại là đứng.

### 3.3.3. Dạy tư thế theo mẫu

Khi chạy thử với camera gắn ở góc trần, quy tắc trên cho kết quả sai: người nằm bị nhận thành ngồi, người ngồi
xếp bằng có lúc bị nhận thành nằm. Nguyên nhân là do camera nhìn chéo từ trên xuống nên hình ảnh của người nằm
dọc theo hướng nhìn của camera giống với hình ảnh của người đang ngồi. Đã thử thay đổi các ngưỡng nhưng không
tách được hai tư thế này.

Để khắc phục, chức năng dạy tư thế theo mẫu được bổ sung. Các bước thực hiện như sau:

- Bước 1: trên trang cài đặt, người dùng bấm nút "Ghi Nằm" (hoặc "Ghi Ngồi", "Ghi Đứng"), sau đó có 5 giây để
  vào đúng tư thế.
- Bước 2: hệ thống ghi 8 mẫu, mỗi giây một mẫu. Mỗi mẫu gồm tọa độ của 17 khớp (34 giá trị). Các tọa độ này
  được trừ đi tọa độ tâm và chia cho khoảng cách lớn nhất để không phụ thuộc vào vị trí và kích thước của người
  trong ảnh.
- Bước 3: mỗi mẫu được lưu kèm một ảnh nhỏ. Người dùng xem lại các ảnh và xóa những mẫu bị sai.
- Bước 4: khi đã có ít nhất 2 tư thế, mỗi tư thế có ít nhất 5 mẫu thì hệ thống dùng thuật toán k-NN với k = 5
  để xác định tư thế thay cho quy tắc ở mục 3.3.2.

Cách làm này không cần huấn luyện lại mô hình ước lượng tư thế. Khi thay đổi vị trí camera hoặc mô hình, cần
kiểm tra lại và ghi mẫu phù hợp với điều kiện mới.

## 3.4. Xây dựng Hub

Hub được viết bằng C# trên nền ASP.NET Core. Hoạt động của Hub như sau:

- Hub kết nối đến MQTT broker và đăng ký nhận tất cả các tin có chủ đề bắt đầu bằng `home/`.
- Khi nhận được tin, Hub kiểm tra định dạng của tin. Tin sai định dạng bị bỏ qua.
- Dữ liệu được đưa vào khối xác định trạng thái hiện diện, khối tự động điều khiển và khối cảnh báo.
- Nếu có lệnh điều khiển thì Hub gửi lệnh lên MQTT broker.
- Các sự kiện được lưu vào cơ sở dữ liệu PostgreSQL.

Mỗi quyết định của Hub đều được ghi lại kèm lý do, ví dụ "Quạt chưa bật: người chưa ở vùng sofa". Lý do này
được hiển thị trong phần nhật ký trên giao diện.

Hub cung cấp các API cho giao diện: đọc trạng thái hiện tại, đọc nhật ký, đọc danh sách cảnh báo, đổi chế độ
Auto/Manual và điều khiển thiết bị.

## 3.5. Điều khiển ổ cắm Tapo P100

Ổ cắm Tapo P100 không hỗ trợ MQTT mà sử dụng giao thức riêng của hãng. Do đó cần có một chương trình trung gian
nhận lệnh từ Hub và điều khiển ổ cắm qua mạng nội bộ. Chương trình này được viết bằng Python, sử dụng thư viện
có sẵn.

Người dùng đăng nhập ổ cắm trên trang cài đặt bằng tài khoản TP-Link và địa chỉ IP của ổ cắm. Sau khi kết nối
thành công, thông tin đăng nhập được lưu lại trên máy để Hub tự kết nối lại khi khởi động lại. Nếu ổ cắm chưa
kết nối mạng thì Hub thử lại sau mỗi 60 giây. Nếu sai mật khẩu thì Hub không thử lại để tránh bị khóa tài khoản.

## 3.6. Xây dựng giao diện

Giao diện được xây dựng bằng Blazor. Sau mỗi 2 giây trang chính lấy dữ liệu từ Hub và cập nhật lại màn hình.
Hình ảnh camera do khối AI truyền lên, đã được vẽ sẵn hộp bao, khung xương và các khu vực.

*[Chèn ảnh chụp màn hình trang chính]*

**Hình 3.2. Giao diện trang chính**

*[Chèn ảnh chụp màn hình trang cài đặt]*

**Hình 3.3. Giao diện trang cài đặt và chức năng dạy tư thế**

Để vẽ các khu vực, người dùng bấm chọn từng đỉnh của đa giác trên ảnh camera. Ngoài ra có thể nhập số cạnh để
tạo sẵn một đa giác, sau đó kéo các đỉnh đến vị trí mong muốn.

## 3.7. Triển khai hệ thống

Hệ thống được đóng gói bằng Docker, gồm 4 container:

- `postgres`: cơ sở dữ liệu.
- `mosquitto`: MQTT broker.
- `hub`: bộ xử lý trung tâm.
- `dashboard`: giao diện, khối AI và chương trình mô phỏng.

Để cài đặt trên máy tính mới chỉ cần cài Docker, tải mã nguồn, tạo tệp `.env` chứa địa chỉ camera và chạy lệnh
`docker compose up -d --build`. Các bước chi tiết được trình bày ở Phụ lục A.

Sau khi máy tính khởi động lại, các container tự chạy lại, khối AI tự bật lại và Hub tự đăng nhập lại ổ cắm.

## 3.8. Các khó khăn gặp phải và cách giải quyết

**Bảng 3.2. Khó khăn gặp phải và cách giải quyết**

| STT | Khó khăn | Cách giải quyết |
|---|---|---|
| 1 | Chưa có phần cứng khi bắt đầu viết phần mềm | Viết chương trình mô phỏng ESP32, ổ cắm, camera gửi đúng loại tin như thật |
| 2 | Mật khẩu camera có ký tự `@` làm sai địa chỉ RTSP | Mã hóa thành `%40` |
| 3 | Người có thể bị mô hình bỏ sót khi độ tin cậy thấp | Hạ ngưỡng từ 0,45 xuống 0,3 và đổi sang mô hình lớn hơn (mục 4.4) |
| 4 | Camera gắn trần làm lẫn ngồi với nằm | Dạy tư thế theo mẫu (mục 3.3.3) |
| 5 | Địa chỉ IP camera thay đổi làm mất hình | Cho đổi IP ngay trên trang Cài đặt |
| 6 | Bật tắt thiết bị dồn dập khi dữ liệu nhiễu | Thêm các lớp bảo vệ (Bảng 2.6) |
| 7 | Sau khi khởi động lại phải bật AI và đăng nhập ổ cắm bằng tay | Lưu lại trạng thái và tự khôi phục |
| 8 | Vẽ vùng lệch làm quạt, đèn phản ứng sai chỗ | Cho vẽ lại vùng trực tiếp trên ảnh camera |

## 3.9. Kết luận chương 3

Chương 3 đã trình bày quá trình xây dựng các khối của hệ thống. Khối AI, Hub, giao diện và phần điều khiển ổ
cắm đã hoạt động được với camera và ổ cắm thật. Chương trình cho ESP32 đã viết xong nhưng chưa chạy trên mạch
thật.

---

# CHƯƠNG 4. KẾT QUẢ THỬ NGHIỆM VÀ ĐÁNH GIÁ

Chương này trình bày kết quả kiểm thử phần mềm, kết quả chạy với thiết bị thật và đánh giá hệ thống.

## 4.1. Kết quả kiểm thử đơn vị

Kiểm thử đơn vị dùng để kiểm tra từng phần của chương trình với dữ liệu đầu vào cho trước. Kết quả như sau:

**Bảng 4.1. Kết quả kiểm thử đơn vị (chạy ngày 21/11/2026)**

| Bộ kiểm thử | Nội dung | Số ca | Đạt |
|---|---|---|---|
| Core | Hiện diện, tự động hóa, cảnh báo, học thói quen | 72 | 72 |
| Hub | Phân tích tin MQTT, chống dồn lệnh, nhật ký quyết định, bộ nối P100 | 71 | 71 |
| AI | Chuỗi xử lý, tư thế, vùng, giải mã mô hình, ghi mẫu tư thế | 23 | 23 |
| **Tổng** | | **166** | **166** |

Ngoài ra còn có 93 ca kiểm thử cho chương trình mô phỏng và 62 ca kiểm thử toàn hệ thống theo kịch bản.

## 4.2. Kết quả kiểm thử theo kịch bản cơ bản

Từ yêu cầu của hệ thống, 12 kịch bản cơ bản được xây dựng, ký hiệu từ SC01 đến SC12. Cách thực hiện: chương
trình mô phỏng gửi các tin MQTT theo kịch bản đến Hub, sau đó so sánh lệnh điều khiển do Hub gửi ra với kết quả
mong muốn. Thời gian trong kịch bản được rút ngắn để giảm thời gian chờ.

**Bảng 4.2. Kết quả 12 kịch bản cơ bản (trên mô phỏng)**

| Mã | Kịch bản | Kỳ vọng | Kết quả |
|---|---|---|---|
| SC01 | Vào phòng khách, trời tối | Đèn tự bật | Đạt |
| SC02 | Vào phòng khách, trời sáng | Đèn giữ nguyên | Đạt |
| SC03 | Nằm nghỉ, trời nóng | Quạt tự bật | Đạt |
| SC04 | Nằm nghỉ, trời mát | Xét theo thói quen | Đạt trong lần chạy trước; cần kiểm tra lại trên phiên bản hiện tại |
| SC05 | Rời chỗ nằm sang khu khác | Không kết luận đi vắng | Đạt |
| SC06 | Ra cửa rồi quay lại | Hủy xác minh đi vắng | Đạt |
| SC07 | Ra ngoài, đèn quạt còn bật | Có cảnh báo | Đạt |
| SC08 | Hai người, một người ra ngoài | Không kết luận nhà vắng | Đạt |
| SC09 | Tắt quạt bằng tay | Hệ thống không tự bật lại ngay | Đạt |
| SC10 | Camera mất kết nối | Không suy ra nhà trống | Đạt |
| SC11 | Người trở về | Hủy cảnh báo đang có | Đạt |
| SC12 | Đổi thói quen | Cập nhật đề xuất ngưỡng | Chưa thực hiện |

Kịch bản SC12 chưa thực hiện được vì phần học thói quen chưa được đưa vào Hub. Kết quả SC04 liên quan đến
thói quen cần được kiểm tra lại trên phiên bản hiện tại để làm rõ điều kiện đạt. Kết quả của SC01 đến SC11 là kết
quả của các lần chạy trước ngày 21/11/2026. Sau các chỉnh sửa trong ngày 21/11/2026 mới chạy lại phần kiểm thử
đơn vị ở Bảng 4.1.

## 4.3. Kết quả kiểm thử theo kịch bản thực tế

Để kiểm tra hệ thống trong các tình huống gần với thực tế hơn, 20 kịch bản dài được xây dựng thêm, ký hiệu từ
R01 đến R20. Các kịch bản này có thêm nhiễu và có nhiều sự kiện xảy ra xen kẽ.

**Bảng 4.3. Kết quả 20 kịch bản thực tế (trên mô phỏng, lần chạy 15/11/2026)**

| Mã | Tình huống | Kết quả |
|---|---|---|
| R01 | Sáng sớm vào phòng tối rồi mở rèm | Đạt: đèn bật đúng 1 lần |
| R02 | Tối về nhà, ngồi nghỉ | Đạt |
| R03 | Trưa nóng 30–33 °C, nằm xem TV rồi ngủ quên | Đạt: quạt bật 1 lần |
| R04 | Đêm ngủ, camera hồng ngoại nhiễu tư thế | Đạt: không báo vắng sai |
| R05 | Đi vệ sinh 2 phút | Đạt: không báo vắng |
| R06 | Ra cửa nhận hàng 1 phút | Đạt: không cảnh báo |
| R07 | Đi làm quên quạt, đèn rồi về sớm | Đạt: có cảnh báo, hủy khi về |
| R08 | Đi vắng cả ngày để đèn | Đạt: nhắc lại đều, không dồn |
| R09 | Có khách, một người ra về | Đạt: không báo vắng |
| R10 | Cả nhà ra ngoài cách nhau 10 giây | Đạt: xác nhận vắng, có cảnh báo |
| R11 | Khối AI ngừng 90 giây khi đang xem TV | Đạt: không cảnh báo sai |
| R12 | Camera bị che 140 giây | Đạt: chuyển `Unknown`, không báo vắng |
| R13 | Trời nóng nhưng người dùng tắt quạt bằng tay | Đạt: không tự bật lại |
| R14 | Bật đèn bằng tay giữa ban ngày | Đạt: không tự tắt |
| R15 | Nhiệt độ dao động quanh 28 °C | Đạt: quạt không bật tắt liên tục |
| R16 | Mưa chiều, nhiệt độ giảm từ 31 xuống 25 °C | Đạt: quạt bật 1 lần, không tự tắt |
| R17 | Mất điện, ổ cắm mất kết nối 3 phút | Đạt: không dồn lệnh khi có lại |
| R18 | Hub khởi động lại khi đang nằm nghỉ, trời nóng | Đạt: chờ hết 30 giây đầu mới tự động |
| R19 | Trẻ chạy nhảy liên tục giữa các vùng | Đạt: mỗi thiết bị không quá 2 lệnh |
| R20 | Một ngày dài: đêm, sáng, ngủ trưa, đi vắng, tối | Đạt: tổng 2 lệnh, 1 cảnh báo |

Cả 20 kịch bản đều đạt kết quả mong đợi trong lần chạy mô phỏng. Tuy nhiên, kết quả này cũng cho thấy một số
hạn chế cần tiếp tục xem xét:

- Trong cấu hình mặc định, hệ thống tự bật nhưng không tự tắt theo điều kiện phòng. Ở kịch bản R16, khi nhiệt
  độ giảm xuống 25 °C thì quạt vẫn chạy.
- Khi người rời khỏi phòng thì đèn vẫn sáng, hệ thống chỉ cảnh báo khi xác nhận cả nhà đã đi vắng.
- Ở kịch bản R06, nếu thời gian ra ngoài nhận hàng lâu hơn khoảng 2,5 phút thì hệ thống sẽ cảnh báo.
- Trường hợp cửa chính vừa mở và người đi vào lối sang khu khác, khuất khỏi camera thì hệ thống có thể kết
  luận nhầm là đi vắng. Trường hợp này chưa có kịch bản kiểm tra.

## 4.4. So sánh các mô hình nhận người trên camera thật

Bốn mô hình YOLOv8-pose với kích thước khác nhau được chạy thử trên camera Tapo C222. Mỗi mô hình được chạy với
40 khung hình, mỗi khung hình cách nhau 0,5 giây, trong hình có một người đang ngồi.

**Bảng 4.4. So sánh các mô hình trên camera thật (40 khung hình, máy i7-10610U, chạy CPU)**

| Mô hình | Thời gian/khung | Số khung thấy người (ngưỡng 0,3) | Số khung thấy người (ngưỡng 0,45) | Độ tin cậy trung bình |
|---|---|---|---|---|
| YOLOv8n-pose | khoảng 72 ms | 35/40 | 19/40 | 0,45 |
| YOLOv8s-pose | khoảng 170 ms | 40/40 | 23/40 | 0,46 |
| YOLOv8m-pose | khoảng 406 ms | 40/40 | 33/40 | 0,62 |
| YOLOv8l-pose | khoảng 780 ms | 40/40 | 39/40 | 0,73 |

Nhận xét:

- Trong lần thử này, việc giảm ngưỡng độ tin cậy từ 0,45 xuống 0,3 làm tăng số khung hình phát hiện được người.
  Với YOLOv8n-pose, số khung hình phát hiện tăng từ 19 lên 35 trên tổng số 40 khung hình.
- Trong các mô hình được thử, mô hình lớn hơn cho độ tin cậy trung bình cao hơn nhưng xử lý chậm hơn.
- Mô hình YOLOv8m-pose được chọn làm mặc định vì có độ tin cậy trung bình 0,62 và xử lý được khoảng 2,5 khung
  hình mỗi giây.

Các mô hình YOLO26-pose và RTMO được bổ sung sau, đã chạy được trên camera thật nhưng chưa được đo theo cách
trên nên chưa có số liệu so sánh.

## 4.5. Kết quả xác định tư thế

*[Chèn ảnh: người ngồi và người nằm trên camera thật, có hộp bao và nhãn tư thế]*

**Hình 4.1. Kết quả nhận diện người ngồi và nằm trên camera thật**

Khi dùng quy tắc theo vị trí các khớp, với camera gắn trần hệ thống nhận sai tư thế nằm thành ngồi như đã nêu ở
mục 3.3.3.

Chức năng dạy tư thế theo mẫu được thử trên 6 ảnh thật, gồm 3 ảnh ngồi và 3 ảnh nằm. Cách thử: lần lượt lấy ra
1 ảnh, dùng 5 ảnh còn lại làm mẫu rồi xác định tư thế của ảnh đã lấy ra. Kết quả:

- Dùng khớp của mô hình YOLOv8m-pose: đúng 6/6 ảnh.
- Dùng khớp của mô hình RTMO-m: đúng 6/6 ảnh.
- Dùng khớp của mô hình YOLO26s-pose: đúng 5/6 ảnh.

Số ảnh thử còn ít nên chưa đánh giá được độ chính xác của chức năng này. Cần quay video ở nhiều vị trí, nhiều
thời điểm khác nhau và gán nhãn để đánh giá đầy đủ.

## 4.6. Kết quả chạy với thiết bị thật

- Camera Tapo C222: hệ thống đọc được hình ảnh, hiển thị trên giao diện kèm hộp bao, khung xương và các khu
  vực. Có thể đổi địa chỉ IP camera và đổi mô hình trên trang cài đặt.
- Ổ cắm Tapo P100: đăng nhập và bật tắt được từ giao diện. Khi bấm nút trực tiếp trên ổ cắm, Hub nhận biết được
  đây là thao tác bằng tay và ghi vào nhật ký.
- ESP32: chưa chạy với mạch thật.
- Hệ thống chạy bằng Docker trên một máy tính. Sau khi khởi động lại, khối AI tự chạy lại.

## 4.7. Đánh giá hệ thống

**Bảng 4.5. Đối chiếu mục tiêu và kết quả**

| Mục tiêu | Kết quả | Mức độ |
|---|---|---|
| Phát hiện người, xác định khu vực và tư thế | Chạy được trên camera thật; với camera gắn trần cần dạy mẫu tư thế; chưa đo độ chính xác | Đạt một phần |
| Thu thập môi trường bằng ESP32 | Firmware đã viết, biên dịch được, chưa chạy trên mạch | Đạt một phần |
| Bộ xử lý trung tâm suy luận hiện diện và ra quyết định | Đã xây dựng; các bộ Core, Hub và AI có 166 ca kiểm thử đạt | Đạt |
| Điều khiển ổ cắm thương mại qua mạng nội bộ | Chạy thật với Tapo P100 | Đạt |
| Giao diện theo dõi, điều khiển, cài đặt | Hoàn thành | Đạt |
| Bộ kịch bản kiểm thử | 11/12 kịch bản cơ bản từng đạt và 20/20 kịch bản thực tế đạt trên mô phỏng; SC04 cần kiểm tra lại | Đạt |

Ưu điểm:

- Việc kết luận đi vắng dựa trên nhiều nguồn thông tin, khi mất dữ liệu thì không kết luận nhà vắng.
- Có các giới hạn để tránh bật tắt thiết bị liên tục, thao tác bằng tay của người dùng được ưu tiên.
- Mỗi quyết định của hệ thống đều có lý do hiển thị trên giao diện.
- Hệ thống chạy trong mạng nội bộ, không cần card đồ họa, sử dụng các thiết bị phổ thông.
- Có chương trình mô phỏng nên kiểm tra được phần mềm khi chưa có phần cứng.

Hạn chế:

- Chưa đo được độ chính xác nhận diện trên video thật có gán nhãn.
- Chương trình ESP32 chưa được chạy trên mạch thật.
- Với một camera, hệ thống không biết được người đang ở phòng khác.
- Cấu hình mặc định chưa tự động tắt thiết bị theo điều kiện trong phòng; chức năng tắt sau cảnh báo được
  thiết kế nhưng mặc định không bật.
- Giao diện chưa có chức năng đăng nhập.
- Hiện mới có một ổ cắm cho đèn, chức năng bật quạt mới được kiểm tra trên mô phỏng.
- Phần học thói quen của người dùng chưa được đưa vào hệ thống.

## 4.8. Kết luận chương 4

Qua kiểm thử, camera và ổ cắm đã hoạt động với hệ thống; các kịch bản điều khiển và cảnh báo được kiểm tra
chủ yếu trên mô phỏng. Kết quả nhận diện tư thế mới chỉ là bước đầu, cần thêm dữ liệu thực tế để đánh giá.

---

# KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN

## 1. Kết luận

Sau quá trình tìm hiểu, xây dựng và thử nghiệm, đồ án đạt được các kết quả sau:

- Tìm hiểu được giao thức MQTT, vi điều khiển ESP32, các cảm biến và các mô hình ước lượng tư thế người.
- Thiết kế và xây dựng được hệ thống nhà thông minh gồm khối AI, bộ xử lý trung tâm, giao diện web và chương
  trình cho ESP32.
- Hệ thống đã chạy được với camera Tapo C222 và ổ cắm Tapo P100 trong mạng nội bộ.
- Xây dựng được chức năng dạy tư thế theo mẫu cho trường hợp camera gắn trần.
- Xây dựng được 166 ca kiểm thử đơn vị, 32 kịch bản kiểm thử và chương trình mô phỏng phần cứng.

Bên cạnh đó, đồ án vẫn còn các hạn chế đã nêu ở mục 4.7, trong đó hạn chế lớn nhất là chưa đo được độ chính xác
nhận diện trên dữ liệu thật và chưa chạy được mạch ESP32.

## 2. Hướng phát triển

Trong thời gian tới, đề tài có thể được phát triển theo các hướng sau:

- Quay video có gán nhãn để đo độ chính xác nhận diện người và tư thế.
- Hoàn thiện mạch ESP32, bổ sung thêm ổ cắm cho quạt.
- Bổ sung chức năng tự động tắt thiết bị khi không còn người trong phòng.
- Đưa phần học thói quen của người dùng vào hệ thống.
- Bổ sung chức năng đăng nhập cho giao diện và mã hóa dữ liệu truyền qua MQTT.
- Bổ sung cảm biến hoặc camera cho các phòng khác.
- Gửi cảnh báo đến điện thoại của người dùng.
- Huấn luyện lại mô hình với ảnh chụp từ camera gắn trần để tăng độ chính xác.

---

# TÀI LIỆU THAM KHẢO

[1] OASIS, "MQTT Version 3.1.1," OASIS Standard, 2014. [Trực tuyến]. Địa chỉ: https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html

[2] G. Jocher, A. Chaurasia, J. Qiu, "Ultralytics YOLOv8," 2023. [Trực tuyến]. Địa chỉ: https://github.com/ultralytics/ultralytics

[3] T.-Y. Lin và cộng sự, "Microsoft COCO: Common Objects in Context," trong *European Conference on Computer Vision (ECCV)*, 2014.

[4] P. Lu và cộng sự, "RTMO: Towards High-Performance One-Stage Real-Time Multi-Person Pose Estimation," arXiv:2312.07526, 2023.

[5] N. Dalal, B. Triggs, "Histograms of Oriented Gradients for Human Detection," trong *IEEE Conference on Computer Vision and Pattern Recognition (CVPR)*, 2005.

[6] Espressif Systems, "ESP32 Series Datasheet." [Trực tuyến]. Địa chỉ: https://www.espressif.com

[7] Sensirion, "Datasheet SHT3x-DIS: Humidity and Temperature Sensor."

[8] ROHM Semiconductor, "BH1750FVI: Digital 16bit Serial Output Type Ambient Light Sensor IC," Datasheet.

[9] Microsoft, "ASP.NET Core documentation." [Trực tuyến]. Địa chỉ: https://learn.microsoft.com/aspnet/core

[10] Eclipse Foundation, "Eclipse Mosquitto: An open source MQTT broker." [Trực tuyến]. Địa chỉ: https://mosquitto.org

[11] The PostgreSQL Global Development Group, "PostgreSQL Documentation." [Trực tuyến]. Địa chỉ: https://www.postgresql.org/docs

[12] Microsoft, "ONNX Runtime." [Trực tuyến]. Địa chỉ: https://onnxruntime.ai

[13] T. Cover, P. Hart, "Nearest neighbor pattern classification," *IEEE Transactions on Information Theory*, tập 13, số 1, tr. 21–27, 1967.

[14] OpenCV, "OpenCV Documentation." [Trực tuyến]. Địa chỉ: https://docs.opencv.org

*(Ngày truy cập các tài liệu trực tuyến: tháng 11 năm 2026.)*

---

# PHỤ LỤC

## Phụ lục A. Hướng dẫn cài đặt và chạy hệ thống

```bash
# 1. Cài Docker
curl -fsSL https://get.docker.com | sh

# 2. Tải mã nguồn
git clone https://github.com/lethanhsonvsp/SmartHomeAI.git
cd SmartHomeAI

# 3. Tạo tệp .env chứa địa chỉ camera
echo 'AI_SOURCE=rtsp://<tài khoản>:<mật khẩu>@<IP camera>:554/stream2' > .env

# 4. Chạy toàn bộ hệ thống
docker compose up -d --build
```

Sau đó mở trình duyệt tại địa chỉ `http://localhost:5037`. Hướng dẫn chi tiết từng bước có trong tệp
`docs/huong-dan-thuc-hien.md` của mã nguồn.

## Phụ lục B. Cấu trúc thư mục mã nguồn

| Thư mục | Nội dung |
|---|---|
| `ai/` | Khối AI (Python): đọc camera, mô hình, tư thế, vùng |
| `src/SmartHomeAI.Core/` | Thuật toán hiện diện, tự động hóa, cảnh báo, thói quen |
| `src/SmartHomeAI.Hub/` | Hub: MQTT, API, ghi cơ sở dữ liệu, bộ nối P100 |
| `src/SmartHomeAI.Dashboard/` | Giao diện web |
| `firmware/esp32/` | Firmware ESP32 |
| `sim/` | Chương trình mô phỏng và kịch bản |
| `tests/` | Kiểm thử đơn vị và kiểm thử đầu-cuối |
| `docs/` | Tài liệu, mô tả giao thức |
| `infra/` | Cấu hình Mosquitto, câu lệnh tạo bảng, dịch vụ systemd |

## Phụ lục C. Các tham số cấu hình chính

| Tham số | Giá trị mặc định | Ý nghĩa |
|---|---|---|
| Ngưỡng tối | 50 lux | Dưới mức này thì coi là tối |
| Ngưỡng nóng | 28 °C | Từ mức này thì coi là nóng |
| Thời gian nằm tối thiểu | 5 giây | Nằm liên tục đủ lâu mới bật quạt |
| Thời gian xác minh đi vắng | 30 giây | Từ `ExitPending` sang `AwayConfirmed` |
| Hạn dữ liệu | 10 giây | Quá hạn thì chuyển `Unknown` |
| Cảnh báo lần đầu | 2 phút | Sau khi xác nhận đi vắng |
| Nhắc lại cảnh báo | 5 phút | |
| Tạm dừng tự động sau lệnh tay | 30 phút | |
| Ngưỡng độ tin cậy phát hiện người | 0,3 | |
