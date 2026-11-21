# Smart Home AI

Nhà thông minh tự chủ: nhận diện hành động, theo dõi hiện diện, học thói quen, tự điều khiển đèn/quạt, cảnh báo quên tắt khi đi vắng. Chi tiết: `docs/nhiem-vu.md`.

## Cấu trúc
- `src/SmartHomeAI.Core` — miền (Occupancy, Automation, Alert, Habit). Logic và test đã có; Habit chưa nối vào Hub.
- `src/SmartHomeAI.Hub` — ASP.NET Core API, MQTT, thiết bị.
- `src/SmartHomeAI.Dashboard` — Blazor (server + client, Auto).
- `ai/` — Python: camera RTSP, tracking, hành động.
- `sim/` — mô phỏng ESP32, P100, camera (chưa có phần cứng).
- `matlab/` — phân tích thực nghiệm.
- `infra/` — Mosquitto, unit systemd. `docker-compose.yml` chỉ chạy PostgreSQL + Mosquitto.

## Chạy nhanh trên Linux (Debian/Ubuntu)
    ./run.sh      # cài .NET 10, Python venv, Docker, mô hình pose, dựng mã, bật Mosquitto
    dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard   # mở http://localhost:5037

## Chạy
    docker compose up -d
    dotnet build
    dotnet test
    dotnet run --project src/SmartHomeAI.Hub

## Chạy bằng Docker (máy chỉ cần Docker)

```bash
echo 'AI_SOURCE=rtsp://user:pass@IP:554/stream2' > .env   # nguồn camera; bỏ qua nếu chỉ chạy mô phỏng
docker compose up -d --build                              # mở http://localhost:5037
```

Bốn container: `postgres`, `mosquitto`, `hub` (Hub + cầu nối ổ cắm P100), `dashboard` (giao diện + AI camera + mô phỏng; mô hình YOLOv8m tải sẵn lúc build). Chạy nền (`restart: unless-stopped`); dừng bằng `docker compose stop`, xóa cả dữ liệu bằng `docker compose down -v`. Thêm mô hình: `AI_MODELS="yolov8m-pose yolov8s-pose"` trong `.env` rồi build lại. Đổi cổng giao diện: `DASHBOARD_PORT`. Hub trong container ở chế độ Development (Mosquitto dev cho kết nối ẩn danh); production cần khóa API và tài khoản MQTT.

Phát triển trên máy vẫn như cũ (`./run.sh`, `dotnet run` Dashboard); khi đó dừng container trùng cổng: `docker compose stop dashboard hub`.
