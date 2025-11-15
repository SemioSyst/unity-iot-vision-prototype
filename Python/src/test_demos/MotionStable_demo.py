import cv2
import numpy as np
from collections import deque

# ---------- Config ----------
CAM_INDEX = 0
W, H = 1280, 720
HISTORY = 300
VAR_T = 10
LR = 0.004              # MOG2 学习率，静景可更小
MIN_AREA = 800          # 连通域最小面积
MAX_AGE = 20            # 允许丢失帧数
MIN_HITS = 6            # 新目标确认所需连续命中帧
MERGE_IOU = 0.3         # 合并框的 IOU 阈值
TRAIL = 24              # 轨迹长度
USE_SHADOWS = False     # 关阴影更干净

# ---------- Utils ----------
def iou(a, b):
    ax1, ay1, aw, ah = a; ax2, ay2 = ax1+aw, ay1+ah
    bx1, by1, bw, bh = b; bx2, by2 = bx1+bw, by1+bh
    inter_w = max(0, min(ax2, bx2) - max(ax1, bx1))
    inter_h = max(0, min(ay2, by2) - max(ay1, by1))
    inter = inter_w * inter_h
    union = aw*ah + bw*bh - inter
    return inter/union if union>0 else 0.0

def merge_boxes(boxes, iou_th=0.3):
    # 简单贪心合并：两两合并直到稳定
    boxes = boxes[:]
    changed = True
    while changed:
        changed = False
        out = []
        used = [False]*len(boxes)
        for i in range(len(boxes)):
            if used[i]: continue
            a = boxes[i]
            for j in range(i+1, len(boxes)):
                if used[j]: continue
                b = boxes[j]
                if iou(a,b) >= iou_th:
                    # 合并为并集框
                    ax, ay, aw, ah = a; bx, by, bw, bh = b
                    x1, y1 = min(ax, bx), min(ay, by)
                    x2, y2 = max(ax+aw, bx+bw), max(ay+ah, by+bh)
                    a = (x1, y1, x2-x1, y2-y1)
                    used[j] = True
                    changed = True
            used[i] = True
            out.append(a)
        boxes = out
    return boxes

# 简易多目标跟踪器：IOU 匹配 + 年龄/命中计数 + 轨迹
class Track:
    _next_id = 1
    def __init__(self, box):
        self.id = Track._next_id; Track._next_id += 1
        self.box = box
        self.hits = 1
        self.age = 0   # 连续丢失帧数
        self.trail = deque(maxlen=TRAIL)
        cx, cy = box[0]+box[2]//2, box[1]+box[3]//2
        self.trail.append((cx, cy))
    def update(self, box):
        # 平滑更新
        bx, by, bw, bh = self.box
        nx, ny, nw, nh = box
        self.box = (int(0.7*bx+0.3*nx), int(0.7*by+0.3*ny),
                    int(0.7*bw+0.3*nw), int(0.7*bh+0.3*nh))
        self.hits += 1
        self.age = 0
        cx = self.box[0]+self.box[2]//2
        cy = self.box[1]+self.box[3]//2
        self.trail.append((cx, cy))

class Tracker:
    def __init__(self, iou_th=0.3, max_age=10, min_hits=3):
        self.tracks = []
        self.iou_th = iou_th
        self.max_age = max_age
        self.min_hits = min_hits
    def step(self, detections):
        # 1) 先把所有轨迹 age+1（缺省认为丢失）
        for t in self.tracks:
            t.age += 1
        # 2) 用 IOU 贪心匹配
        unmatched = set(range(len(detections)))
        for t in self.tracks:
            # 找与 t IOU 最高的候选
            best_j, best_iou = -1, 0
            for j in list(unmatched):
                iou_val = iou(t.box, detections[j])
                if iou_val > best_iou:
                    best_iou, best_j = iou_val, j
            if best_iou >= self.iou_th:
                t.update(detections[best_j])
                unmatched.discard(best_j)
        # 3) 对未匹配的检测，新建轨迹
        for j in unmatched:
            self.tracks.append(Track(detections[j]))
        # 4) 清理长期丢失的轨迹
        self.tracks = [t for t in self.tracks if t.age <= self.max_age]
        # 返回“已确认”的轨迹（命中次数足够）
        return [t for t in self.tracks if t.hits >= self.min_hits]

# ---------- Main ----------
cap = cv2.VideoCapture(CAM_INDEX)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, W)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, H)
if not cap.isOpened():
    raise RuntimeError("Camera open failed")

mog2 = cv2.createBackgroundSubtractorMOG2(history=HISTORY, varThreshold=VAR_T, detectShadows=USE_SHADOWS)
tracker = Tracker(iou_th=0.3, max_age=MAX_AGE, min_hits=MIN_HITS)

print("q 退出，r 重置背景。")

while True:
    ok, frame = cap.read()
    if not ok: break
    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    gray = cv2.GaussianBlur(gray, (5,5), 0)

    fg = mog2.apply(gray, learningRate=LR)
    # 阈值化（去阴影/噪声）
    _, fg = cv2.threshold(fg, 200, 255, cv2.THRESH_BINARY)
    # 形态学清理
    fg = cv2.morphologyEx(fg, cv2.MORPH_OPEN, np.ones((3,3), np.uint8), iterations=1)
    fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((5,5), np.uint8), iterations=1)

    # 连通域
    num, labels, stats, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
    dets = []
    for i in range(1, num):  # 0 是背景
        x,y,w,h,area = stats[i]
        if area < MIN_AREA: continue
        # 几何过滤：过于扁/细、填充率低的过滤掉
        bbox_area = w*h
        solidity = area / float(bbox_area + 1e-6)
        aspect = w / float(h + 1e-6)
        if solidity < 0.4: continue
        if aspect < 0.2 or aspect > 5.0: continue
        dets.append((x,y,w,h))

    # 合并重叠框
    dets = merge_boxes(dets, iou_th=MERGE_IOU)

    # 跟踪
    confirmed = tracker.step(dets)

    # 画图
    out = frame.copy()
    for t in confirmed:
        x,y,w,h = t.box
        cv2.rectangle(out, (x,y), (x+w,y+h), (0,200,255), 2)
        cv2.putText(out, f"ID {t.id}", (x, y-6), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0,200,255), 2)
        # 轨迹
        for i in range(1, len(t.trail)):
            cv2.line(out, t.trail[i-1], t.trail[i], (255,100,0), 2)

    vis = cv2.hconcat([cv2.resize(out, (640,360)), cv2.cvtColor(cv2.resize(fg,(640,360)), cv2.COLOR_GRAY2BGR)])
    cv2.imshow("motion (left=tracks, right=mask)", vis)

    k = cv2.waitKey(1) & 0xFF
    if k == ord('q'): break
    if k == ord('r'):
        mog2 = cv2.createBackgroundSubtractorMOG2(history=HISTORY, varThreshold=VAR_T, detectShadows=USE_SHADOWS)
        tracker = Tracker(iou_th=0.3, max_age=MAX_AGE, min_hits=MIN_HITS)
        print("背景&跟踪重置")

cap.release()
cv2.destroyAllWindows()
