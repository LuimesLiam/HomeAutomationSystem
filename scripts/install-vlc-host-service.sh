#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"
. "${SCRIPT_DIR}/lib/env.sh"

if [ ! -f "${ENV_FILE}" ]; then
  echo "Missing env file: ${ENV_FILE}" >&2
  exit 1
fi

if [ "$(id -u)" -eq 0 ]; then
  root_command=()
  configured_service_user="${VLC_HOST_USER:-$(read_env VLC_HOST_USER)}"
  service_user="${configured_service_user:-${SUDO_USER:-root}}"
else
  root_command=(sudo)
  configured_service_user="${VLC_HOST_USER:-$(read_env VLC_HOST_USER)}"
  service_user="${configured_service_user:-$(id -un)}"
fi

service_name="${VLC_HOST_SERVICE_NAME:-$(read_env VLC_HOST_SERVICE_NAME)}"
service_name="${service_name:-homeapp-vlc}"
host_media_root="${VLC_HOST_MEDIA_ROOT:-$(read_env VLC_HOST_MEDIA_ROOT)}"
host_media_root="${host_media_root:-${MEDIA_SOURCE_PATH:-$(read_env MEDIA_SOURCE_PATH)}}"
container_media_root="${VLC_CONTAINER_MEDIA_ROOT:-$(read_env VLC_CONTAINER_MEDIA_ROOT)}"
container_media_root="${container_media_root:-/mnt/movies}"
host_bind="${VLC_HOST_BIND:-$(read_env VLC_HOST_BIND)}"
host_bind="${host_bind:-0.0.0.0}"
host_port="${VLC_HOST_PORT:-$(read_env VLC_HOST_PORT)}"
host_port="${host_port:-6000}"
configure_ufw="${VLC_HOST_CONFIGURE_UFW:-$(read_env VLC_HOST_CONFIGURE_UFW)}"
configure_ufw="${configure_ufw:-true}"
docker_subnets="${VLC_DOCKER_SUBNETS:-$(read_env VLC_DOCKER_SUBNETS)}"
control_token="${VLC_CONTROL_TOKEN:-$(read_env VLC_CONTROL_TOKEN)}"
additional_media_mounts="${ADDITIONAL_MEDIA_MOUNTS:-$(read_env ADDITIONAL_MEDIA_MOUNTS)}"
document_source_path="${DOCUMENT_SOURCE_PATH:-$(read_env DOCUMENT_SOURCE_PATH)}"
auto_install="${VLC_HOST_AUTO_INSTALL:-$(read_env VLC_HOST_AUTO_INSTALL)}"
auto_install="${auto_install:-true}"
display_value="${VLC_HOST_DISPLAY:-$(read_env VLC_HOST_DISPLAY)}"
display_value="${display_value:-${DISPLAY:-:0}}"
wayland_display_value="${VLC_HOST_WAYLAND_DISPLAY:-$(read_env VLC_HOST_WAYLAND_DISPLAY)}"
wayland_display_value="${wayland_display_value:-${WAYLAND_DISPLAY:-}}"
pulse_server_value="${VLC_HOST_PULSE_SERVER:-$(read_env VLC_HOST_PULSE_SERVER)}"
pulse_server_value="${pulse_server_value:-${PULSE_SERVER:-}}"

if [ "${service_user}" = "root" ]; then
  echo "VLC must run as the desktop user, not root. Set VLC_HOST_USER in ${ENV_FILE}." >&2
  exit 1
fi

if [ -z "${host_media_root}" ] || [ ! -d "${host_media_root}" ]; then
  echo "VLC host media path does not exist: ${host_media_root:-<empty>}" >&2
  echo "Set VLC_HOST_MEDIA_ROOT or MEDIA_SOURCE_PATH to the host's media directory." >&2
  exit 1
fi

missing_dependencies=false
command -v vlc >/dev/null 2>&1 || missing_dependencies=true
python3 -c 'import venv' >/dev/null 2>&1 || missing_dependencies=true

if [ "${missing_dependencies}" = "true" ]; then
  if [ "${auto_install}" != "true" ] && [ "${auto_install}" != "1" ] && [ "${auto_install}" != "yes" ]; then
    echo "VLC and python3-venv are required. Install them or set VLC_HOST_AUTO_INSTALL=true." >&2
    exit 1
  fi

  if ! command -v apt-get >/dev/null 2>&1; then
    echo "Automatic VLC installation currently supports apt-based Linux hosts only." >&2
    exit 1
  fi

  "${root_command[@]}" apt-get update
  "${root_command[@]}" apt-get install -y vlc python3-venv
fi

