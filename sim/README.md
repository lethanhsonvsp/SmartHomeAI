# Mô phỏng phần cứng

Chưa có ESP32, P100, Tapo C222. Bộ mô phỏng Python publish/nhận đúng hợp đồng `docs/mqtt-topics.md`; mọi payload qua JSON schema (`docs/schemas/`) trước khi publish. Camera (phát lại video) chưa làm.

## Cài đặt
```
docker compose up -d mosquitto        # broker localhost:1883
python3 -m venv sim/.venv && sim/.venv/bin/pip install -r sim/requirements.txt
```
Chạy từ gốc repo, dùng `sim/.venv/bin/python -m sim ...` (tùy chọn chung `--host`, `--port` đặt trước lệnh con).

## Lệnh
- `python -m sim list`: liệt kê SC01–SC11.
- `python -m sim run SC07 --speed 20`: phát kịch bản (AI state/transition, env, door, status), nhanh gấp 20. `--dry`: chỉ kiểm schema, không cần broker. `--ts real` (mặc định): `ts` theo đồng hồ thật, các sự kiện cách nhau t/speed giây. `--ts sim`: `ts` cách nhau đúng t giây mô phỏng, nên với `--speed` > 1 sẽ chạy trước đồng hồ thật (dùng khi Hub tính theo `ts` và cần thấy 3 phút vắng thật). Với `real` + speed lớn, bộ đếm thời gian của Hub nếu dựa `ts` sẽ không thấy đủ 3 phút.
- `python -m sim esp --node esp1 --interval 10`: ESP32 giả, env mỗi 10 s, status retained + LWT, nhận `buzzer/set` (in ra). Gõ `o` hoặc `c` + Enter để mở/đóng cửa (publish door retained).
- `python -m sim esp --control-port 8091 [--env-interval 10] [--temp 28 --humidity 60 --lux 150 --door]`: thêm HTTP điều khiển chỉ nghe 127.0.0.1 (mặc định tắt). `GET /state` trả `{"tempC","humidity","lux","doorOpen","buzzer"}` (buzzer = lệnh `buzzer/set` gần nhất). `POST /set` nhận JSON một phần `{"tempC"(-20..60),"humidity"(0..100),"lux"(0..100000),"doorOpen"}`; sai kiểu/ngoài khoảng/trường lạ trả 400 và không đổi gì, thành công trả state mới. Đổi `doorOpen` publish ngay `door` (retained); đổi cảm biến publish ngay một `env`; còn lại env phát theo chu kỳ với giá trị hiện tại (không nhiễu). SIGTERM hoặc Ctrl+C: đóng cổng, publish status offline.
- `python -m sim p100`: P100 giả, nghe `home/device/{light,fan}/set`, publish `state` retained, status retained + LWT.

Xem luồng: `mosquitto_sub -t 'home/#' -v`.

## Kịch bản
Dạng dữ liệu trong `sim/scenarios.py` (hàm dựng `Event(t, topic, payload, retain)`). Kịch bản chỉ phát sự kiện AI/cảm biến; Hub và `p100` phản ứng. SC09 có thêm một lệnh `manual` giả lập người dùng tắt quạt. SC12 (thói quen) chưa có.

## Test
`sim/.venv/bin/python -m pytest sim` từ gốc repo, hoặc `pytest` từ trong `sim/` (có `conftest.py`). Không cần broker, trừ test p100 (tự bỏ qua nếu không có broker): payload mọi kịch bản hợp schema, thời gian không giảm, seq tăng.
