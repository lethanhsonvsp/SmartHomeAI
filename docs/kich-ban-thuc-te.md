# Kịch bản thực tế R01–R20

Mã trong `sim/realistic.py`, đăng ký chung với SC/N: `python -m sim list`, `python -m sim run R03 --speed 40`, `python -m sim list --json` (mảng `code,title,description,durationSec,expect,requires` cho Dashboard; `requires`: `p100` = cần P100 giả/thật để thiết bị phản hồi, `esp` = cần ESP/`sim esp` cho buzzer và cửa).
Kiểm tự động: `tests/e2e/test_realistic.py` (Hub thật nén thời gian; cổng Hub `E2E_RHUB_PORT` mặc định 5096; nên dùng broker riêng qua `E2E_MQTT_PORT`). Kỳ vọng phản ánh luật hiện hành (`AutomationEngine`, `OccupancyEngine`, `AlertManager`), không chỉnh cho dễ qua.

Nén thời gian e2e: speed 40 (R18 12, R20 50); Hub dùng thời gian thật: VerifyAway 5 s, FirstAfter 5 s, RemindEvery 6 s, ManualBlock 15 s, StateStale 3 s. Cấu hình thật (VerifyAway 30 s, FirstAfter 2 phút, RemindEvery 5 phút) quy đổi theo tỉ lệ khác nhau, nên con số mô phỏng chỉ để kiểm logic.

## Bảng kết quả (lần chạy 15/11/2026: 20/20 đạt, 525 s)

| Mã | Tình huống (thời lượng mô phỏng) | Kỳ vọng | Kết quả |
|---|---|---|---|
| R01 | Sáng sớm vào phòng khách tối, mở rèm (5 phút) | Đèn auto bật đúng 1 lần, lux tăng không tắt/bật thêm, không quạt | Đạt |
| R02 | Tối về nhà, vào cửa, ngồi sofa (5 phút) | Đèn bật 1 lần, không quạt (ngồi, dưới 28 °C) | Đạt |
| R03 | Trưa nóng 30→33 °C, nằm xem TV, ngủ quên (15 phút) | Quạt auto bật 1 lần, không đèn, không lệnh tắt | Đạt |
| R04 | Đêm ngủ sofa, camera IR nhiễu tư thế, nhiệt 27→24 (15 phút) | Đèn ≤1 lần, không quạt, không AwayConfirmed | Đạt |
| R05 | Đi vệ sinh 2 phút (vẫn thấy ở lối sang khu khác) (10 phút) | Không AwayConfirmed, không cảnh báo | Đạt |
| R06 | Ra cửa nhận hàng 1 phút (5 phút) | Không cảnh báo | Đạt (Phát hiện 3) |
| R07 | Đi làm quên quạt+đèn, về sớm (15 phút) | Cảnh báo first, nhắc lại cùng id, hủy person_returned, không tự tắt | Đạt |
| R08 | Đi vắng cả ngày để đèn (25 phút) | 1 first, ≥2 nhắc đều (khoảng 6 s thật, không dồn), không hủy, không tự tắt | Đạt |
| R09 | Khách đến (2 người), một người ra về (10 phút) | Không AwayConfirmed, không cảnh báo | Đạt |
| R10 | Cả nhà ra ngoài cách nhau 10 s (12 phút) | AwayConfirmed + cảnh báo first | Đạt |
| R11 | AI offline 90 s khi xem TV (10 phút) | Không cảnh báo, không tắt quạt, đèn không lệnh, quạt ≤1 lệnh bật | Đạt |
| R12 | Camera lỗi/che ống kính 140 s (5 phút) | Thấy Unknown, không AwayConfirmed, không cảnh báo | Đạt |
| R13 | Nóng, người dùng tắt quạt tay (10 phút) | 1 lệnh manual, không auto quạt trong 15 s thật sau đó | Đạt |
| R14 | Bật đèn tay ban ngày lux 500 (7 phút) | 1 manual, không auto đèn, không tắt | Đạt |
| R15 | Nhiệt dao động Gauss 0,6 quanh 28 °C, nằm sofa (15 phút) | Quạt ≤1 lần bật, không tắt | Đạt |
| R16 | Mưa chiều 31→25 °C (20 phút) | Quạt bật 1 lần, không bật thêm, không tự tắt | Đạt (Phát hiện 1) |
| R17 | Mất điện: ổ cắm offline 3 phút, về lại ở trạng thái tắt (15 phút) | Không dồn lệnh (khoảng cách set ≥2,5 s), quạt bật ≤3, Hub sống | Đạt: 2 lệnh bật quạt auto (Phát hiện 4) |
| R18 | Hub khởi động lại khi nằm sofa nóng (5 phút, speed 12) | Không auto trước StartupGrace, sau đó bật quạt ≤1 | Đạt: lệnh đầu ở 8,0 s sau khi chạy Hub (grace 6 s) |
| R19 | Trẻ chạy nhảy sofa/phòng khách (10 phút) | Mỗi thiết bị ≤2 lệnh set, không tắt | Đạt |
| R20 | Ngày dài: đêm, sáng, ngủ trưa nóng, đi vắng, tối (50 phút, speed 50) | Đèn 1–2, quạt 1–2, không tắt, tổng set ≤8, first khi vắng, hủy khi về | Đạt: tổng 2 lệnh set (1 đèn, 1 quạt), 1 cảnh báo first |

`pytest sim` (có `sim/tests/test_realistic.py`: schema, ts không giảm, mô tả không rỗng, `list --json`): R đều xanh; riêng `test_scenarios.py::test_p100_publishes_state_on_set` đỏ khi chạy chung, không liên quan R (nghi tranh broker 1883 với phiên khác đang chạy Hub).

## Phát hiện về hành vi hệ thống

1. **Không có luật tắt** (R16, R07, R08, R20): mưa làm mát 25 °C quạt vẫn chạy; ra khỏi nhà đèn/quạt chỉ bị cảnh báo, không tự tắt (AutoOff mặc định tắt); người về rồi thiết bị vẫn bật. Đúng luật hiện hành ("only turns devices on") nhưng là khoảng trống trải nghiệm cần chủ quyết.
2. **Đèn không tắt khi người rời** (R02, R07, R20): chỉ nhắc khi AwayConfirmed và có thiết bị bật; sang khu khác (R05) đèn cũng giữ.
3. **R06 phụ thuộc cấu hình thời gian**: vắng 1 phút đủ AwayConfirmed theo mặc định thật (VerifyAway 30 s); chỉ không báo vì FirstAfter mặc định 2 phút. Nhận hàng quá khoảng 2,5 phút sẽ có cảnh báo oan.
4. **R17 sau mất điện**: ổ cắm về trạng thái tắt, Hub bật lại quạt 1 lần (tổng 2 lệnh auto), không coi là lệnh tay, không dồn lệnh. Hợp lý, nhưng nếu người dùng chủ ý cắt nguồn thì Hub tự bật lại.
5. **Chưa đo, nghi qua đọc mã** (không có kịch bản trong R): `OnTransition(out)` đặt bằng chứng rời; nếu người sang `other_exit` bị khuất hẳn khỏi khung hình (`persons` rỗng) thì sau VerifyAway thành AwayConfirmed dù chỉ vào nhà vệ sinh. R05 giữ người nhìn thấy ở lối đi nên không lộ; cần xác định AI thật có giữ track ở `other_exit` không.
