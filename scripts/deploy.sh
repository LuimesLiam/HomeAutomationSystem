#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-${REPO_ROOT}/docker-compose.yml}"
. "${SCRIPT_DIR}/lib/env.sh"
COMPOSE_FILE_EXTRA="${COMPOSE_FILE_EXTRA:-$(read_env COMPOSE_FILE_EXTRA)}"

if [ ! -f "${ENV_FILE}" ]; then
  echo "Missing env file: ${ENV_FILE}" >&2
  echo "Create one from .env.example and set APP_IMAGE_REPOSITORY, IMAGE_TAG, host paths, and secrets." >&2
  exit 1
fi

postgres_connection="${POSTGRES_CONNECTION:-$(read_env POSTGRES_CONNECTION)}"
if [[ "${postgres_connection}" =~ (^|[[:space:];])Host=(127\.0\.0\.1|localhost)([[:space:];]|$) ]]; then
  echo "POSTGRES_CONNECTION points at localhost, but deploy.sh runs the app in Docker Compose." >&2
  echo "Use the compose service name instead: Host=postgres;Port=5432;..." >&2
  exit 1
fi

recorded_video_path="${RECORDED_VIDEO_HOST_PATH:-$(read_env RECORDED_VIDEO_HOST_PATH)}"
media_path="${MEDIA_SOURCE_PATH:-$(read_env MEDIA_SOURCE_PATH)}"
document_path="${DOCUMENT_SOURCE_PATH:-$(read_env DOCUMENT_SOURCE_PATH)}"
additional_media_mounts="${ADDITIONAL_MEDIA_MOUNTS:-$(read_env ADDITIONAL_MEDIA_MOUNTS)}"
export APP_IMAGE_REF="${APP_IMAGE_REF:-$(resolve_image_ref)}"
export APP_ENV_FILE="${APP_ENV_FILE:-${ENV_FILE}}"

mkdir -p "${recorded_video_path:-${REPO_ROOT}/recorded_videos}"

if [ -n "${media_path}" ] && [ ! -d "${media_path}" ]; then
  echo "Warning: MEDIA_SOURCE_PATH does not exist on this host: ${media_path}" >&2
fi

if [ -n "${document_path}" ] && [ ! -d "${document_path}" ]; then
  echo "Warning: DOCUMENT_SOURCE_PATH does not exist on this host: ${document_path}" >&2
fi

