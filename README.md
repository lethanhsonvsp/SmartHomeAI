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
