import zmq
import base64
import cv2
import numpy as np

context = zmq.Context()
subscriber = context.socket(zmq.SUB)
subscriber.connect("tcp://localhost:5555")
subscriber.setsockopt_string(zmq.SUBSCRIBE, "")

while True:
    msg = subscriber.recv()
    try:
        # Try to decode as utf-8 string
        msg_str = msg.decode('utf-8')
        if msg_str == "motion_detected":
            print("Motion detected event received.")
            continue
    except UnicodeDecodeError:
        # Not a string, treat as image
        pass

    # Decode base64 image and display
    jpg_bytes = base64.b64decode(msg)
    np_arr = np.frombuffer(jpg_bytes, dtype=np.uint8)
    frame = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
    if frame is not None:
        cv2.imshow("Video Feed", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cv2.destroyAllWindows()
