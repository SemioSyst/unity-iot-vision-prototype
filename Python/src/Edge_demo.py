import cv2

# ---------- 参数 ----------
CAM_INDEX = 0          # 若打不开可改为 1
FRAME_W, FRAME_H = 1280, 720   # C922 常用分辨率：1280x720 或 1920x1080
USE_GAUSSIAN = True    # 默认开启高斯模糊降噪

# ---------- 摄像头 ----------
cap = cv2.VideoCapture(CAM_INDEX)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, FRAME_W)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, FRAME_H)
cap.set(cv2.CAP_PROP_FPS, 60)

if not cap.isOpened():
    raise RuntimeError("无法打开摄像头：请检查连接、索引（CAM_INDEX）或被占用情况")

# ---------- UI：阈值滑块 ----------
cv2.namedWindow("Edges", cv2.WINDOW_NORMAL)
cv2.resizeWindow("Edges", 960, 540)  # 预设窗口大小，避免过大
cv2.createTrackbar("Thresh1", "Edges", 50, 300, lambda x: None) # 最小阈值
cv2.createTrackbar("Thresh2", "Edges", 150, 300, lambda x: None) # 最大阈值
cv2.createTrackbar("Blur ksize(odd)", "Edges", 3, 15, lambda x: None)  # 1/3/5/...

print("✅ 摄像头已启动：按 'q' 退出，'s' 保存当前边缘图，空格键切换是否模糊降噪。")

save_idx = 0

while True:
    ok, frame = cap.read()
    if not ok:
        print("⚠️ 读取失败，可能摄像头被占用。")
        break

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # 读取滑块
    t1 = cv2.getTrackbarPos("Thresh1", "Edges")
    t2 = cv2.getTrackbarPos("Thresh2", "Edges")
    k  = cv2.getTrackbarPos("Blur ksize(odd)", "Edges")
    if k % 2 == 0:
        k += 1
    if k < 1:
        k = 1

    # 可选高斯模糊降噪（边缘更稳定）
    if USE_GAUSSIAN and k >= 3:
        gray = cv2.GaussianBlur(gray, (k, k), 0)

    edges = cv2.Canny(gray, threshold1=t1, threshold2=t2) # 计算边缘

    # 拼接显示：左原图，右边缘
    vis = cv2.hconcat([
        cv2.resize(frame, (640, 360)),
        cv2.cvtColor(cv2.resize(edges, (640, 360)), cv2.COLOR_GRAY2BGR)
    ])
    cv2.imshow("Edges - press 'q' to quit, 's' to save", vis)

    key = cv2.waitKey(1) & 0xFF
    if key == ord('q'):
        break
    elif key == ord('s'):
        cv2.imwrite(f"edges_{save_idx}.png", edges)
        print(f"💾 已保存 edges_{save_idx}.png")
        save_idx += 1
    elif key == ord(' '):  # 切换降噪
        USE_GAUSSIAN = not USE_GAUSSIAN
        print(f"🔁 高斯模糊降噪：{'开' if USE_GAUSSIAN else '关'}")

cap.release()
cv2.destroyAllWindows()
