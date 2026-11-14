# E2E SC01-SC11

Mosquitto thật + Hub thật (`dotnet run --no-build`, cổng 5099, không Postgres) + sim phát kịch bản; kiểm qua bus `home/#` và `/api/state`.

```
docker compose up -d mosquitto
dotnet build src/SmartHomeAI.Hub          # một lần
sim/.venv/bin/python -m pytest tests/e2e -v
```
Tự bỏ qua nếu không có broker/dotnet/bản build. Hub dùng đồng hồ thật (xác minh vắng 30 s, không cấu hình được) nên kịch bản chạy speed 2, `Alerts__FirstAfter=00:00:05`; cả bộ ~5 phút. SC07/SC11 đặt retained `device/light/state=on` để có thiết bị bật (không chạy p100). Mỗi test xóa retained và mở Hub mới.
