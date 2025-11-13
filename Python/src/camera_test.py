import cv2, time

# 打开摄像头
# 如果你电脑上只有这一个摄像头，一般 index=0
# 如果还有笔记本内置摄像头，就可能是 1
cap = cv2.VideoCapture(0)

# 可选：设置分辨率（C922 支持 720p / 1080p）
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
cap.set(cv2.CAP_PROP_FPS, 30)  # 尝试设置为 30 FPS

# 在图像上绘制 FPS
def draw_fps(img, fps):
    cv2.putText(img, f"FPS: {fps:.1f}", (12, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255,255,255), 2)

t0, frames = time.time(), 0

if not cap.isOpened():
    print("❌ 无法打开摄像头，请检查连接或索引号！")
else:
    print("✅ 摄像头已启动，按 Q 键退出窗口。")

while True:
    ret, frame = cap.read()
    if not ret:
        print("⚠️ 读取失败，可能摄像头被占用。")
        break

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

    # 显示画面
    cv2.imshow("Logitech C922 Pro - Press Q to Quit", frame)

    # 按 Q 退出
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# 清理
cap.release()
cv2.destroyAllWindows()
