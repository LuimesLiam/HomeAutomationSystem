import asyncio
import os
import cv2
import threading
import psutil
import time 
import zmq
import base64
import json
import numpy as np
from dotenv import load_dotenv

def get_temperatures():
    temps = psutil.sensors_temperatures()
    for name, entries in temps.items():
        for entry in entries:
            return f"{name} {entry.label or 'unknown'}: {entry.current}°C"

load_dotenv()  # <-- add this

# Keep the recorder aligned with the .NET listener's configured output folder.
output_folder = os.getenv(
    "RECORDED_VIDEO_OUTPUT_FOLDER",
    os.getenv("OUTPUT_FOLDER", "/workspaces/HomeApp/recorded_videos")
)
camera_device_value = os.getenv("CAMERA_DEVICE", "/dev/video0")

def parse_camera_device(value):
    return int(value) if value.isdigit() else value

camera_device = parse_camera_device(camera_device_value)

context = zmq.Context()
publisher = context.socket(zmq.PUB)
publisher.bind("tcp://*:5555")

# Setup camera
cap = None  # Start with no camera open
camera_lock = threading.Lock()
backSub = cv2.createBackgroundSubtractorMOG2()
status = {
    "enabled": True,
    "cameraDevice": camera_device_value,
    "cameraOpened": False,
    "frameCount": 0,
    "lastFrameAt": None,
    "lastError": None
}

motion_detected=False
motion_duration_thresh = 3

motion_start_time = None
is_recording = False
recording_start_time = None
record_duration = 10  # seconds
max_duration = 25
motion_duration = 1.5

# Sensitivity control: Increase to ignore small movements/shadows
min_contour_area = 700  # Default: 1000. Try 5000 or higher for less sensitivity.
min_contour_solidity = 0.4  # 0.0-1.0, higher = more solid (person), lower = more likely shadow

# Ensure the output folder exists in the current directory
if not os.path.exists(output_folder):
    os.makedirs(output_folder)

def open_camera():
    capture = cv2.VideoCapture(camera_device)
    opened = capture.isOpened()
    status["cameraOpened"] = opened
    if opened:
        status["lastError"] = None
        print(f"Camera opened: {camera_device_value}", flush=True)
    else:
        status["lastError"] = f"Could not open camera device {camera_device_value}"
        print(status["lastError"], flush=True)
    return capture

def close_camera():
    global cap
    with camera_lock:
        if cap is not None:
            cap.release()
            cap = None
        status["cameraOpened"] = False

def read_camera_frame():
    global cap
    with camera_lock:
        if cap is None or not cap.isOpened():
            cap = open_camera()
        if cap is None or not cap.isOpened():
            return False, None
        return cap.read()

def control_thread(control_flag):
    control_context = zmq.Context()
    control_socket = control_context.socket(zmq.REP)
    control_socket.bind("tcp://*:5556")
    global cap
    while True:
        cmd = control_socket.recv_string()
        if cmd == "enable":
            control_flag["enabled"] = True
            status["enabled"] = True
            # (Re)open the camera if not already open
            with camera_lock:
                if cap is None or not cap.isOpened():
                    cap = open_camera()
            control_socket.send_string("ok")
        elif cmd == "disable":
            control_flag["enabled"] = False
            status["enabled"] = False
            close_camera()
            control_socket.send_string("ok")
        elif cmd == "status":
            with camera_lock:
                status["cameraOpened"] = bool(cap is not None and cap.isOpened())
            control_socket.send_string(json.dumps(status))
        else:
            control_socket.send_string("unknown command")

control_flag = {"enabled": True}
threading.Thread(target=control_thread, args=(control_flag,), daemon=True).start()

while True:
    if not control_flag["enabled"]:
        close_camera()
        time.sleep(0.5)
        continue

    ret, frame = read_camera_frame()
    if not ret:
        # Instead of breaking, just wait and try again
        status["lastError"] = f"Camera device {camera_device_value} opened but did not return a frame"
        with camera_lock:
            status["cameraOpened"] = bool(cap is not None and cap.isOpened())
        time.sleep(0.1)
        continue
    status["cameraOpened"] = True
    status["frameCount"] += 1
    status["lastFrameAt"] = time.time()
    status["lastError"] = None
    fg_mask = backSub.apply(frame)
    contours, hierarchy = cv2.findContours(fg_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    # Filter by area first
    area_filtered = [cnt for cnt in contours if cv2.contourArea(cnt) > min_contour_area]
    # Further filter by solidity
    large_contours = []
    for cnt in area_filtered:
        area = cv2.contourArea(cnt)
        hull = cv2.convexHull(cnt)
        hull_area = cv2.contourArea(hull)
        if hull_area > 0:
            solidity = float(area) / hull_area
            if solidity > min_contour_solidity:
                large_contours.append(cnt)
    frame_out = frame.copy()
    # Draw bounding boxes for large contours
    for cnt in large_contours:
        x, y, w, h = cv2.boundingRect(cnt)
        cv2.rectangle(frame_out, (x, y), (x+w, y+h), (0, 0, 200), 3)
    
    current_time = time.time()

    if large_contours:
        if not motion_detected:
            motion_detected = True
            motion_start_time = current_time
        else:
            motion_duration = current_time - motion_start_time
            if (motion_duration > motion_duration_thresh and not is_recording) :
                is_recording = True
                recording_start_time = current_time
                video_path = os.path.join(output_folder, f"{int(current_time)}.avi")
                frame_size = (frame.shape[1], frame.shape[0])
                out = cv2.VideoWriter(video_path, cv2.VideoWriter_fourcc(*'XVID'), 20.0, frame_size)
                print(f"Recording started due to motion detection. Saving to {video_path}")
    else:
        motion_detected = False
        motion_start_time = None

    if is_recording:
        if (current_time - recording_start_time > record_duration) and (not large_contours or (current_time - recording_start_time > max_duration)):
            is_recording = False
            out.release()
            print("SENDING VIDEO: ", video_path)
            # Instead of sending to Discord, publish a notification event
            try:
                publisher.send_string("motion_detected")
            except Exception as e:
                print("Failed to send event:", e)
            out = None
            print("Recording stopped.")
        else:
            out.write(frame)
        
    # Encode and publish the frame with bounding boxes
    _, buffer = cv2.imencode('.jpg', frame_out)
    video_string = base64.b64encode(buffer)
    publisher.send(video_string)

    # Show the frame with bounding boxes in a window
    #cv2.imshow('Motion Detection', frame_out)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# Cleanup
close_camera()
cv2.destroyAllWindows()
