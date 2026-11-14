# E2E SC01-SC11

Mosquitto thật + Hub thật (`dotnet run --no-build`, cổng 5099, không Postgres) + sim phát kịch bản; kiểm qua bus `home/#` và `/api/state`.

```
docker compose up -d mosquitto
dotnet build src/SmartHomeAI.Hub          # một lần
sim/.venv/bin/python -m pytest tests/e2e -v
```
Tự bỏ qua nếu không có broker/dotnet/bản build. Hub dùng đồng hồ thật nên test đặt `Occupancy__VerifyAway=00:00:05`, `Occupancy__StateStale=00:00:03`, `Alerts__FirstAfter=00:00:05` và chạy kịch bản speed 8; fixture chờ /health rồi gửi tin thăm dò đến khi Hub nhận được MQTT. SC07/SC11 đặt retained `device/light/state=on` để có thiết bị bật (không chạy p100). Mỗi test xóa retained và mở Hub mới.
