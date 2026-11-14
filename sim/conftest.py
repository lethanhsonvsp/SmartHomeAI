"""Cho phép `pytest` chạy từ trong sim/: đưa gốc repo vào sys.path để `import sim` được."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
