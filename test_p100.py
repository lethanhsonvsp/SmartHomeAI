"""Kiểm tra P100 qua LAN; mặc định chỉ đọc trạng thái."""
import argparse
import asyncio
import getpass
import json
import os
import sys

from src.tapo_p100 import TapoError, TapoP100Service


async def run(args):
    username = os.environ.get("TAPO_USERNAME")
    password = os.environ.get("TAPO_PASSWORD")
    if not username or not password:
        if not sys.stdin.isatty():
            raise ValueError("Cần TAPO_USERNAME và TAPO_PASSWORD trong môi trường không tương tác.")
        username = username or input("TP-Link ID (email): ")
        password = password or getpass.getpass("Mật khẩu TP-Link ID: ")
    service = TapoP100Service(args.host, username, password, args.timeout)
    status = await service.get_status()
    print(json.dumps({"ok": True, "on": status.on}), flush=True)
    if args.control:
        if not sys.stdin.isatty():
            raise ValueError("Thử bật/tắt cần terminal tương tác để xác nhận.")
        desired = args.control == "on"
        if input(f"Xác nhận chuyển sang {args.control.upper()}? Nhập YES: ") != "YES":
            print("Đã hủy.", file=sys.stderr)
            return
        await (service.turn_on() if desired else service.turn_off())
        result = await service.get_status()
        print(json.dumps({"ok": True, "on": result.on}), flush=True)
        if result.on != desired:
            raise TapoError("state_mismatch", "Trạng thái đọc lại không khớp lệnh.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="192.168.0.8")
    parser.add_argument("--timeout", type=float, default=10)
    parser.add_argument("--control", choices=("on", "off"), help="đọc trạng thái rồi yêu cầu xác nhận")
    args = parser.parse_args()
    try:
        asyncio.run(run(args))
    except (TapoError, ValueError) as exc:
        print(json.dumps({"ok": False, "error": getattr(exc, "code", "configuration"), "message": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("Đã hủy.", file=sys.stderr)
        return 130
    return 0


if __name__ == "__main__":
    sys.exit(main())
