#!/usr/bin/env bash
# Cài mọi thứ cần có trên Linux (Debian/Ubuntu) rồi chạy được Dashboard. Chạy lại nhiều lần cũng an toàn.
#   ./run.sh          cài/kiểm tra môi trường
#   ./run.sh start    cài xong thì chạy luôn Dashboard
# Sau đó: dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard   (mở http://localhost:5037)
set -euo pipefail
cd "$(dirname "$0")"

say() { printf '\n== %s\n' "$*"; }
SUDO=""; [ "$(id -u)" -ne 0 ] && SUDO="sudo"
APT_UPDATED=""
apt_install() {
  command -v apt-get >/dev/null || { echo "Không có apt-get: cài tay: $*"; exit 1; }
  [ -n "$APT_UPDATED" ] || { $SUDO apt-get update -y; APT_UPDATED=1; }
  $SUDO apt-get install -y "$@"
}

say "1/6 .NET SDK 10"
export PATH="$HOME/.dotnet:$PATH"
if ! dotnet --version 2>/dev/null | grep -q '^10\.'; then
  command -v curl >/dev/null || apt_install curl ca-certificates
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  grep -q '.dotnet' "$HOME/.profile" 2>/dev/null || echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.profile"
fi
dotnet --version

say "2/6 Python 3.11+ và venv"
if ! python3 -c 'import sys,venv,ensurepip; sys.exit(sys.version_info < (3, 11))' 2>/dev/null; then
  apt_install python3 python3-venv python3-pip
fi
python3 --version

say "3/6 Docker và Compose (chạy Mosquitto, PostgreSQL)"
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  apt_install docker.io docker-compose-v2
fi
if ! docker info >/dev/null 2>&1; then
  $SUDO usermod -aG docker "$USER" || true
  echo "Chưa dùng được docker không cần sudo. Đăng xuất rồi đăng nhập lại (hoặc chạy: newgrp docker), sau đó chạy lại ./run.sh"
  exit 1
fi
docker compose version

say "4/6 Python venv: sim/ (mô phỏng) và ai/ (camera)"
mkvenv() { # $1=thư mục, $2...=lệnh pip
  local d=$1; shift
  [ -x "$d/.venv/bin/python" ] || python3 -m venv "$d/.venv"
  "$d/.venv/bin/pip" install -q --upgrade pip
  "$d/.venv/bin/pip" install -q "$@"
}
mkvenv sim -r sim/requirements.txt
# ai/run.sh chạy qua PYTHONPATH nên chỉ cần phụ thuộc, không cài gói (pip install -e ai lỗi vì ai/ có nhiều thư mục cấp đầu)
mapfile -t AI_DEPS < <(python3 -c "import tomllib; print(*tomllib.load(open('ai/pyproject.toml','rb'))['project']['dependencies'], sep='\n')")
mkvenv ai "${AI_DEPS[@]}"
ai/.venv/bin/python ai/tools/fetch_model.py   # mô hình pose (bỏ qua nếu đã có, kiểm sha256)

say "5/6 Dựng mã .NET"
dotnet build SmartHomeAI.slnx -v q --nologo

say "6/6 Mosquitto (MQTT broker)"
docker compose up -d mosquitto

printf '\nXong. Chạy Dashboard:\n  dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard\nrồi mở http://localhost:5037 (Dashboard tự bật Hub và mô phỏng).\n'
if [ "${1:-}" = start ]; then
  exec dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard
fi
