#!/bin/sh
# Chạy AI từ bất kỳ thư mục nào: ai/run.sh --source 0 --detector pose --zones ai/config/zones.example.json ...
d=$(cd "$(dirname "$0")" && pwd)
PYTHONPATH="$d" exec "$d/.venv/bin/python" -m smarthome_ai "$@"
