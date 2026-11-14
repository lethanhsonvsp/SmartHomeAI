# MATLAB (NV09)
Script phân tích dữ liệu thực nghiệm. Chưa có dữ liệu thật; script mới chỉ thử trên DỮ LIỆU GIẢ.

## Quy trình
1. Xuất CSV từ PostgreSQL (cần `SMARTHOME_PG`, ví dụ `postgresql://user:pass@host/smarthome`):
   `cd tools && python3 -m venv .venv && .venv/bin/pip install -r requirements.txt`
   `SMARTHOME_PG=... .venv/bin/python export_csv.py --from 2026-11-01 --to 2026-12-01 --out data/real`
2. Nhãn thật cho phần (c): tự ghi `truth_labels.csv` với hai cột `ts` (ISO UTC `2026-11-01T01:00:00Z`) và `truth` (`home` hoặc `away`), mỗi dòng là lúc trạng thái thật đổi.
3. Trong MATLAB, thư mục `matlab/`: sửa `dataDir`, `truthFile` trong `run_all.m` rồi chạy `run_all`. Kết quả vào `matlab/out/` (gitignore): `a_*`, `b_*`, `c_*` (PNG và CSV).

## Script
- `a_survey.m`: nhiệt độ/lux trung bình theo giờ; tương quan và tỷ lệ bật quạt theo nhiệt độ, đèn theo lux.
- `b_threshold.m`: ngưỡng thích nghi = trung vị nhiệt độ/lux lúc bật tay; so luật cố định (28 °C, 50 lux) với luật thích nghi bằng số phút lệch hành vi người dùng và số lần bật/tắt sai (chỉ tính lúc `Occupied`).
- `c_occupancy_eval.m`: so `occupancy_log` với nhãn thật mỗi phút: độ chính xác, ma trận nhầm lẫn, tỷ lệ báo vắng sai. Chưa đánh giá `ai_event` (nhận diện hành động) theo từng sự kiện.
- `load_csv.m`, `state_at.m`: hàm phụ. Không cần toolbox ngoài MATLAB cơ bản (cần R2018b+ cho `xline`; `readtable`, `datetime`).

## Dữ liệu giả để thử
`cd tools && .venv/bin/python make_synthetic.py` tạo `tools/data/SYNTHETIC_DU_LIEU_GIA/` (14 ngày, nhãn nằm trong `truth_labels_SYNTHETIC.csv`). DỮ LIỆU GIẢ: chỉ để thử script, không dùng cho kết luận luận văn. Test định dạng cột: `cd tools && .venv/bin/pytest tests`.
