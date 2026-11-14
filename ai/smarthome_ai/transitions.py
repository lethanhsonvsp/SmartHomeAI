"""Chuyển vùng -> transition. Chỉ quan sát: hướng suy từ cặp vùng, Hub mới quyết định ý nghĩa."""
ROOM = {"livingroom", "sofa"}
EXIT = {"door", "other_exit"}


def direction(frm: str, to: str) -> str:
    if frm in ROOM and to in EXIT:
        return "out"
    if frm in EXIT and to in ROOM:
        return "in"
    return "unknown"


def step(track, zone: str | None, confirm=3):
    """Cập nhật track.zone; trả (from, to) khi vùng mới đã ổn định `confirm` khung, không thì None."""
    if zone is None:
        return None
    if track.zone is None:
        track.zone = zone
        return None
    if zone == track.zone:
        track.pending_zone, track.pending_n = None, 0
        return None
    if zone != track.pending_zone:
        track.pending_zone, track.pending_n = zone, 0
    track.pending_n += 1
    if track.pending_n < confirm:
        return None
    frm, track.zone, track.pending_zone, track.pending_n = track.zone, zone, None, 0
    return frm, zone
