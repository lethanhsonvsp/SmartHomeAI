# SmartHomeAI Dashboard

Một trang duy nhất (trang chủ): hiện diện, cảm biến, đèn/quạt (Auto/Manual), camera + bảng người đang thấy, cảnh báo Đúng/Sai, nhật ký quyết định.
`/camera` và `/alerts` chuyển hướng về trang chủ.

Ô "Nhật ký" (thay biểu đồ cũ) đọc `GET {Hub}/api/events?limit=50` mỗi 2 giây: dòng thời gian mới nhất trên cùng, cuộn trong ô, lọc Tất cả / Chỉ hành động / Chỉ lý do chờ; màu theo loại (auto xanh lá, wait vàng nhạt, manual xanh dương, alert đỏ). Dưới mỗi thiết bị hiện lý do chờ mới nhất hoặc "Vừa tự bật lúc hh:mm". Hub cũ trả 404 thì hiện "Hub chưa hỗ trợ nhật ký". Điện thoại có 5 tab: Tổng quan / Camera / Nhật ký / Cảnh báo / Mô phỏng.

Bố cục vừa khít màn hình, không cuộn trang (điện thoại dọc dùng tab ở cuối). Ngăn "Mô phỏng" bật/tắt ESP32 giả (`python -m sim esp --control-port 8091`, có thanh trượt nhiệt độ/độ ẩm/lux và công tắc cửa) và ổ cắm giả; lệnh lấy từ mục `Sim` trong `appsettings.json`.

Ổ cắm giả tự bật khi Dashboard khởi động (`Sim:AutoStartPlug`, mặc định true; `Sim:AutoStartEsp`, mặc định false) và tự dọn khi tắt. Nếu Hub báo ổ cắm chưa online, ô Đèn / quạt trần hiện cảnh báo kèm nút bật mô phỏng; bấm Bật/Tắt mà sau 3 giây trạng thái không đổi thì hiện "Chưa thấy thiết bị phản hồi".

Ô "Kịch bản" (trong ngăn/tab Mô phỏng): danh sách lấy từ `python -m sim list --json` (lệnh ở `Sim:ListArgs`, nhóm "Thực tế (R)") gộp với SC01..SC11 lấy từ `python -m sim list` (nhóm "Cơ bản (SC)", chỉ có mã nên tên/mô tả mặc định "Kịch bản cơ bản SCxx", coi như cần ổ cắm giả); N và S là bộ test chống nhiễu nên không hiện. Tải một lần khi mở trang, có nút tải lại khi lỗi. Chọn mã để xem mô tả và kỳ vọng, chọn tốc độ 1x/2x/5x/10x (mặc định 5x; Hub tính thời gian thật nên tốc độ cao có thể làm hệ thống không kịp phản ứng, dùng 1x để thấy đúng hành vi thật), bấm Chạy (`python -m sim run CODE --speed N`) hoặc Dừng. Mã phải khớp `^[A-Z]{1,2}[0-9]{2}$` và nằm trong danh sách đã lấy, tốc độ chỉ nhận bốn mức trên (kiểm lại trên server); chỉ một kịch bản chạy một lúc; Dashboard tắt thì tiến trình kịch bản bị dừng. Thanh tiến độ là ước tính (thời lượng/tốc độ), kèm trạng thái Đang chạy/Xong/Lỗi, vài dòng log cuối và 5 dòng nhật ký Hub mới nhất. Nút Chạy bị khóa khi AI camera đang chạy ("Tắt AI camera trước...") hoặc Hub chưa online; kịch bản có `requires` p100 thì tự bật ổ cắm giả trước. Kịch bản tự phát dữ liệu cảm biến nên `requires` esp bị bỏ qua: nếu ESP32 giả đang chạy thì bị tắt trong lúc chạy (kèm ghi chú) và không tự bật lại, sau khi xong hiện "ESP32 giả đã tắt". Trong lúc chạy trang làm mới mỗi 1 giây thay vì 2 giây.

Chạy: `dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard` (mở http://localhost:5037).
Dashboard tự bật Mosquitto (docker compose, nếu 1883 chưa mở) và Hub (nếu `{Hub:BaseUrl}/health` không đáp); khi tắt Dashboard chỉ dừng tiến trình nó đã tạo.
Công tắc trên khung camera bật/tắt `ai/run.sh`. Lệnh chạy chỉ lấy từ `appsettings.json` (mục `Hub`, `Ai`, `Startup`, `Repo`), không nhận lệnh từ trình duyệt.

**Chỉ dùng trong mạng LAN.** Dashboard không có xác thực: ai vào được trang đều bật/tắt được AI và gửi lệnh tới Hub.
Nếu Hub đặt `Api:Key`, đặt cùng giá trị ở `Dashboard:ApiKey` để Dashboard gửi header `X-Api-Key` khi POST.
