#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"
. "${SCRIPT_DIR}/lib/env.sh"

DOCKERFILE="${DOCKERFILE:-${REPO_ROOT}/Dockerfile}"
BUILD_CONTEXT="${BUILD_CONTEXT:-${REPO_ROOT}}"
BUILD_WITH_CAMERAS="${BUILD_WITH_CAMERAS:-$(read_env BUILD_WITH_CAMERAS)}"
BUILD_WITH_CAMERAS="${BUILD_WITH_CAMERAS:-true}"
FULL_IMAGE="$(resolve_image_ref)"

echo "Building ${FULL_IMAGE} (camera services: ${BUILD_WITH_CAMERAS})"
docker build \
  --build-arg "INCLUDE_CAMERA_SERVICES=${BUILD_WITH_CAMERAS}" \
  --file "${DOCKERFILE}" \
  --tag "${FULL_IMAGE}" \
  "${BUILD_CONTEXT}"

echo "Built ${FULL_IMAGE}"
