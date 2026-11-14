# SmartHomeAI Dashboard

Một trang duy nhất (trang chủ): hiện diện, cảm biến, đèn/quạt (Auto/Manual), camera + bảng người đang thấy, cảnh báo Đúng/Sai, nhật ký quyết định.
`/camera` và `/alerts` chuyển hướng về trang chủ.

Ô "Nhật ký" (thay biểu đồ cũ) đọc `GET {Hub}/api/events?limit=50` mỗi 2 giây: dòng thời gian mới nhất trên cùng, cuộn trong ô, lọc Tất cả / Chỉ hành động / Chỉ lý do chờ; màu theo loại (auto xanh lá, wait vàng nhạt, manual xanh dương, alert đỏ). Dưới mỗi thiết bị hiện lý do chờ mới nhất hoặc "Vừa tự bật lúc hh:mm". Hub cũ trả 404 thì hiện "Hub chưa hỗ trợ nhật ký". Điện thoại có 5 tab: Tổng quan / Camera / Nhật ký / Cảnh báo / Mô phỏng.

Bố cục vừa khít màn hình, không cuộn trang (điện thoại dọc dùng tab ở cuối). Ngăn "Mô phỏng" bật/tắt ESP32 giả (`python -m sim esp --control-port 8091`, có thanh trượt nhiệt độ/độ ẩm/lux và công tắc cửa) và ổ cắm giả; lệnh lấy từ mục `Sim` trong `appsettings.json`.

Ổ cắm giả tự bật khi Dashboard khởi động (`Sim:AutoStartPlug`, mặc định true; `Sim:AutoStartEsp`, mặc định false) và tự dọn khi tắt. Nếu Hub báo ổ cắm chưa online, ô Đèn / quạt trần hiện cảnh báo kèm nút bật mô phỏng; bấm Bật/Tắt mà sau 3 giây trạng thái không đổi thì hiện "Chưa thấy thiết bị phản hồi".

Chạy: `dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard` (mở http://localhost:5037).
Dashboard tự bật Mosquitto (docker compose, nếu 1883 chưa mở) và Hub (nếu `{Hub:BaseUrl}/health` không đáp); khi tắt Dashboard chỉ dừng tiến trình nó đã tạo.
Công tắc trên khung camera bật/tắt `ai/run.sh`. Lệnh chạy chỉ lấy từ `appsettings.json` (mục `Hub`, `Ai`, `Startup`, `Repo`), không nhận lệnh từ trình duyệt.

**Chỉ dùng trong mạng LAN.** Dashboard không có xác thực: ai vào được trang đều bật/tắt được AI và gửi lệnh tới Hub.
Nếu Hub đặt `Api:Key`, đặt cùng giá trị ở `Dashboard:ApiKey` để Dashboard gửi header `X-Api-Key` khi POST.
