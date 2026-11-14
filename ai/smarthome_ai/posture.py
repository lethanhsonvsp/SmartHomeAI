"""Posture bằng quy tắc hình học. CHỈ LÀ BASELINE, ngưỡng đoán, cần đo và chỉnh bằng video thật.

Hộp: tỉ lệ rộng/cao (đã quy về pixel bằng aspect khung) -> standing / sitting / lying.
moving: tâm dịch nhiều (đơn vị chiều cao hộp mỗi giây) so với khung trước.
Có keypoints COCO-17 thì dùng góc thân (vai->hông) và khoảng hông-gối.
"""
import math

MOVE_SPEED = 0.5   # chiều cao hộp / giây
LYING_AR = 1.1     # rộng/cao
STAND_AR = 0.55


def _kp_posture(kp):
    if not kp or len(kp) < 17:
        return None
    ok = lambda i: kp[i][2] >= 0.3
    if not all(ok(i) for i in (5, 6, 11, 12)):
        return None
    sx, sy = (kp[5][0] + kp[6][0]) / 2, (kp[5][1] + kp[6][1]) / 2
    hx, hy = (kp[11][0] + kp[12][0]) / 2, (kp[11][1] + kp[12][1]) / 2
    if abs(hy - sy) < abs(hx - sx):  # thân nằm ngang
        return "lying"
    if ok(13) and ok(14):
        ky = (kp[13][1] + kp[14][1]) / 2
        if abs(ky - hy) < 0.5 * abs(hy - sy):  # gối ngang hông
            return "sitting"
        return "standing"
    return None


def classify(box, speed: float, aspect: float = 16 / 9, keypoints=None) -> str:
    """speed: chiều cao hộp/giây (0 nếu chưa biết). aspect = rộng/cao khung hình."""
    if speed > MOVE_SPEED:
        return "moving"
    kp = _kp_posture(keypoints)
    if kp:
        return kp
    w, h = (box[2] - box[0]) * aspect, box[3] - box[1]
    if h <= 0:
        return "unknown"
    ar = w / h
    return "lying" if ar > LYING_AR else "standing" if ar < STAND_AR else "sitting"


def smooth(history: list, new: str, n=5) -> str:
    """Bỏ phiếu số đông n khung gần nhất để chống nhảy."""
    history.append(new)
    del history[:-n]
    return max(set(history), key=history.count)
