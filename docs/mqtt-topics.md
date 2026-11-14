# Hợp đồng MQTT

## Quyết định ranh giới (phương án A)
Python AI chỉ **quan sát**, gửi sự kiện qua MQTT. Hub (C#) giữ trạng thái Occupancy, ghép với cảm biến cửa MC-38, quyết định tự động và cảnh báo. Lý do: trạng thái ở một chỗ, test thuần C#, sim thay camera bằng cách publish sự kiện giả.

## Quy ước
- Payload JSON UTF-8. Thời gian `ts` là ISO 8601 UTC, ví dụ `2026-11-14T12:00:00Z`.
- QoS 1 cho mọi topic. `status` và `state` thiết bị: retained.
- Mỗi nguồn gửi `status` retained `online`, đặt LWT `offline` cùng topic.
- Hub coi nguồn `offline` hoặc quá hạn là mất dữ liệu: AI mất thì Occupancy về `UNKNOWN` (SC10).
- Schema JSON ở `docs/schemas/`.

## Topic
| Topic | Chiều | Tần suất | Schema |
|---|---|---|---|
| `home/sensor/{node}/env` | ESP32 → Hub | 10 s | `sensor-env.json` |
| `home/sensor/{node}/door` | ESP32 → Hub | khi đổi, retained | `sensor-door.json` |
| `home/sensor/{node}/status` | ESP32 → Hub | retained + LWT | `status.json` |
| `home/sensor/{node}/buzzer/set` | Hub → ESP32 | khi cần | `buzzer-set.json` |
| `home/device/{id}/set` | Hub → adapter P100 | khi cần | `device-set.json` |
| `home/device/{id}/state` | adapter → Hub | khi đổi, retained | `device-state.json` |
| `home/device/status` | adapter → Hub | retained + LWT | `status.json` |
| `home/ai/livingroom/state` | AI → Hub | 1–2 Hz | `ai-state.json` |
| `home/ai/livingroom/transition` | AI → Hub | khi có | `ai-transition.json` |
| `home/ai/status` | AI → Hub | retained + LWT | `status.json` |
| `home/alert/{id}` | Hub → mọi nơi | khi cảnh báo, không retained | chưa có schema |
| `home/alert/{id}/cancel` | Hub → mọi nơi | khi hủy | chưa có schema |

`{node}`: `esp1`, `esp2`. `{id}`: `light` (đèn), `fan` (quạt trần, treo trên trần phòng khách, không phải quạt đứng; thiết bị cắm qua ổ P100).
`env`: SHT30 (nhiệt độ, độ ẩm) và BH1750 (lux). `door`: MC-38. Chưa gán ESP nào mang cảm biến nào; chốt khi có phần cứng.

## Cảnh báo (Hub phát)
`{id}` dạng `alert-N`, đếm lại từ 1 sau khi Hub khởi động lại.
- `home/alert/{id}`: `{"ts","id","level":"first"|"reminder","devices_on":["light","fan"]}`. Mỗi lần phát, Hub cũng gửi `home/sensor/esp1/buzzer/set` với `on:true`.
- `home/alert/{id}/cancel`: `{"ts","id","reason":"person_returned"|"devices_off"|"data_lost"}`. Hub gửi buzzer `on:false`.
- Tự tắt thiết bị (mặc định tắt, bật bằng cấu hình `Alerts`): lệnh `home/device/{id}/set` với `source:"alert"`, `on:false`.
- API: `GET /api/alerts`, `POST /api/alerts/{id}/feedback {"correct":bool}` (404 nếu id lạ).

## Giá trị cho phép
- `zone`: `livingroom`, `sofa`, `door`, `other_exit`.
- `posture`: `standing`, `sitting`, `lying`, `moving`, `unknown`.
- `direction`: `in`, `out`, `unknown`.
- `source` của lệnh thiết bị: `auto`, `manual`, `alert`. Lệnh `manual` ưu tiên hơn `auto` (SC09).

## Ví dụ
`home/ai/livingroom/state`
```json
{"ts":"2026-11-14T12:00:00Z","seq":4812,"camera_ok":true,
 "persons":[{"track_id":3,"zone":"sofa","posture":"lying","conf":0.82}]}
```
`home/ai/livingroom/transition`
```json
{"ts":"2026-11-14T12:00:05Z","track_id":3,"from_zone":"livingroom","to_zone":"door","direction":"out","conf":0.74}
```
`home/sensor/esp1/env`
```json
{"ts":"2026-11-14T12:00:00Z","temp_c":31.4,"humidity":62.0,"lux":18}
```
`home/sensor/esp1/door`
```json
{"ts":"2026-11-14T12:00:05Z","open":true}
```
`home/device/fan/set`
```json
{"ts":"2026-11-14T12:00:01Z","on":true,"source":"auto"}
```
`home/device/fan/state`
```json
{"ts":"2026-11-14T12:00:02Z","on":true}
```
`home/ai/status` (và các `status`)
```json
{"ts":"2026-11-14T12:00:00Z","online":true}
```

## Chưa chốt
- Cảm biến nào gắn ESP nào.
- Xác thực MQTT (hiện `allow_anonymous true`, chỉ dùng dev).
- Hệ tọa độ vùng camera, thuộc NV01.
