# HomeApp Deployment

This repo now uses tagged container images for the app and Docker Compose for the laptop deployment. That keeps the deployment simple for a local-network app while still giving you immutable, tag-based releases.

## Build and Push From the Development PC

1. Set the image repository and tag:

   ```bash
   export APP_IMAGE_REPOSITORY=ghcr.io/your-user/homeapp
   export IMAGE_TAG=2026-07-03
   ```

2. Build and push:

   ```bash
   ./scripts/build-and-push-image.sh
   ```

You can also put `APP_IMAGE_REPOSITORY` and `IMAGE_TAG` in `.env` and run the same script. For one-off local use, a full `APP_IMAGE` or `APP_IMAGE_REF` such as `homeapp:local` still works.

Build without the camera service:

```bash
BUILD_WITH_CAMERAS=false IMAGE_TAG=2026-07-03 ./scripts/build-and-push-image.sh
```

## Pull and Run on the Laptop

1. Copy `.env.example` to `.env` on the laptop and set the paths/secrets for that machine.

2. Deploy a tag:

   ```bash
   IMAGE_TAG=2026-07-03 ./scripts/deploy.sh
   ```

The deploy script pulls the configured images and recreates the compose services in the background.

### Host TV Playback

`deploy.sh` also installs and starts `homeapp-vlc.service` on the Docker host by default. This host-side microservice is required because a VLC window running inside the app container cannot directly use the host TV/display and audio session.

Set these values in the deployment `.env`:

```bash
VLC_CONTROL_URL=http://host.docker.internal:6000/control
VLC_CONTROL_TOKEN=replace-with-a-long-random-token
VLC_HOST_SERVICE_ENABLED=true
VLC_HOST_MEDIA_ROOT=/absolute/host/path/to/media
VLC_CONTAINER_MEDIA_ROOT=/mnt/movies
VLC_HOST_DISPLAY=:0
```

The first deployment installs VLC and `python3-venv` on apt-based Linux hosts when needed, creates an isolated Python environment under `/opt/homeapp-vlc`, and starts the service as the current desktop user. Set `VLC_HOST_USER` when deploying over SSH or with a different desktop account. Set `VLC_HOST_AUTO_INSTALL=false` if system packages are managed separately.

When UFW is active, deployment detects the app container's current Docker subnet and permits only that subnet to connect to the host VLC port. Set `VLC_HOST_CONFIGURE_UFW=false` to manage this firewall rule manually.

Useful host playback diagnostics:

```bash
systemctl status homeapp-vlc
journalctl -u homeapp-vlc -f
curl http://127.0.0.1:6000/health
```

`./scripts/stop.sh` stops both the Compose stack and the host VLC service.

### Additional Media Locations

The app can pass any configured media path directly to the host VLC service. For every storage root beyond the default `/mnt/movies` mount, add a container-to-host mapping:

```bash
ADDITIONAL_MEDIA_MOUNTS=/mnt/drive1=/absolute/host/path/to/drive1;/mnt/nas=/srv/nas-media
```

Each entry is:

```text
container path=host path
```

Entries are separated with semicolons. `deploy.sh` uses this one setting for both sides:

- It mounts each host directory read-only into the app container.
- It teaches the host VLC service how to translate the container path back to the original host file.

The existing `DOCUMENT_SOURCE_PATH` mount is also translated automatically. For example,
`DOCUMENT_SOURCE_PATH=/home/user/Documents/HomeAppFilms` makes a database path such as
`/mnt/document/Movies/Example.mkv` resolve directly to
`/home/user/Documents/HomeAppFilms/Movies/Example.mkv` on the VLC host.

For example, with:

```bash
ADDITIONAL_MEDIA_MOUNTS=/mnt/drive1=/media/storage-drive
```

add `/mnt/drive1/Movies` or `/mnt/drive1/TV` in the Video Library Sources screen. A file stored as `/media/storage-drive/Movies/Example.mkv` on the host is seen by the container as `/mnt/drive1/Movies/Example.mkv`, and VLC receives that database path and translates it back to the original full-resolution file.

Camera devices are optional. The base deployment does not mount `/dev/video*`, so the app can run on machines without cameras. To enable cameras on a host that has them, build the image with the camera services and deploy with the camera compose override:

```bash
BUILD_WITH_CAMERAS=true IMAGE_TAG=2026-07-03 ./scripts/build-and-push-image.sh
COMPOSE_FILE_EXTRA=./docker-compose.cameras.yml IMAGE_TAG=2026-07-03 ./scripts/deploy.sh
```

## Useful Commands

```bash
./scripts/pull-image.sh
./scripts/deploy.sh
./scripts/stop.sh
docker compose logs -f app
```

## Optional Kubernetes Note

For a 12 GB laptop, Docker Compose is likely the cleanest deployment target. If you want the fun Kubernetes route later, k3s is the lightest practical option: install k3s, push the same image tag, then translate this compose file into a small Deployment plus hostPath/PVC volumes. The app currently uses host networking and camera devices, so Compose is the lower-friction fit.