compose_args=(--env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
if [ -n "${COMPOSE_FILE_EXTRA}" ]; then
  IFS=',' read -ra extra_files <<< "${COMPOSE_FILE_EXTRA}"
  for extra_file in "${extra_files[@]}"; do
    compose_args+=(-f "${extra_file}")
  done
fi

media_mount_override=""
cleanup_media_mount_override() {
  if [ -n "${media_mount_override}" ] && [ -f "${media_mount_override}" ]; then
    rm -f "${media_mount_override}"
  fi
}
trap cleanup_media_mount_override EXIT

if [ -n "${additional_media_mounts}" ]; then
  if ! command -v python3 >/dev/null 2>&1; then
    echo "python3 is required to configure ADDITIONAL_MEDIA_MOUNTS." >&2
    exit 1
  fi

  media_mount_override="$(mktemp)"
  python3 "${REPO_ROOT}/scripts/media-mount-override.py" \
    --mappings "${additional_media_mounts}" > "${media_mount_override}"
  compose_args+=(-f "${media_mount_override}")
fi

app_port="${APP_PORT:-$(read_env APP_PORT)}"
app_port="${app_port:-5000}"
meshnet_proxy_enabled="${MESHNET_PROXY_ENABLED:-$(read_env MESHNET_PROXY_ENABLED)}"
meshnet_proxy_enabled="${meshnet_proxy_enabled:-false}"
meshnet_proxy_ip="${MESHNET_PROXY_IP:-$(read_env MESHNET_PROXY_IP)}"
meshnet_proxy_port="${MESHNET_PROXY_PORT:-$(read_env MESHNET_PROXY_PORT)}"
meshnet_proxy_port="${meshnet_proxy_port:-${app_port}}"
meshnet_proxy_service="${MESHNET_PROXY_SERVICE:-$(read_env MESHNET_PROXY_SERVICE)}"
meshnet_proxy_service="${meshnet_proxy_service:-homeapp-meshnet-proxy}"
vlc_host_service_enabled="${VLC_HOST_SERVICE_ENABLED:-$(read_env VLC_HOST_SERVICE_ENABLED)}"
vlc_host_service_enabled="${vlc_host_service_enabled:-true}"
app_bind_ip="${APP_BIND_IP:-$(read_env APP_BIND_IP)}"
app_bind_ip="${app_bind_ip:-0.0.0.0}"

install_meshnet_proxy() {
  local service_name="$1"
  local listen_ip="$2"
  local listen_port="$3"
  local target_port="$4"
  local proxy_script="${REPO_ROOT}/scripts/meshnet_proxy.py"
  local unit_path="/etc/systemd/system/${service_name}.service"

  if [ -z "${listen_ip}" ]; then
    echo "MESHNET_PROXY_ENABLED is true, but MESHNET_PROXY_IP is not set." >&2
    exit 1
  fi

  if [ "${app_bind_ip}" != "127.0.0.1" ] && [ "${app_bind_ip}" != "localhost" ]; then
    echo "MESHNET_PROXY_ENABLED requires APP_BIND_IP=127.0.0.1 so the Meshnet proxy can own ${listen_ip}:${listen_port}." >&2
    echo "Current APP_BIND_IP is '${app_bind_ip}'." >&2
    exit 1
  fi

  if ! command -v python3 >/dev/null 2>&1; then
    echo "python3 is required for the Meshnet port proxy, but it is not installed." >&2
    exit 1
  fi

  if [ ! -f "${proxy_script}" ]; then
    echo "Missing Meshnet proxy script: ${proxy_script}" >&2
    exit 1
  fi

  echo "Installing Meshnet proxy ${service_name}: ${listen_ip}:${listen_port} -> 127.0.0.1:${target_port}"
  sudo tee "${unit_path}" >/dev/null <<EOF
[Unit]
Description=HomeApp Meshnet port proxy
After=network-online.target docker.service
Wants=network-online.target

[Service]
ExecStart=/usr/bin/python3 ${proxy_script} --listen-ip ${listen_ip} --listen-port ${listen_port} --target-host 127.0.0.1 --target-port ${target_port}
Restart=always
RestartSec=2

[Install]
WantedBy=multi-user.target
EOF

  sudo systemctl daemon-reload
  sudo systemctl enable --now "${service_name}"
  sudo systemctl restart "${service_name}"
}

echo "Deploying ${APP_IMAGE_REF} with ${COMPOSE_FILE}${COMPOSE_FILE_EXTRA:+ plus ${COMPOSE_FILE_EXTRA}}"
docker compose "${compose_args[@]}" pull
docker compose "${compose_args[@]}" up -d --remove-orphans

if [ "${vlc_host_service_enabled}" = "true" ] || [ "${vlc_host_service_enabled}" = "1" ] || [ "${vlc_host_service_enabled}" = "yes" ]; then
  vlc_docker_subnets=""
  app_container_id="$(docker compose "${compose_args[@]}" ps -q app)"
  if [ -n "${app_container_id}" ]; then
    app_networks="$(docker inspect \
      --format '{{range $name, $network := .NetworkSettings.Networks}}{{$name}} {{end}}' \
      "${app_container_id}")"
    for app_network in ${app_networks}; do
      app_subnet="$(docker network inspect \
        --format '{{range .IPAM.Config}}{{if .Subnet}}{{.Subnet}}{{end}}{{end}}' \
        "${app_network}")"
      if [ -n "${app_subnet}" ]; then
        vlc_docker_subnets="${vlc_docker_subnets}${vlc_docker_subnets:+,}${app_subnet}"
      fi
    done
  fi

  ENV_FILE="${ENV_FILE}" \
    VLC_DOCKER_SUBNETS="${vlc_docker_subnets}" \
    "${SCRIPT_DIR}/install-vlc-host-service.sh"
fi

if [ "${meshnet_proxy_enabled}" = "true" ] || [ "${meshnet_proxy_enabled}" = "1" ] || [ "${meshnet_proxy_enabled}" = "yes" ]; then
  install_meshnet_proxy "${meshnet_proxy_service}" "${meshnet_proxy_ip}" "${meshnet_proxy_port}" "${app_port}"
fi

echo "Deployment is running"
docker compose "${compose_args[@]}" ps

if command -v curl >/dev/null 2>&1; then
  echo "Checking app on http://localhost:${app_port}/"
  app_ready=false
  for attempt in {1..30}; do
    if curl --fail --silent --max-time 5 "http://localhost:${app_port}/" >/dev/null; then
      app_ready=true
      break
    fi

    echo "App is not ready yet (${attempt}/30); waiting..."
    sleep 2
  done

  if [ "${app_ready}" != "true" ]; then
    echo "App did not return a successful HTTP response. Current containers:" >&2
    docker compose "${compose_args[@]}" ps >&2 || true
    echo "Recent app logs:" >&2
    docker compose "${compose_args[@]}" logs --tail=200 app >&2 || true
    exit 1
  fi

  if [ "${meshnet_proxy_enabled}" = "true" ] || [ "${meshnet_proxy_enabled}" = "1" ] || [ "${meshnet_proxy_enabled}" = "yes" ]; then
    echo "Checking Meshnet proxy on http://${meshnet_proxy_ip}:${meshnet_proxy_port}/"
    if ! curl --fail --silent --max-time 5 "http://${meshnet_proxy_ip}:${meshnet_proxy_port}/" >/dev/null; then
      echo "Meshnet proxy did not return a successful HTTP response." >&2
      sudo systemctl status "${meshnet_proxy_service}" --no-pager >&2 || true
      exit 1
    fi
  fi
fi
