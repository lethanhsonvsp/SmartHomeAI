"""One session per service instance; no implicit retries or credential logging."""
import asyncio
import ipaddress
from dataclasses import dataclass

from tapo import ApiClient


class TapoError(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code


@dataclass(frozen=True)
class P100Status:
    on: bool


class TapoP100Service:
    def __init__(self, host: str, username: str, password: str, timeout: float = 10):
        ip = ipaddress.ip_address(host)
        if not ip.is_private or ip.is_loopback or ip.is_link_local or ip.is_multicast or ip.is_unspecified:
            raise ValueError("Cần địa chỉ IP LAN riêng hợp lệ.")
        if not username or not password or not 0 < timeout <= 120:
            raise ValueError("Thiếu thông tin đăng nhập hoặc timeout không hợp lệ.")
        self.host, self.timeout = str(ip), timeout
        self._client = ApiClient(username, password)
        self._device = None
        self._failed = False
        self._lock = asyncio.Lock()

    async def _call(self, operation):
        try:
            return await asyncio.wait_for(operation(), timeout=self.timeout)
        except TimeoutError:
            raise TapoError("timeout", "Hết thời gian chờ thiết bị; kiểm tra IP và mạng LAN.") from None
        except Exception as exc:
            message = str(exc)
            if "TPAP_AUTH_ATTEMPTS_LIMIT" in message:
                code, detail = "auth_locked", "Thiết bị tạm khóa đăng nhập; chờ trước khi thử lại."
            elif "TPAP_CREDENTIALS" in message:
                code, detail = "auth_credentials", "Thiết bị từ chối thông tin xác thực; kiểm tra TP-Link ID và mật khẩu."
            elif "TPAP_HASH_MISMATCH" in message:
                code, detail = "auth_device_proof", "Không xác minh được thiết bị; kiểm tra IP và thông tin xác thực."
            elif "SESSION_TIMEOUT" in message:
                code, detail = "session_expired", "Phiên đã hết hạn; tạo phiên mới khi phù hợp."
            else:
                code, detail = "device_error", "Không giao tiếp được với thiết bị; kiểm tra mạng, IP và giao thức."
            raise TapoError(code, detail) from None

    async def _connect(self):
        if self._failed:
            raise TapoError("connection_failed", "Phiên kết nối đã thất bại; không tự thử lại.")
        if self._device is None:
            try:
                self._device = await self._call(lambda: self._client.p100(self.host))
            except TapoError:
                self._failed = True
                raise
        return self._device

    async def get_status(self) -> P100Status:
        async with self._lock:
            device = await self._connect()
            info = await self._call(device.get_device_info)
            return P100Status(on=info.device_on)

    async def turn_on(self) -> None:
        async with self._lock:
            device = await self._connect()
            await self._call(device.on)

    async def turn_off(self) -> None:
        async with self._lock:
            device = await self._connect()
            await self._call(device.off)
