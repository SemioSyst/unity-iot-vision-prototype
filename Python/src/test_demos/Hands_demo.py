import cv2, time, math
import mediapipe as mp

# ---------- Camera config ----------
CAM_INDEX = 0
W, H = 1280, 720
TARGET_FPS = 30            # 仅作请求，是否成功需测量

# ---------- MediaPipe config ----------
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
mp_styles = mp.solutions.drawing_styles

# 检测/跟踪阈值：越高越严格（更稳但更容易漏）
DETECT_CONF = 0.6
TRACK_CONF  = 0.6
MAX_HANDS   = 2
MODEL_COMPLEXITY = 0       # 0 更快，1/2 更精细

# ---------- Utils ----------
# 在图像上绘制 FPS
def draw_fps(img, fps):
    cv2.putText(img, f"FPS: {fps:.1f}", (12, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255,255,255), 2)

# 在图像上绘制标注左手还是右手以及置信度
def draw_handedness(img, results):
    # 无检测到手则跳过
    if not results.multi_handedness or not results.multi_hand_landmarks:
        return
    # 遍历每只手
    for handedness, lm in zip(results.multi_handedness, results.multi_hand_landmarks):
        label = handedness.classification[0].label  # 'Left' or 'Right'
        score = handedness.classification[0].score  # 置信度
        # 取手腕点作为标注位置
        h, w = img.shape[:2] # 图像尺寸
        x = int(lm.landmark[0].x * w) # 手腕 x 坐标
        y = int(lm.landmark[0].y * h) # 手腕 y 坐标
        cv2.putText(img, f"{label} {score:.2f}", (x+10, y-10),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0,255,255), 2)

def landmarks_to_list(lmks, w, h):
    """把 21 点转成便于发往 Unity 的结构（归一化 & 像素坐标都返回）"""
    pts = [] # 存放点的列表
    for lm in lmks.landmark:
        # 通过dict存储每个点的信息
        pts.append({
            "x": lm.x, "y": lm.y, "z": lm.z,            # 归一化坐标（0..1）
            "px": int(lm.x * w), "py": int(lm.y * h),   # 像素坐标
            "vz": lm.z                                   # 相对深度（负数更靠近）
        })
    return pts

# ---------- Main loop ----------
def main():
    cap = cv2.VideoCapture(CAM_INDEX, cv2.CAP_DSHOW)  # Windows上用 DSHOW 更稳
    cap.set(cv2.CAP_PROP_FRAME_WIDTH,  W)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, H)
    cap.set(cv2.CAP_PROP_FPS, TARGET_FPS)

    hands = mp_hands.Hands(
        model_complexity=MODEL_COMPLEXITY,
        max_num_hands=MAX_HANDS,
        min_detection_confidence=DETECT_CONF,
        min_tracking_confidence=TRACK_CONF
    )

    cv2.namedWindow("MediaPipe Hands", cv2.WINDOW_NORMAL)
    cv2.resizeWindow("MediaPipe Hands", 960, 540)

    t0, frames = time.time(), 0
    print("✅ 运行中：q 退出，f 切换镜像，s 保存截图。")

    selfie = True # 是否水平镜像显示（更符合自拍视角）

    save_idx = 0
    while True:
        ok, frame = cap.read()
        if not ok:
            print("⚠️ 摄像头读取失败"); break

        if selfie:
            frame = cv2.flip(frame, 1)

        # MediaPipe 使用 RGB, OpenCV 使用 BGR
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB) # 转换颜色空间
        rgb.flags.writeable = False 
        results = hands.process(rgb)
        rgb.flags.writeable = True

        # 绘制关键点
        if results.multi_hand_landmarks:
            for hand_landmarks in results.multi_hand_landmarks:
                # 通过 MediaPipe 自带的绘图样式绘制
                mp_drawing.draw_landmarks(
                    frame,
                    hand_landmarks,
                    mp_hands.HAND_CONNECTIONS,
                    mp_styles.get_default_hand_landmarks_style(),
                    mp_styles.get_default_hand_connections_style()
                )

            # 也可以把数据结构化（为接 Unity 做准备）
            h, w = frame.shape[:2] # 图像尺寸
            all_hands = [landmarks_to_list(lm, w, h) for lm in results.multi_hand_landmarks] # 结构化手部关键点
            # TODO: 发送 all_hands 通过 WebSocket 到 Unity（后续再接）

        draw_handedness(frame, results) # 绘制左右手标注

        # 计算 FPS（更可信的方式：测 60 帧时间）
        frames += 1
        dt = time.time() - t0
        if dt >= 0.5:
            fps = frames / dt
            t0, frames = time.time(), 0
        else:
            fps = float('nan')
        if fps == fps:  # 非 NaN
            draw_fps(frame, fps)

        cv2.imshow("MediaPipe Hands", frame)
        k = cv2.waitKey(1) & 0xFF
        if k == ord('q'): break
        elif k == ord('f'):
            selfie = not selfie
            print("镜像：", "开" if selfie else "关")
        elif k == ord('s'):
            cv2.imwrite(f"hands_{save_idx}.png", frame)
            print(f"💾 已保存 hands_{save_idx}.png")
            save_idx += 1

    hands.close()
    cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    main()
