import cv2
import time
cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
cap.set(cv2.CAP_PROP_FPS, 30)

t0 = time.time()
frames = 0
while frames < 120:
    ok, _ = cap.read()
    if not ok: break
    frames += 1
t1 = time.time()
print("Measured FPS:", frames / (t1 - t0))
cap.release()
