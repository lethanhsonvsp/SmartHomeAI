import csv, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
import make_synthetic as m, export_csv as e

SQLDIR = os.path.join(os.path.dirname(__file__), "..", "..", "infra", "sql")
SQL = "".join(open(os.path.join(SQLDIR, f), encoding="utf-8").read() for f in ("001-init.sql", "002-alert.sql"))

def sql_cols(table):
    body = re.search(rf"CREATE TABLE IF NOT EXISTS {table} \((.*?)\);", SQL, re.S).group(1)
    names = [c.strip().split()[0].strip('"') for c in body.split(",")]
    return [n for n in names if n not in ("id", "pk")]

def test_columns_match_sql_and_export():
    for t, (_, cols) in e.TABLES.items():
        assert [c.strip('"') for c in cols] == m.HEADERS[t]
        if t != "alert_log":  # alert_log giu cot id (ma canh bao), chi bo pk
            assert m.HEADERS[t] == sql_cols(t)
    assert m.HEADERS["alert_log"] == ["id", "raised_at", "cancelled_at", "reason", "feedback"]

def test_generate_and_write(tmp_path):
    rows = m.generate(days=3, seed=2)
    m.write(str(tmp_path), rows)
    assert (tmp_path / "README_DU_LIEU_GIA.txt").exists()
    for name, hdr in m.HEADERS.items():
        fn = f"{name}_SYNTHETIC.csv" if name == "truth_labels" else f"{name}.csv"
        r = list(csv.reader(open(tmp_path / fn, encoding="utf-8")))
        assert r[0] == hdr and len(r) > 1 and all(len(x) == len(hdr) for x in r)
    assert len(rows["sensor_reading"]) == 3 * 1440
    assert any(r[3] == "manual" for r in rows["device_command_log"])
