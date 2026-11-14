# E2E SC01-SC11

Mosquitto thật + Hub thật (`dotnet run --no-build`, cổng 5099, không Postgres) + sim phát kịch bản; kiểm qua bus `home/#` và `/api/state`.

```
docker compose up -d mosquitto
dotnet build src/SmartHomeAI.Hub          # một lần
sim/.venv/bin/python -m pytest tests/e2e -v
```
Tự bỏ qua nếu không có broker/dotnet/bản build. Hub dùng đồng hồ thật nên test đặt `Occupancy__VerifyAway=00:00:05`, `Occupancy__StateStale=00:00:03`, `Alerts__FirstAfter=00:00:05` và chạy kịch bản speed 8; fixture chờ /health rồi gửi tin thăm dò đến khi Hub nhận được MQTT. SC07/SC11 đặt retained `device/light/state=on` để có thiết bị bật (không chạy p100). Mỗi test xóa retained và mở Hub mới.

## Chống nhiễu và dồn dập
Cấu hình Hub dùng chung ở `hubkit.py` (cổng 5098, `HUB_ENV` nén thời gian: StartupGrace 1 s, SofaLyingMin 1 s, ManualBlock 15 s, VerifyAway 5 s...; ManualPath sang thư mục tmp riêng từng test; p100 giả khởi động riêng). `test_e2e.py` dùng cổng 5097 (đổi bằng `E2E_HUB_PORT`). Broker riêng: đặt `E2E_MQTT_PORT` (ví dụ `mosquitto -p 18830`) khi broker chung có Hub mồ côi giữ client id `smarthome-hub`; Hub cũ còn chạy sẽ làm test sai, kiểm `pgrep -af SmartHomeAI.Hub` trước.
- `test_noise.py`: N01–N08 đếm lệnh `set` trên bus + đọc `/api/events`; nhóm `test_robust_to_noise` chạy SC01/03/07/08/09 với `--noise mild|moderate`, seed 7, kỳ vọng như bản sạch.
- `test_burst.py`: S01–S06 bấm dồn dập qua `POST /api/device/*`, đo khoảng cách thật giữa các lệnh (in min/median với `-s`).
- `test_safety_invariants.py`: `FUZZ_N` (12) chuỗi ngẫu nhiên nhiễu nặng (`sim/fuzz.py`), mỗi chuỗi một Hub mới; 5 bất biến (khoảng cách set >= 3 s; đổi auto <= 1/10 s và <= 6/giờ; không auto trong thời gian chặn tay; không quá 3 lệnh trùng trạng thái liền nhau; Hub còn sống). Vi phạm in seed + 20 sự kiện quanh điểm lỗi; chạy lại: `python -m sim fuzz --seed N`. `-s` để xem thống kê.
Test chờ tính năng chưa có trong src dùng `xfail(strict=False)` tự gỡ khi grep thấy tính năng.
