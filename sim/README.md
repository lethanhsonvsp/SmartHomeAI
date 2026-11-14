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
- `python -m sim run SC07 --speed 20`: phát kịch bản (AI state/transition, env, door, status), nhanh gấp 20. `--dry`: chỉ kiểm schema, không cần broker. `ts` trong payload là thời gian mô phỏng, nên với `--speed` > 1 sẽ chạy trước đồng hồ thật.
- `python -m sim esp --node esp1 --interval 10`: ESP32 giả, env mỗi 10 s, status retained + LWT, nhận `buzzer/set` (in ra). Gõ `o` hoặc `c` + Enter để mở/đóng cửa (publish door retained).
- `python -m sim p100`: P100 giả, nghe `home/device/{light,fan}/set`, publish `state` retained, status retained + LWT.

Xem luồng: `mosquitto_sub -t 'home/#' -v`.

## Kịch bản
Dạng dữ liệu trong `sim/scenarios.py` (hàm dựng `Event(t, topic, payload, retain)`). Kịch bản chỉ phát sự kiện AI/cảm biến; Hub và `p100` phản ứng. SC09 có thêm một lệnh `manual` giả lập người dùng tắt quạt. SC12 (thói quen) chưa có.

## Test
`sim/.venv/bin/python -m pytest sim` (không cần broker): payload mọi kịch bản hợp schema, thời gian không giảm, seq tăng.
