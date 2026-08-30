#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-${REPO_ROOT}/docker-compose.yml}"
. "${SCRIPT_DIR}/lib/env.sh"
COMPOSE_FILE_EXTRA="${COMPOSE_FILE_EXTRA:-$(read_env COMPOSE_FILE_EXTRA)}"
export APP_ENV_FILE="${APP_ENV_FILE:-${ENV_FILE}}"
meshnet_proxy_enabled="${MESHNET_PROXY_ENABLED:-$(read_env MESHNET_PROXY_ENABLED)}"
meshnet_proxy_enabled="${meshnet_proxy_enabled:-false}"
meshnet_proxy_service="${MESHNET_PROXY_SERVICE:-$(read_env MESHNET_PROXY_SERVICE)}"
meshnet_proxy_service="${meshnet_proxy_service:-homeapp-meshnet-proxy}"
vlc_host_service_enabled="${VLC_HOST_SERVICE_ENABLED:-$(read_env VLC_HOST_SERVICE_ENABLED)}"
vlc_host_service_enabled="${vlc_host_service_enabled:-true}"
vlc_host_service_name="${VLC_HOST_SERVICE_NAME:-$(read_env VLC_HOST_SERVICE_NAME)}"
vlc_host_service_name="${vlc_host_service_name:-homeapp-vlc}"

compose_args=(--env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
if [ -n "${COMPOSE_FILE_EXTRA}" ]; then
  IFS=',' read -ra extra_files <<< "${COMPOSE_FILE_EXTRA}"
  for extra_file in "${extra_files[@]}"; do
    compose_args+=(-f "${extra_file}")
  done
fi

docker compose "${compose_args[@]}" down

if [ "${meshnet_proxy_enabled}" = "true" ] || [ "${meshnet_proxy_enabled}" = "1" ] || [ "${meshnet_proxy_enabled}" = "yes" ]; then
  sudo systemctl disable --now "${meshnet_proxy_service}" || true
fi

if [ "${vlc_host_service_enabled}" = "true" ] || [ "${vlc_host_service_enabled}" = "1" ] || [ "${vlc_host_service_enabled}" = "yes" ]; then
  sudo systemctl disable --now "${vlc_host_service_name}" || true
fi
