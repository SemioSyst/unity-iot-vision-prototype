import cv2
import numpy as np
from collections import deque

# ---------------- Config ----------------
CAM_INDEX = 0              # 摄像头索引，打不开就试 1
FRAME_W, FRAME_H = 1280, 720
USE_MOG2 = True            # True=使用MOG2背景建模；False=简单帧差
HISTORY = 300              # MOG2历史帧数
VAR_THRESHOLD = 16         # MOG2灵敏度（阈值）
DETECT_SHADOWS = True      # MOG2是否启用阴影检测
MIN_AREA = 800             # 连通域最小面积（像素）
SMOOTH_K = 5               # 高斯核大小（奇数）
DILATE_ITER = 2            # 膨胀迭代次数（连通更好）
DRAW_TRAIL = True          # 画质心轨迹
TRAIL_LEN = 32             # 轨迹长度

# ---------------- Camera ----------------
cap = cv2.VideoCapture(CAM_INDEX)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, FRAME_W)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, FRAME_H)

if not cap.isOpened():
    raise RuntimeError("无法打开摄像头：检查连接/索引/占用情况。")

# 背景模型 & UI
mog2 = cv2.createBackgroundSubtractorMOG2(history=HISTORY,
                                          varThreshold=VAR_THRESHOLD,
                                          detectShadows=DETECT_SHADOWS)
cv2.namedWindow("Motion", cv2.WINDOW_NORMAL)
cv2.resizeWindow("Motion", 960, 540)
cv2.createTrackbar("min_area", "Motion", MIN_AREA, 20000, lambda x: None)
cv2.createTrackbar("blur(odd)", "Motion", SMOOTH_K, 21, lambda x: None)
cv2.createTrackbar("dilate", "Motion", DILATE_ITER, 10, lambda x: None)
if USE_MOG2:
    cv2.createTrackbar("varT", "Motion", VAR_THRESHOLD, 100, lambda x: None)
    cv2.createTrackbar("history", "Motion", HISTORY, 2000, lambda x: None)

print("✅ 运动检测已启动：'m' 切换算法，'r' 重置背景，'s' 保存，'q' 退出。")

prev_gray = None
trail = deque(maxlen=TRAIL_LEN)

while True:
    ok, frame = cap.read()
    if not ok:
        print("⚠️ 读取失败，可能摄像头被占用。")
        break

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # UI参数
    MIN_AREA = max(0, cv2.getTrackbarPos("min_area", "Motion"))
    k = cv2.getTrackbarPos("blur(odd)", "Motion") or 1
    if k % 2 == 0: k += 1
    dil_iter = cv2.getTrackbarPos("dilate", "Motion")

    if k >= 3:
        gray = cv2.GaussianBlur(gray, (k, k), 0)

    # 选择方法
    if USE_MOG2:
        VAR_THRESHOLD = cv2.getTrackbarPos("varT", "Motion") or 1
        HISTORY = max(1, cv2.getTrackbarPos("history", "Motion"))
        mog2.setVarThreshold(VAR_THRESHOLD)
        mog2.setHistory(HISTORY)

        fg = mog2.apply(gray)                      # 前景掩码（含阴影=127）
        _, fg = cv2.threshold(fg, 200, 255, cv2.THRESH_BINARY)  # 去掉阴影
    else:
        if prev_gray is None:
            prev_gray = gray
            cv2.imshow("Motion", frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break
            continue
        diff = cv2.absdiff(gray, prev_gray)
        _, fg = cv2.threshold(diff, 25, 255, cv2.THRESH_BINARY)
        prev_gray = gray

    # 形态学：去噪并连通
    fg = cv2.dilate(fg, None, iterations=dil_iter)

    # 找轮廓并画框
    contours, _ = cv2.findContours(fg, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    centers = []
    for c in contours:
        if cv2.contourArea(c) < MIN_AREA:
            continue
        x, y, w, h = cv2.boundingRect(c)
        cv2.rectangle(frame, (x, y), (x+w, y+h), (0, 200, 255), 2)
        cx, cy = x + w//2, y + h//2
        centers.append((cx, cy))
        cv2.circle(frame, (cx, cy), 3, (0, 255, 0), -1)

    # 轨迹线
    if DRAW_TRAIL and centers:
        trail.append(centers[0])  # 取最大目标也可：max(contours, key=cv2.contourArea)
        for i in range(1, len(trail)):
            cv2.line(frame, trail[i-1], trail[i], (255, 100, 0), 2)

    # 可视化拼接
    vis_mask = cv2.cvtColor(cv2.resize(fg, (640, 360)), cv2.COLOR_GRAY2BGR)
    vis_frame = cv2.resize(frame, (640, 360))
    vis = cv2.hconcat([vis_frame, vis_mask])
    cv2.imshow("Motion - 按 'q' 退出, 'm' 切换算法，'r' 重置背景，'s' 保存", vis)

    key = cv2.waitKey(1) & 0xFF
    if key == ord('q'):
        break
    elif key == ord('m'):
        USE_MOG2 = not USE_MOG2
        print(f"🔁 切换算法：{'MOG2背景建模' if USE_MOG2 else '帧差法'}")
        prev_gray = None
    elif key == ord('r'):
        mog2 = cv2.createBackgroundSubtractorMOG2(history=HISTORY,
                                                  varThreshold=VAR_THRESHOLD,
                                                  detectShadows=DETECT_SHADOWS)
        prev_gray = None
        trail.clear()
        print("♻️ 背景已重置")
    elif key == ord('s'):
        cv2.imwrite("motion_frame.png", frame)
        cv2.imwrite("motion_mask.png", fg)
        print("💾 已保存 motion_frame.png / motion_mask.png")

cap.release()
cv2.destroyAllWindows()