service_user_home="$(getent passwd "${service_user}" | cut -d: -f6)"
service_user_id="$(id -u "${service_user}")"
venv_path="/opt/homeapp-vlc"
service_env_path="/etc/${service_name}.env"
unit_path="/etc/systemd/system/${service_name}.service"
playback_script="${REPO_ROOT}/backend/PythonServices/PlaybackService/playbackService.py"
requirements_file="${REPO_ROOT}/backend/PythonServices/PlaybackService/requirements.txt"

"${root_command[@]}" python3 -m venv "${venv_path}"
"${root_command[@]}" "${venv_path}/bin/pip" install --upgrade pip
"${root_command[@]}" "${venv_path}/bin/pip" install -r "${requirements_file}"

escape_env_value() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "${value}"
}

vlc_path_mappings="${additional_media_mounts}"
if [ -n "${document_source_path}" ]; then
  if [ -n "${vlc_path_mappings}" ]; then
    vlc_path_mappings="${vlc_path_mappings};"
  fi
  vlc_path_mappings="${vlc_path_mappings}/mnt/document=${document_source_path}"
fi

{
  printf 'VLC_HOST_MEDIA_ROOT=%s\n' "$(escape_env_value "${host_media_root}")"
  printf 'VLC_CONTAINER_MEDIA_ROOT=%s\n' "$(escape_env_value "${container_media_root}")"
  printf 'VLC_HOST_BIND=%s\n' "$(escape_env_value "${host_bind}")"
  printf 'VLC_HOST_PORT=%s\n' "$(escape_env_value "${host_port}")"
  printf 'VLC_CONTROL_TOKEN=%s\n' "$(escape_env_value "${control_token}")"
  printf 'VLC_PATH_MAPPINGS=%s\n' "$(escape_env_value "${vlc_path_mappings}")"
  printf 'DISPLAY=%s\n' "$(escape_env_value "${display_value}")"
  if [ -n "${wayland_display_value}" ]; then
    printf 'WAYLAND_DISPLAY=%s\n' "$(escape_env_value "${wayland_display_value}")"
  fi
  if [ -n "${pulse_server_value}" ]; then
    printf 'PULSE_SERVER=%s\n' "$(escape_env_value "${pulse_server_value}")"
  fi
  printf 'XAUTHORITY=%s\n' "$(escape_env_value "${XAUTHORITY:-${service_user_home}/.Xauthority}")"
  printf 'XDG_RUNTIME_DIR=%s\n' "$(escape_env_value "/run/user/${service_user_id}")"
  printf 'PYTHONUNBUFFERED=1\n'
} | "${root_command[@]}" tee "${service_env_path}" >/dev/null
"${root_command[@]}" chmod 600 "${service_env_path}"

{
  printf '[Unit]\n'
  printf 'Description=HomeApp host VLC playback bridge\n'
  printf 'After=network-online.target graphical.target\n'
  printf 'Wants=network-online.target\n\n'
  printf '[Service]\n'
  printf 'Type=simple\n'
  printf 'User=%s\n' "${service_user}"
  printf 'EnvironmentFile=%s\n' "${service_env_path}"
  printf 'WorkingDirectory=%s\n' "$(dirname "${playback_script}")"
  printf 'ExecStart=%s/bin/python %s\n' "${venv_path}" "${playback_script}"
  printf 'Restart=on-failure\n'
  printf 'RestartSec=2\n\n'
  printf '[Install]\n'
  printf 'WantedBy=graphical.target\n'
} | "${root_command[@]}" tee "${unit_path}" >/dev/null

"${root_command[@]}" systemctl daemon-reload
"${root_command[@]}" systemctl enable --now "${service_name}"
"${root_command[@]}" systemctl restart "${service_name}"

if command -v ufw >/dev/null 2>&1 &&
   { [ "${configure_ufw}" = "true" ] || [ "${configure_ufw}" = "1" ] || [ "${configure_ufw}" = "yes" ]; } &&
   "${root_command[@]}" ufw status | grep -q '^Status: active'; then
  IFS=',' read -ra subnet_list <<< "${docker_subnets}"
  for docker_subnet in "${subnet_list[@]}"; do
    if [ -n "${docker_subnet}" ]; then
      echo "Allowing Docker subnet ${docker_subnet} to reach VLC host port ${host_port}"
      "${root_command[@]}" ufw allow \
        from "${docker_subnet}" \
        to any port "${host_port}" \
        proto tcp \
        comment "HomeApp VLC bridge"
    fi
  done
fi

for attempt in {1..20}; do
  if "${venv_path}/bin/python" -c \
    "import urllib.request; urllib.request.urlopen('http://127.0.0.1:${host_port}/health', timeout=2).read()" \
    >/dev/null 2>&1; then
    echo "VLC host service is ready on port ${host_port}"
    exit 0
  fi
  sleep 1
done

echo "VLC host service failed its health check." >&2
"${root_command[@]}" systemctl status "${service_name}" --no-pager >&2 || true
exit 1
