# Nhiệm vụ phần mềm Smart Home AI

**Mục tiêu:** hệ thống nhà thông minh tự chủ: nhận diện hành động, theo dõi hiện diện theo khu vực, học thói quen, tự điều khiển thiết bị, cảnh báo quên tắt điện khi đi vắng.

**Môi trường:** Linux Server 16 GB RAM, không GPU rời. ASP.NET Core, Blazor, Python AI, PostgreSQL, MQTT, MATLAB. Chạy dịch vụ bằng systemd.

**Lưu ý:** nằm sofa và nhà vắng là hai bài khó nhất; kiểm chứng bằng dữ liệu thật trước khi đặt mục tiêu chính xác. Một camera không biết có người ở phòng ngoài vùng quan sát. Phần cứng chưa có: làm trước bằng mô phỏng (`sim/`).

## Kiến trúc
Camera RTSP → Python AI → Hub (ASP.NET Core) ⇄ MQTT ⇄ ESP32 ×2; Hub → adapter P100 (đèn, quạt); Hub ⇄ PostgreSQL; Blazor Dashboard ⇄ Hub; MATLAB đọc dữ liệu xuất ra.

## 10 nhiệm vụ
- [ ] **NV01 AI Camera và nhận diện hành động** (trọng tâm AI): đọc RTSP Tapo C222; phát hiện và theo dõi nhiều người; phân vùng phòng khách, sofa, cửa chính, lối sang khu khác; phân biệt đứng, ngồi, nằm sofa, di chuyển; hướng đi qua cửa, độ tin cậy, lịch sử chuyển vùng.
- [ ] **NV02 Occupancy Engine** (trọng tâm AI): có người trong phòng khách hay không; chuyển phòng khác rời nhà; nhiều người; kết hợp camera với cảm biến cửa MC-38; trạng thái `OCCUPIED`, `OTHER_AREA`, `EXIT_PENDING`, `AWAY_CONFIRMED`, `UNKNOWN`.
- [ ] **NV03 Habit Engine** (trọng tâm nghiên cứu): ghi thời gian, hành động, nhiệt độ, độ sáng, lần bật/tắt; phân tích thói quen đèn, quạt; đề xuất ngưỡng; học từ điều khiển thủ công; cho duyệt, sửa, tắt luật học được.
- [ ] **NV04 Automation Engine**: có người + tối → bật đèn; nằm sofa + nóng → bật quạt; nằm sofa + hợp thói quen → bật quạt; sang phòng khác → không kết luận đi vắng; nhà vắng + thiết bị bật → cảnh báo; thời gian chờ, chống bật/tắt liên tục, ưu tiên lệnh thủ công.
- [ ] **NV05 IoT và Device Manager**: Mosquitto MQTT; nhận SHT30, BH1750, MC-38 từ ESP32; lệnh buzzer; hai ổ Tapo P100 qua adapter LAN; theo dõi kết nối, xử lý mất kết nối.
- [ ] **NV06 Alert Manager**: thiết bị còn bật khi đủ bằng chứng nhà vắng; xác minh đi vắng ~30 giây; cảnh báo đầu sau 2 phút, nhắc lại sau 5 phút; tùy chọn tự tắt sau 10 phút; hủy khi người về hoặc thiết bị tắt; lưu lịch sử đúng, sai, phản hồi.
- [ ] **NV07 Blazor Dashboard**: camera, vùng, trạng thái người; nhiệt độ, độ ẩm, lux, cửa; điều khiển đèn, quạt, Auto/Manual; cấu hình ngưỡng, thời gian cảnh báo, luật; lịch sử, biểu đồ, thói quen đã học.
- [ ] **NV08 PostgreSQL và Backend API**: người dùng, thiết bị, phòng, vùng camera; dữ liệu cảm biến, hành động, hiện diện; luật, thói quen, lịch sử điều khiển; API cho Dashboard và AI; phân quyền, bảo vệ API, sao lưu.
- [ ] **NV09 MATLAB nghiên cứu và đánh giá**: phân tích dữ liệu thực nghiệm; quan hệ nhiệt độ, ánh sáng, hành động, thói quen; tối ưu ngưỡng; độ chính xác nhận diện; so luật cố định với luật thích nghi; xuất biểu đồ, bảng cho luận văn.
- [ ] **NV10 Kiểm thử, triển khai, an toàn**: triển khai systemd; tải CPU/RAM khi chạy AI; độ trễ camera/cảm biến đến P100; thử mất Internet, MQTT, camera, server; nhiều người, mất dấu; thiết bị không tự bật lại trái lệnh thủ công.

## Kịch bản phải qua
| Mã | Kịch bản | Mong muốn |
|----|----------|-----------|
| SC01 | Vào phòng khách, trời tối | Đèn tự bật |
| SC02 | Vào phòng khách, trời sáng | Đèn giữ nguyên |
| SC03 | Nằm sofa, trời nóng | Quạt tự bật |
| SC04 | Nằm sofa, nhiệt độ mát | Xét thói quen |
| SC05 | Rời sofa sang khu khác | Không kết luận đi vắng |
| SC06 | Ra cửa rồi quay lại | Hủy xác minh đi vắng |
| SC07 | Ra ngoài, đèn/quạt còn bật | Cảnh báo |
| SC08 | Hai người, một người ra ngoài | Không kết luận nhà vắng |
| SC09 | Tắt quạt thủ công | AI không bật lại ngay |
| SC10 | Camera mất kết nối | Không suy luận nhà trống |
| SC11 | Người trở về | Hủy cảnh báo còn hiệu lực |
| SC12 | Đổi thói quen | Cập nhật đề xuất sau khi đủ dữ liệu |

## Công nghệ
C# / .NET 10: Hub, thiết bị, Automation, Habit. Blazor: giao diện. Python + OpenCV + mô hình AI: camera, tracking, hành động. Mosquitto: MQTT. PostgreSQL: lưu trữ. MATLAB: phân tích. Linux + systemd: chạy 24/7.

## Thứ tự làm
Luồng lõi trước: camera → hành động → quyết định → P100 → lịch sử (dùng mô phỏng trong `sim/`). Học thói quen nâng cao sau.
1. Mô phỏng `sim/` (ESP32, P100, camera phát lại) + topic MQTT
2. NV05, NV08 (nền)
3. NV02, NV04 theo SC01–SC11 bằng test
4. NV01 với video thật
5. NV06, NV07
6. NV03, NV09 (gồm SC12)
7. NV10
