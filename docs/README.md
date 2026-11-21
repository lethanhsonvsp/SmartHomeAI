# Bắt đầu từ đây

Trang này dành cho người mới vào dự án, kể cả khi bạn chưa từng làm IoT hay AI. Tài liệu được xếp theo
**bốn bậc từ dễ đến khó**. Đi lần lượt, đừng nhảy bậc: mỗi bậc dùng kiến thức của bậc trước.

## Dự án này làm gì (đọc trong 1 phút)

Một phòng khách có camera gắn trần, một ổ cắm thông minh cho đèn và một mạch ESP32 đo nhiệt độ, ánh sáng,
cửa. Hệ thống tự làm ba việc:

1. Có người và trời tối thì bật đèn.
2. Có người nằm nghỉ và trời nóng thì bật quạt.
3. Cả nhà ra ngoài mà đèn, quạt vẫn bật thì kêu còi cảnh báo.

Nó gồm năm phần, bạn sẽ gặp lại năm cái tên này ở mọi tài liệu:

| Tên | Là gì | Viết bằng | Thư mục |
|---|---|---|---|
| **AI** | Xem hình camera, báo "có ai, ở vùng nào, tư thế gì" | Python | `ai/` |
| **Hub** | Bộ não: nhận tin, ra quyết định bật/tắt, lưu lịch sử | C# | `src/SmartHomeAI.Hub`, `src/SmartHomeAI.Core` |
| **Dashboard** | Trang web để xem và điều khiển | C# (Blazor) | `src/SmartHomeAI.Dashboard` |
| **ESP32** | Mạch đo nhiệt độ, ánh sáng, cửa và kêu còi | C++ | `firmware/esp32` |
| **Mô phỏng** | Chương trình giả làm ESP32, ổ cắm, camera để thử khi chưa có phần cứng | Python | `sim/` |

Các phần nói chuyện với nhau bằng **MQTT** (một kiểu nhắn tin qua "bưu điện" trung gian tên Mosquitto).

## Lộ trình

### Bậc 1. Chạy được và nhìn thấy nó hoạt động (nửa ngày)

Chưa cần hiểu mã. Mục tiêu: hệ thống chạy trên máy bạn và bạn thấy đèn tự bật trong mô phỏng.

- Làm theo [huong-dan-thuc-hien.md](huong-dan-thuc-hien.md), **Bài 1 đến Bài 3**.
- Đọc Mục 0 của [kien-thuc-nen-tang.md](kien-thuc-nen-tang.md) ("Bức tranh chung qua một buổi tối").

Bạn qua bậc này khi: mở được Dashboard, bật mô phỏng, chạy kịch bản SC01 và thấy dòng "Đèn đã bật" trong nhật ký.

### Bậc 2. Hiểu từng khối và cách chúng nhắn tin (2 đến 3 ngày)

- Đọc [kien-thuc-nen-tang.md](kien-thuc-nen-tang.md) Mục 1 đến 9 theo thứ tự. Gặp từ lạ tra Mục 19.
- Làm **Bài 4 và Bài 5** của hướng dẫn (xem tin MQTT bằng mắt, gọi API của Hub).
- Đọc [mqtt-topics.md](mqtt-topics.md): danh sách mọi loại tin và ai gửi cho ai.

Bạn qua bậc này khi: nhìn một dòng tin `home/sensor/esp1/env` và nói được ai gửi, ai nhận, Hub làm gì với nó.

### Bậc 3. Ghép phần cứng thật (3 đến 5 ngày)

- **Bài 6 đến Bài 8** của hướng dẫn: camera, ổ cắm P100, ESP32.
- [nhat-ky-camera-tapo.md](nhat-ky-camera-tapo.md): những lỗi đã gặp với camera thật và cách xử lý.
- [tapo-p100.md](tapo-p100.md): chi tiết ổ cắm P100.

Bạn qua bậc này khi: camera lên hình trên Dashboard, bấm nút thì ổ cắm thật đóng cắt, ESP32 gửi nhiệt độ thật.

### Bậc 4. Đọc mã, sửa luật, kiểm thử, bảo vệ (1 đến 2 tuần)

- **Bài 9 đến Bài 11** của hướng dẫn: đọc mã theo một luồng, chạy test, tự sửa một luật.
- [kien-thuc-nen-tang.md](kien-thuc-nen-tang.md) Mục 10 đến 18 (phần khó: đồng thời, kiểm thử, bảo mật, thống kê).
- [kich-ban-thuc-te.md](kich-ban-thuc-te.md): 20 kịch bản thực tế và những gì chúng phát hiện.
- [nhiem-vu.md](nhiem-vu.md): 10 nhiệm vụ của đề tài và 12 kịch bản phải qua.
- [on-tap-bao-ve.md](on-tap-bao-ve.md): 170 câu hỏi hội đồng có gợi ý trả lời.
- [thuyet-minh-do-an.md](thuyet-minh-do-an.md): bản thuyết minh đồ án.

## Tra nhanh: tôi muốn... thì đọc gì

| Tôi muốn | Đọc |
|---|---|
| Cài và chạy trên máy mới | Hướng dẫn, Bài 1 |
| Biết một từ lạ nghĩa là gì | Kiến thức nền tảng, Mục 19 |
| Biết tin MQTT nào có trường gì | `mqtt-topics.md` và thư mục `schemas/` |
| Camera không lên hình, nhận sai tư thế | Hướng dẫn Bài 6, rồi `nhat-ky-camera-tapo.md` |
| Ổ cắm không điều khiển được | Hướng dẫn Bài 7, rồi `tapo-p100.md` |
| Biết phần nào đã làm, phần nào chưa | Hướng dẫn, mục "Tình trạng hiện tại" |
| Chuẩn bị bảo vệ | `on-tap-bao-ve.md` và `thuyet-minh-do-an.md` |

## Lưu ý về độ mới của tài liệu

`kien-thuc-nen-tang.md` và `on-tap-bao-ve.md` viết ở giai đoạn toàn bộ còn chạy bằng mô phỏng. Từ ngày
21/11/2026 camera Tapo C222 và ổ cắm Tapo P100 đã chạy thật, firmware ESP32 đã viết (chưa nạp thử trên mạch).
Chỗ nào hai tài liệu đó ghi "chưa chạy thật" cho camera và P100 thì hiểu theo mục "Tình trạng hiện tại" của
hướng dẫn, đó là nơi cập nhật mới nhất.
