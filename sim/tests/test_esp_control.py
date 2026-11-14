import json, urllib.error, urllib.request

import pytest

from sim.esp_control import State, serve


@pytest.fixture
def srv():
    st, seen = State(), []
    s = serve(st, 0, seen.append)  # cổng 0: hệ điều hành chọn
    yield st, seen, f"http://127.0.0.1:{s.server_address[1]}"
    s.shutdown(); s.server_close()


def call(url, body=None):
    req = urllib.request.Request(url, None if body is None else json.dumps(body).encode())
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, json.load(r)
    except urllib.error.HTTPError as e:
        return e.code, json.load(e)


def test_state_default(srv):
    assert call(srv[2] + "/state") == (200, {"tempC": 28.0, "humidity": 60.0, "lux": 150, "doorOpen": False, "buzzer": False})


def test_set_partial_and_callback(srv):
    st, seen, u = srv
    code, new = call(u + "/set", {"tempC": 35.5, "doorOpen": True})
    assert code == 200 and new["tempC"] == 35.5 and new["doorOpen"] and new["lux"] == 150
    assert seen == [{"tempC", "doorOpen"}]
    assert call(u + "/state")[1] == new
    st.v["buzzer"] = True
    assert call(u + "/state")[1]["buzzer"] is True


@pytest.mark.parametrize("bad", [{"tempC": 61}, {"tempC": "30"}, {"humidity": -1}, {"lux": 100001},
                                 {"doorOpen": 1}, {"tempC": True}, {"x": 1}, [1]])
def test_set_bad_400_no_change(srv, bad):
    _, _, u = srv
    before = call(u + "/state")[1]
    assert call(u + "/set", {"humidity": 70, **bad} if isinstance(bad, dict) else bad)[0] == 400
    assert call(u + "/state")[1] == before
