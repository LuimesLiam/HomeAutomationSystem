#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"
. "${SCRIPT_DIR}/lib/env.sh"

FULL_IMAGE="$(resolve_image_ref)"

echo "Pulling ${FULL_IMAGE}"
docker pull "${FULL_IMAGE}"

echo "Pulled ${FULL_IMAGE}"
