# HomeApp Deployment

This repo now uses tagged container images for the app and Docker Compose for the laptop deployment. That keeps the deployment simple for a local-network app while still giving you immutable, tag-based releases.

## Access boundaries

The app has no built-in user authentication. Compose binds the app to
`127.0.0.1` by default (`APP_BIND_IP`) and database/monitoring ports to
`127.0.0.1` (`ADMIN_BIND_IP`). Keep admin ports local. Use an SSH tunnel or a
private VPN for remote access; an internet-facing deployment needs a reverse
proxy that authenticates every application route and API request and terminates
TLS. CORS does not provide access control.

For a trusted private interface, set `APP_BIND_IP` to that interface's IP and
restrict access through the host firewall. Explicitly changing it to `0.0.0.0`
exposes all host interfaces. The Meshnet proxy can expose its private address
while Docker continues to bind to localhost.

## Release From GitHub Actions

The [release workflow](.github/workflows/release.yml) builds the existing Dockerfile
for `linux/amd64`, pushes a versioned image to your private registry, and then
attaches image details to the GitHub Release. For manually pushed tags without
an existing release, it creates a release with generated change notes.

In GitHub **Settings → Secrets and variables → Actions**, configure:

| Kind | Name | Example / purpose |
| --- | --- | --- |
| Variable | `DOCKER_REGISTRY` | `registry.example.com` or `docker.io`; optional port, no URL scheme or path |
| Variable | `DOCKER_IMAGE_REPOSITORY` | `registry.example.com/your-namespace/homeapp`; full repository, without a tag |
| Secret | `DOCKER_USERNAME` | Registry account with push access |
| Secret | `DOCKER_PASSWORD` | Registry access token or password |
| Optional variable | `BUILD_WITH_CAMERAS` | `true` (default) or `false`, matching the local build option |

Create the private repository in your registry first. The workflow does not
configure repository visibility. The registry must be reachable from GitHub's
hosted runner and use a trusted TLS certificate. Application runtime secrets
stay in the deployment host's `.env`; they are not needed for image builds.
The registry hostname and image reference appear in the GitHub release notes.

### Automatic versioning with Release Please

The [Release Please workflow](.github/workflows/release-please.yml) runs on `main`.
It opens and updates a release PR with changes to `version.txt`, `CHANGELOG.md`
and `.release-please-manifest.json`. The first version is `0.1.0`; `0.0.0` in
these files is the unreleased starting point. Use Conventional Commit messages
such as `feat: add a dashboard` and `fix: correct media playback` for commits
(or squash-merge titles). Review the release PR and merge it when CI passes.
Release Please then creates the version tag and GitHub Release and calls
the Docker build workflow directly. The Docker workflow preserves Release Please's notes
and attaches `docker-image.txt` with the image reference and digest. The release
exists before the Docker build finishes, so wait for the **docker** job in
**Release Please** to succeed before deploying.

Release Please uses GitHub's built-in `github-actions[bot]` identity and its
automatically supplied `GITHUB_TOKEN`; no GitHub PAT or `RELEASE_PLEASE_TOKEN`
secret is needed. Enable **Settings → Actions → General → Workflow permissions →
Allow GitHub Actions to create and approve pull requests** so the bot can open
release PRs. The workflow does not approve or merge PRs for you.

Bot-created tags do not trigger another workflow, so the Docker builder is a
reusable workflow called from the same run. When Release Please opens or updates
its PR, it explicitly dispatches **Build and test** and **Secret scan** against
the release branch so the changes receive checks before merging. This uses the
built-in token's `actions: write` permission. See
[GitHub's workflow trigger rules](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow).

### Manual version tags

You can also create a release manually from a commit whose CI checks passed:

```bash
git tag -a v0.1.0 -m "HomeApp v0.1.0"
git push origin v0.1.0
```

Subsequent releases use new tags such as `v0.1.1`. Tags such as `v0.2.0-rc.1`
produce GitHub prereleases. The Docker tag retains the `v` prefix; no `latest`
image tag is published. For manual tags, release creation runs only after a
successful image push.
The existing CI workflow runs independently; the release workflow does not wait
for its results, so tag a validated commit.

To retry a failed release, run **Actions → Release Docker image → Run workflow**
and enter the existing tag in `release_tag`. This rebuilds and pushes that tag;
an existing GitHub Release keeps its notes and receives updated image details. Use a new version tag for changed code.

Deploy the published image using the existing scripts:

```bash
docker login registry.example.com
APP_IMAGE_REPOSITORY=registry.example.com/your-namespace/homeapp \
  IMAGE_TAG=v0.1.0 ./scripts/deploy.sh
```

For Docker Hub, use `docker login docker.io` and a repository such as
`docker.io/YOUR_USERNAME/homeapp` in both GitHub settings and deployment config.

## Build and Push From the Development PC

1. Set the image repository and tag:

   ```bash
   export APP_IMAGE_REPOSITORY=docker.io/YOUR_USERNAME/homeapp
   export IMAGE_TAG=2026-07-03
   ```

2. Sign in to Docker Hub on the development PC, then build and push:

   ```bash
   docker login
   ./scripts/build-and-push-image.sh
   ```

The target is the private Docker Hub repository `YOUR_USERNAME/homeapp`. Repository visibility is managed in Docker Hub; these scripts do not change it.

You can also put `APP_IMAGE_REPOSITORY` and `IMAGE_TAG` in `.env` and run the same script. For one-off local use, a full `APP_IMAGE` or `APP_IMAGE_REF` such as `homeapp:local` still works.

To use a separate deployment environment file, create `.env.deployment` from `.env.example`, configure the deployment host paths and secrets (including `POSTGRES_CONNECTION` with `Host=postgres`), and pass `ENV_FILE=.env.deployment` to both build and deployment commands. An `APP_IMAGE_REF` override takes precedence over the repository and tag.

Build without the camera service:

```bash
BUILD_WITH_CAMERAS=false IMAGE_TAG=2026-07-03 ./scripts/build-and-push-image.sh
```

## Pull and Run on the Laptop

1. Copy `.env.example` to `.env` on the laptop and set the paths/secrets for that machine.

2. Sign in to Docker Hub on the laptop using an account with access to `YOUR_USERNAME/homeapp`, then deploy the same tag you built:

   ```bash
   docker login
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
BUILD_WITH_CAMERAS=true IMAGE_TAG=2026-10-09 ./scripts/build-and-push-image.sh
COMPOSE_FILE_EXTRA=./docker-compose.cameras.yml IMAGE_TAG=2026-10-09 ./scripts/deploy.sh
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
