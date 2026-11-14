"""Sinh CSV DU LIEU GIA cung dinh dang export_csv.py, de thu script MATLAB.

DU LIEU GIA: khong dung cho ket luan luan van. Ten thu muc/tep deu mang nhan SYNTHETIC.
Dung: python make_synthetic.py --out data/SYNTHETIC_DU_LIEU_GIA --days 14
Them truth_labels_SYNTHETIC.csv (ts,truth home|away) cho phan danh gia nhan dien.
"""
import argparse, csv, json, math, os, random
from datetime import datetime, timedelta, timezone

TZ = timezone(timedelta(hours=7))
USER_FAN_ON, USER_FAN_OFF = 29.5, 27.5   # "thoi quen that" gia lap: khac luat co dinh 28
USER_LIGHT_ON = 30.0                      # nguoi dung bat den khi lux < 30 (luat co dinh: 50)

def z(t):
    return t.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

def generate(days=14, seed=1, start=datetime(2026, 11, 1, tzinfo=TZ)):
    rnd = random.Random(seed)
    rows = {k: [] for k in ["sensor_reading", "ai_event", "door_event", "device_command_log",
                            "occupancy_log", "alert_log", "truth_labels"]}
    fan = light = False
    prev_truth = prev_state = None
    day_off = 0.0
    for i in range(days * 1440):
        t = start + timedelta(minutes=i)
        if i % 1440 == 0:
            day_off = rnd.gauss(0, 1.5)
        h = t.hour + t.minute / 60
        temp = 27 + 4.5 * math.sin((h - 9) / 24 * 2 * math.pi) + day_off + rnd.gauss(0, .3)
        lux = max(2.0, 450 * math.sin((h - 6) / 12 * math.pi)) if 6 <= h <= 18 else 3.0
        lux = max(1.0, lux * rnd.uniform(.6, 1.0) + rnd.gauss(0, 2))
        rows["sensor_reading"].append([z(t), "esp1", round(temp, 2), round(60 + rnd.gauss(0, 3), 1), round(lux, 1)])
        home = not (t.weekday() < 5 and 8 <= h < 17)
        truth = "home" if home else "away"
        if truth != prev_truth:
            rows["truth_labels"].append([z(t), truth])
            prev_truth = truth
            rows["door_event"] += [[z(t), "esp2", "true"], [z(t + timedelta(seconds=20)), "esp2", "false"]]
            d = "in" if home else "out"
            rows["ai_event"].append([z(t), "transition", json.dumps(
                {"ts": z(t), "track_id": 1, "from_zone": "door", "to_zone": "livingroom", "direction": d, "conf": 0.8})])
        # he thong nhan dien: vang xac nhan tre 5 phut; thinh thoang bao vang sai khi dang o nha
        state = "Occupied" if home else ("ExitPending" if t.hour == 8 and t.minute < 5 and t.weekday() < 5 else "AwayConfirmed")
        if home and rnd.random() < 0.0004:
            state = "AwayConfirmed"
        if state != prev_state:
            rows["occupancy_log"].append([z(t), state])
            prev_state = state
        if not home:
            continue
        # hanh vi tay: quat theo nhiet do (co tre), den theo lux
        if not fan and temp >= USER_FAN_ON + rnd.gauss(0, .4):
            fan = True; rows["device_command_log"].append([z(t), "fan", "true", "manual"])
        elif fan and temp <= USER_FAN_OFF + rnd.gauss(0, .4):
            fan = False; rows["device_command_log"].append([z(t), "fan", "false", "manual"])
        if not light and lux < USER_LIGHT_ON:
            light = True; rows["device_command_log"].append([z(t), "light", "true", "manual"])
        elif light and lux > USER_LIGHT_ON * 3:
            light = False; rows["device_command_log"].append([z(t), "light", "false", "manual"])
    # vai canh bao: dung (quen tat khi ra ngoai) va sai (nguoi van o nha)
    for k, day in enumerate(range(0, days, 3)):
        t = start + timedelta(days=day, hours=8, minutes=10)
        wrong = k % 2 == 1
        rows["alert_log"].append([f"light-{day}", z(t), z(t + timedelta(minutes=3)) if wrong else "",
                                  "returned" if wrong else "", "wrong" if wrong else "correct"])
    return rows

HEADERS = {
    "sensor_reading": ["ts", "node", "temp_c", "humidity", "lux"], "ai_event": ["ts", "kind", "payload"],
    "door_event": ["ts", "node", "open"], "device_command_log": ["ts", "device", "on", "source"],
    "occupancy_log": ["ts", "state"], "alert_log": ["id", "raised_at", "cancelled_at", "reason", "feedback"],
    "truth_labels": ["ts", "truth"],
}

def write(out, rows):
    os.makedirs(out, exist_ok=True)
    for name, data in rows.items():
        fn = f"{name}_SYNTHETIC.csv" if name == "truth_labels" else f"{name}.csv"
        with open(os.path.join(out, fn), "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f); w.writerow(HEADERS[name]); w.writerows(data)
    with open(os.path.join(out, "README_DU_LIEU_GIA.txt"), "w", encoding="utf-8") as f:
        f.write("DU LIEU GIA sinh bang tools/make_synthetic.py. Khong dung cho ket luan luan van.\n")

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--out", default="data/SYNTHETIC_DU_LIEU_GIA")
    p.add_argument("--days", type=int, default=14)
    p.add_argument("--seed", type=int, default=1)
    a = p.parse_args()
    write(a.out, generate(a.days, a.seed))
    print("Da ghi du lieu GIA vao", a.out)
