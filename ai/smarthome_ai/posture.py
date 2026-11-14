"""Posture bằng quy tắc hình học. CHỈ LÀ BASELINE, ngưỡng đoán, cần đo và chỉnh bằng video thật.

Hộp: tỉ lệ rộng/cao (đã quy về pixel bằng aspect khung) -> standing / sitting / lying.
moving: tâm dịch nhiều (đơn vị chiều cao hộp mỗi giây) so với khung trước.
Có keypoints COCO-17 thì dùng góc thân (vai->hông) và khoảng hông-gối.
"""
import math

MOVE_SPEED = 0.5   # chiều cao hộp / giây
LYING_AR = 1.1     # rộng/cao
STAND_AR = 0.55


def _kp_posture(kp, aspect=16 / 9):
    """Tọa độ chuẩn hóa nên x nhân aspect để so với y theo pixel."""
    if not kp or len(kp) < 17:
        return None
    ok = lambda i: kp[i][2] >= 0.3
    mid = lambda a, b: ((kp[a][0] + kp[b][0]) / 2 * aspect, (kp[a][1] + kp[b][1]) / 2)
    hips = [i for i in (11, 12) if ok(i)]
    sh = [i for i in (5, 6) if ok(i)]
    if not hips or not sh:
        return None
    hx, hy = mid(hips[0], hips[-1])
    sx, sy = mid(sh[0], sh[-1])
    dx, dy = abs(hx - sx), abs(hy - sy)
    if dx > dy:  # thân gần nằm ngang
        return "lying"
    if ok(0) and abs(kp[0][1] - hy) < dx * 0.5 and abs(kp[0][1] - sy) < dx * 0.5:  # đầu-vai-hông cùng độ cao
        return "lying"
    knees = [i for i in (13, 14) if ok(i)]
    if knees:
        ky = sum(kp[i][1] for i in knees) / len(knees)
        # hông gần mức đầu gối (đùi nằm ngang) -> ngồi; chân duỗi thẳng xuống -> đứng
        return "sitting" if abs(ky - hy) < 0.6 * dy else "standing"
    return None


def classify(box, speed: float, aspect: float = 16 / 9, keypoints=None) -> str:
    """speed: chiều cao hộp/giây (0 nếu chưa biết). aspect = rộng/cao khung hình."""
    if speed > MOVE_SPEED:
        return "moving"
    kp = _kp_posture(keypoints, aspect)
    if kp:
        return kp
    w, h = (box[2] - box[0]) * aspect, box[3] - box[1]
    if h <= 0:
        return "unknown"
    ar = w / h
    return "lying" if ar > LYING_AR else "standing" if ar < STAND_AR else "sitting"


def smooth(history: list, new: str, n=8, prev: str | None = None, min_frames=4) -> str:
    """Bỏ phiếu số đông n khung gần nhất. Chưa đủ min_frames -> unknown; hòa phiếu -> giữ tư thế đang phát (prev), chưa có -> unknown."""
    history.append(new)
    del history[:-n]
    if len(history) < min_frames:
        return "unknown"
    top = max(history.count(x) for x in set(history))
    win = {x for x in history if history.count(x) == top}
    return win.pop() if len(win) == 1 else (prev or "unknown")
