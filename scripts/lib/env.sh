#!/usr/bin/env bash

read_env() {
  local key="$1"
  [ -f "${ENV_FILE}" ] || return 0
  awk -v key="${key}" '
    $0 ~ /^[[:space:]]*#/ || $0 !~ /=/ { next }
    {
      split($0, parts, "=")
      if (parts[1] == key) {
        sub(/^[^=]*=/, "")
        gsub(/^["'\''"]|["'\''"]$/, "")
        print
        exit
      }
    }
  ' "${ENV_FILE}"
}

image_has_tag() {
  local image="$1"
  local last_part="${image##*/}"
  [[ "${last_part}" == *:* ]]
}

resolve_image_ref() {
  local image_ref image_repo legacy_image raw_tag tag

  image_ref="${APP_IMAGE_REF:-$(read_env APP_IMAGE_REF)}"
  if [ -n "${image_ref}" ]; then
    printf '%s\n' "${image_ref}"
    return 0
  fi

  image_repo="${APP_IMAGE_REPOSITORY:-$(read_env APP_IMAGE_REPOSITORY)}"
  legacy_image="${APP_IMAGE:-$(read_env APP_IMAGE)}"
  raw_tag="${IMAGE_TAG:-$(read_env IMAGE_TAG)}"

  if [ -n "${image_repo}" ]; then
    tag="${raw_tag:-latest}"
    printf '%s:%s\n' "${image_repo}" "${tag}"
    return 0
  fi

  if [ -n "${legacy_image}" ] && image_has_tag "${legacy_image}" && [ -z "${raw_tag}" ]; then
    printf '%s\n' "${legacy_image}"
    return 0
  fi

  image_repo="${legacy_image:-ghcr.io/your-user/homeapp}"
  # IMAGE_TAG replaces a legacy tag; registry ports are part of the repository.
  if image_has_tag "${image_repo}"; then
    image_repo="${image_repo%:*}"
  fi
  tag="${raw_tag:-latest}"
  printf '%s:%s\n' "${image_repo}" "${tag}"
}
