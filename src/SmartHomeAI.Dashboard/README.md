# SmartHomeAI Dashboard

Một trang duy nhất (trang chủ): hiện diện, cảm biến, đèn/quạt (Auto/Manual), camera + bảng người đang thấy, cảnh báo Đúng/Sai.
`/camera` và `/alerts` chuyển hướng về trang chủ.

Bố cục vừa khít màn hình, không cuộn trang (điện thoại dọc dùng tab ở cuối). Ngăn "Mô phỏng" bật/tắt ESP32 giả (`python -m sim esp --control-port 8091`, có thanh trượt nhiệt độ/độ ẩm/lux và công tắc cửa) và ổ cắm giả; lệnh lấy từ mục `Sim` trong `appsettings.json`.

Chạy: `dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard` (mở http://localhost:5037).
Dashboard tự bật Mosquitto (docker compose, nếu 1883 chưa mở) và Hub (nếu `{Hub:BaseUrl}/health` không đáp); khi tắt Dashboard chỉ dừng tiến trình nó đã tạo.
Công tắc trên khung camera bật/tắt `ai/run.sh`. Lệnh chạy chỉ lấy từ `appsettings.json` (mục `Hub`, `Ai`, `Startup`, `Repo`), không nhận lệnh từ trình duyệt.

**Chỉ dùng trong mạng LAN.** Dashboard không có xác thực: ai vào được trang đều bật/tắt được AI và gửi lệnh tới Hub.
Nếu Hub đặt `Api:Key`, đặt cùng giá trị ở `Dashboard:ApiKey` để Dashboard gửi header `X-Api-Key` khi POST.
