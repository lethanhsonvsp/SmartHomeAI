"""Private stdin/stdout bridge for the Hub. One login per process, no retries."""
import asyncio
import json
import os
import sys

from . import TapoError, TapoP100Service


def reply(value):
    print(json.dumps(value, separators=(',', ':')), flush=True)


async def main():
    try:
        service = TapoP100Service(os.environ['TAPO_HOST'], os.environ['TAPO_USERNAME'], os.environ['TAPO_PASSWORD'], float(os.environ.get('TAPO_TIMEOUT', '10')))
        status = await service.get_status()
        reply({'ok': True, 'on': status.on})
        while line := await asyncio.to_thread(sys.stdin.readline):
            command = json.loads(line)
            if command == 'status':
                pass
            elif command == 'on':
                await service.turn_on()
            elif command == 'off':
                await service.turn_off()
            else:
                raise ValueError('invalid command')
            status = await service.get_status()
            reply({'ok': True, 'on': status.on})
    except TapoError as exc:
        reply({'ok': False, 'error': exc.code})
    except Exception:
        reply({'ok': False, 'error': 'bridge_error'})


if __name__ == '__main__':
    asyncio.run(main())
