#!/usr/bin/env python3
"""Build a Compose override for extra HomeApp media mounts."""

import argparse
import json
import os
import sys


def parse_mappings(raw_mappings):
    volumes = []
    targets = set()

    for raw_mapping in raw_mappings.split(";"):
        raw_mapping = raw_mapping.strip()
        if not raw_mapping:
            continue
        if "=" not in raw_mapping:
            raise ValueError(
                f"Invalid media mapping '{raw_mapping}'. "
                "Expected /container/path=/host/path"
            )

        container_path, host_path = (
            value.strip() for value in raw_mapping.split("=", 1)
        )
        if not os.path.isabs(container_path) or not os.path.isabs(host_path):
            raise ValueError(
                f"Media mapping paths must be absolute: '{raw_mapping}'"
            )
        if container_path in targets:
            raise ValueError(f"Duplicate container media path: {container_path}")
        if not os.path.isdir(host_path):
            raise ValueError(f"Host media path does not exist: {host_path}")

        targets.add(container_path)
        volumes.append({
            "type": "bind",
            "source": host_path,
            "target": container_path,
            "read_only": True,
        })

    return volumes


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mappings", required=True)
    args = parser.parse_args()

    try:
        volumes = parse_mappings(args.mappings)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1

    json.dump({"services": {"app": {"volumes": volumes}}}, sys.stdout)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
