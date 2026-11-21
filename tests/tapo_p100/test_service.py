import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import unittest

from src.tapo_p100 import TapoError, TapoP100Service


class ServiceTests(unittest.TestCase):
 def test_status_and_commands_share_session(self):
     async def exercise():
         device = SimpleNamespace(get_device_info=AsyncMock(return_value=SimpleNamespace(device_on=False)), on=AsyncMock(), off=AsyncMock())
         client = SimpleNamespace(p100=AsyncMock(return_value=device))
         with patch("src.tapo_p100.service.ApiClient", return_value=client):
             service = TapoP100Service("192.168.0.8", "user", "secret")
             assert not (await service.get_status()).on
             await service.turn_on()
             await service.turn_off()
         client.p100.assert_awaited_once()
         device.on.assert_awaited_once()
         device.off.assert_awaited_once()
     asyncio.run(exercise())


 def test_auth_failure_is_not_retried_or_exposed(self):
     async def exercise():
         client = SimpleNamespace(p100=AsyncMock(side_effect=Exception("TPAP_CREDENTIALS secret")))
         with patch("src.tapo_p100.service.ApiClient", return_value=client):
             service = TapoP100Service("192.168.0.8", "user", "secret")
             with self.assertRaises(TapoError) as error:
                 await service.get_status()
             assert error.exception.code == "auth_credentials"
             assert "secret" not in str(error.exception)
             with self.assertRaises(TapoError):
                 await service.get_status()
         client.p100.assert_awaited_once()
     asyncio.run(exercise())


 def test_timeout(self):
     async def exercise():
         async def slow(_host):
             await asyncio.sleep(1)
         with patch("src.tapo_p100.service.ApiClient", return_value=SimpleNamespace(p100=slow)):
             service = TapoP100Service("192.168.0.8", "user", "secret", .01)
             with self.assertRaises(TapoError) as error:
                 await service.get_status()
             assert error.exception.code == "timeout"
     asyncio.run(exercise())
