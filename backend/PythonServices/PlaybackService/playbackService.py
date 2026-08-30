#!/usr/bin/env python3
"""Host-side VLC bridge for HomeApp.

This process intentionally runs on the display/audio host, outside Docker.
The container API sends it media paths and playback commands over HTTP.
"""

import hmac
import logging
import os
import threading
import time

from flask import Flask, jsonify, request
import vlc


logging.basicConfig(level=os.getenv("VLC_LOG_LEVEL", "INFO"))
logger = logging.getLogger("homeapp-vlc")

CONTAINER_MEDIA_ROOT = os.path.abspath(os.getenv("VLC_CONTAINER_MEDIA_ROOT", "/mnt/movies"))
HOST_MEDIA_ROOT = os.path.abspath(os.getenv("VLC_HOST_MEDIA_ROOT", CONTAINER_MEDIA_ROOT))
CONTROL_TOKEN = os.getenv("VLC_CONTROL_TOKEN", "")


def parse_path_mappings():
    mappings = [(CONTAINER_MEDIA_ROOT, HOST_MEDIA_ROOT)]
    raw_mappings = os.getenv("VLC_PATH_MAPPINGS", "")

    for raw_mapping in raw_mappings.split(";"):
        raw_mapping = raw_mapping.strip()
        if not raw_mapping:
            continue
        if "=" not in raw_mapping:
            raise ValueError(
                f"Invalid VLC path mapping '{raw_mapping}'. Expected /container/path=/host/path"
            )

        container_root, host_root = raw_mapping.split("=", 1)
        mappings.append((
            os.path.abspath(container_root.strip()),
            os.path.abspath(host_root.strip()),
        ))

    # The most specific container root wins when mappings overlap.
    return sorted(
        dict(mappings).items(),
        key=lambda mapping: len(mapping[0]),
        reverse=True,
    )


PATH_MAPPINGS = parse_path_mappings()


def translate_media_path(path):
    if not path:
        raise ValueError("Path is required")

    container_path = os.path.abspath(path)
    candidate = container_path

    for container_root, host_root in PATH_MAPPINGS:
        try:
            relative = os.path.relpath(container_path, container_root)
        except ValueError:
            continue

        if relative == ".." or relative.startswith(f"..{os.sep}"):
            continue

        candidate = os.path.abspath(os.path.join(host_root, relative))
        break

    if not os.path.isfile(candidate):
        raise FileNotFoundError(f"Media file does not exist on the TV host: {candidate}")
    return candidate


class PlayerManager:
    def __init__(self):
        vlc_arguments = ["--fullscreen", "--no-video-title-show"]
        extra_arguments = os.getenv("VLC_EXTRA_ARGS", "").strip()
        if extra_arguments:
            vlc_arguments.extend(extra_arguments.split())

        self.instance = vlc.Instance(*vlc_arguments)
        self.player = self.instance.media_player_new()
        self.lock = threading.RLock()
        self.current_path = None

    def play(self, path):
        resolved_path = translate_media_path(path)
        with self.lock:
            self.player.stop()
            media = self.instance.media_new_path(resolved_path)
            self.player.set_media(media)
            self.player.set_fullscreen(True)
            result = self.player.play()
            if result == -1:
                raise RuntimeError("VLC rejected the media file")
            self.current_path = resolved_path
            time.sleep(0.15)
            self.player.audio_set_volume(80)
            return self.status()

    def pause(self):
        with self.lock:
            self.player.set_pause(1)
            return self.status()

    def resume(self):
        with self.lock:
            self.player.set_pause(0)
            return self.status()

    def stop(self):
        with self.lock:
            self.player.stop()
            self.current_path = None
            return self.status()

    def seek_relative(self, seconds):
        with self.lock:
            current = max(self.player.get_time(), 0)
            total = max(self.player.get_length(), 0)
            target = max(current + int(seconds * 1000), 0)
            if total:
                target = min(target, total)
            self.player.set_time(target)
            return self.status()

    def set_time(self, milliseconds):
        with self.lock:
            total = max(self.player.get_length(), 0)
            target = max(int(milliseconds), 0)
            if total:
                target = min(target, total)
            self.player.set_time(target)
            return self.status()

    def set_volume(self, volume):
        with self.lock:
            normalized = max(0, min(int(volume), 100))
            self.player.audio_set_volume(normalized)
            return self.status()

    def status(self):
        with self.lock:
            state = self.player.get_state()
            return {
                "current_time": max(self.player.get_time(), 0),
                "total_time": max(self.player.get_length(), 0),
                "playing": state == vlc.State.Playing,
                "paused": state == vlc.State.Paused,
                "state": str(state),
                "path": self.current_path,
            }


app = Flask(__name__)
manager = PlayerManager()


@app.before_request
def authorize():
    if request.path == "/health" or not CONTROL_TOKEN:
        return None
    supplied = request.headers.get("X-HomeApp-Token", "")
    if not hmac.compare_digest(supplied, CONTROL_TOKEN):
        return jsonify(error="Unauthorized"), 401
    return None


@app.get("/health")
def health():
    return jsonify(status="ok", player=manager.status())


@app.post("/control")
def control():
    data = request.get_json(silent=True) or {}
    command = str(data.get("command", "")).lower()

    try:
        if command == "play":
            return jsonify(manager.play(data.get("path")))
        if command == "pause":
            return jsonify(manager.pause())
        if command == "resume":
            return jsonify(manager.resume())
        if command == "stop":
            return jsonify(manager.stop())
        if command == "seek":
            return jsonify(manager.seek_relative(data.get("seconds", 0)))
        if command == "set_time":
            return jsonify(manager.set_time(data.get("seconds", 0)))
        if command == "get_time":
            return jsonify(manager.status())
        if command == "volume":
            return jsonify(manager.set_volume(data.get("seconds", 80)))
        return jsonify(error=f"Unknown command: {command}"), 400
    except (TypeError, ValueError) as error:
        return jsonify(error=str(error)), 400
    except FileNotFoundError as error:
        return jsonify(error=str(error)), 404
    except Exception as error:
        logger.exception("VLC command failed")
        return jsonify(error=str(error)), 500


if __name__ == "__main__":
    app.run(
        host=os.getenv("VLC_HOST_BIND", "0.0.0.0"),
        port=int(os.getenv("VLC_HOST_PORT", "6000")),
        threaded=True,
    )
