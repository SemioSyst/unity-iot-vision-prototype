import cv2

# 打开摄像头
# 如果你电脑上只有这一个摄像头，一般 index=0
# 如果还有笔记本内置摄像头，就可能是 1
cap = cv2.VideoCapture(0)

# 可选：设置分辨率（C922 支持 720p / 1080p）
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

if not cap.isOpened():
    print("❌ 无法打开摄像头，请检查连接或索引号！")
else:
    print("✅ 摄像头已启动，按 Q 键退出窗口。")

while True:
    ret, frame = cap.read()
    if not ret:
        print("⚠️ 读取失败，可能摄像头被占用。")
        break

    # 显示画面
    cv2.imshow("Logitech C922 Pro - Press Q to Quit", frame)

    # 按 Q 退出
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# 清理
cap.release()
cv2.destroyAllWindows()
