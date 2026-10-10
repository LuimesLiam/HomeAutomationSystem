# Deployment quick reference

For local development, follow [README.md](README.md). For container deployment,
follow [DEPLOYMENT.md](DEPLOYMENT.md), including its access restrictions.

1. Copy `.env.example` to `.env.deployment`.
2. Set real paths and generated passwords/tokens. Set `POSTGRES_CONNECTION` to
   use `Host=postgres` and the same credentials as the PostgreSQL service.
3. Set `APP_IMAGE_REPOSITORY` to a repository you can push to. Remove any
   `APP_IMAGE_REF` override when using repository/tag settings.
4. Sign in to the registry on both the build and deployment machines.

```bash
docker login
BUILD_WITH_CAMERAS=false IMAGE_TAG=local-test ENV_FILE=.env.deployment \
  ./scripts/build-and-push-image.sh
IMAGE_TAG=local-test ENV_FILE=.env.deployment ./scripts/deploy.sh
ENV_FILE=.env.deployment ./scripts/stop.sh
```

Camera builds require Python dependencies and suitable host camera devices.
Enable `BUILD_WITH_CAMERAS=true` and set
`COMPOSE_FILE_EXTRA=./docker-compose.cameras.yml` when deploying them.

App and administrative ports bind to localhost by default. Configure private
remote access explicitly; do not expose the unauthenticated app to the internet.
