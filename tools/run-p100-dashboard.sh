#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
credential_file="${TAPO_CREDENTIAL_FILE:-$PWD/.tapo-p100-credentials}"
if [ "${1:-}" = "--setup" ]; then
    umask 077
    read -r -p 'TP-Link ID (email): ' tapo_user
    read -r -s -p 'Mật khẩu TP-Link ID: ' tapo_password
    printf '\n'
    if [ -z "$tapo_user" ] || [ -z "$tapo_password" ]; then
        echo 'Email và mật khẩu không được để trống.' >&2
        exit 1
    fi
    # Two plain lines; never source the file as shell code.
    if [[ "$tapo_user" == *$'\n'* || "$tapo_password" == *$'\n'* ]]; then
        echo 'Không hỗ trợ thông tin đăng nhập chứa ký tự xuống dòng.' >&2
        exit 1
    fi
    temp_file=$(mktemp "${credential_file}.XXXXXX")
    trap 'rm -f -- "$temp_file"' EXIT
    printf '%s\n%s\n' "$tapo_user" "$tapo_password" > "$temp_file"
    chmod 600 "$temp_file"
    mv -f -- "$temp_file" "$credential_file"
    trap - EXIT
    unset tapo_user tapo_password
    echo 'Đã lưu thông tin đăng nhập cục bộ.'
    exit 0
fi
if [ "$#" -ne 0 ]; then
    echo "Cách dùng: $0 [--setup]" >&2
    exit 1
fi
if [ ! -f "$credential_file" ]; then
    echo "Chưa có thông tin đăng nhập. Chạy $0 --setup trước." >&2
    exit 1
fi
{
    IFS= read -r TAPO_USERNAME
    IFS= read -r TAPO_PASSWORD
} < "$credential_file"
if [ -z "$TAPO_USERNAME" ] || [ -z "$TAPO_PASSWORD" ]; then
    echo 'Tệp thông tin đăng nhập không hợp lệ; chạy lại --setup.' >&2
    exit 1
fi
export TAPO_USERNAME TAPO_PASSWORD
export TapoP100__Enabled=true
export TapoP100__DeviceId="${TapoP100__DeviceId:-light}"
export TapoP100__Host="${TapoP100__Host:-192.168.0.8}"
exec dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard
