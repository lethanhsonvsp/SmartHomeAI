"""Xuat cac bang PostgreSQL ra CSV theo khoang thoi gian (NV09).

Chuoi ket noi lay tu bien moi truong SMARTHOME_PG (vd postgresql://user:pass@host/db).
Dung: SMARTHOME_PG=... python export_csv.py --from 2026-11-01 --to 2026-11-15 --out data/real
"""
import argparse, csv, os, sys
from datetime import datetime, timezone

# bang -> (cot thoi gian, cot xuat); khop infra/sql/*.sql, bo cot id/pk
TABLES = {
    "sensor_reading": ("ts", ["ts", "node", "temp_c", "humidity", "lux"]),
    "ai_event": ("ts", ["ts", "kind", "payload"]),
    "door_event": ("ts", ["ts", "node", "open"]),
    "device_command_log": ("ts", ["ts", "device", '"on"', "source"]),
    "occupancy_log": ("ts", ["ts", "state"]),
    "alert_log": ("raised_at", ["id", "raised_at", "cancelled_at", "reason", "feedback"]),
}

def fmt(v):
    if isinstance(v, datetime):
        return v.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return "" if v is None else str(v)

def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--from", dest="start", required=True, help="YYYY-MM-DD (UTC, gom)")
    p.add_argument("--to", dest="end", required=True, help="YYYY-MM-DD (UTC, khong gom)")
    p.add_argument("--out", required=True)
    a = p.parse_args(argv)
    dsn = os.environ.get("SMARTHOME_PG")
    if not dsn:
        sys.exit("Thieu bien moi truong SMARTHOME_PG")
    import psycopg  # import muon de test khong can DB
    os.makedirs(a.out, exist_ok=True)
    with psycopg.connect(dsn) as conn:
        for table, (tcol, cols) in TABLES.items():
            q = f"SELECT {','.join(cols)} FROM {table} WHERE {tcol} >= %s AND {tcol} < %s ORDER BY {tcol}"
            with conn.cursor() as cur, open(os.path.join(a.out, table + ".csv"), "w", newline="", encoding="utf-8") as f:
                cur.execute(q, (a.start, a.end))
                w = csv.writer(f)
                w.writerow([c.strip('"') for c in cols])
                for row in cur:
                    w.writerow([fmt(v) for v in row])
                print(table, cur.rowcount, "dong")

if __name__ == "__main__":
    main()
