sudo apt-get update
sudo apt-get install -y openssh-server
sudo systemctl enable --now ssh

sudo apt-get install -y vlc python3-vlc


IMAGE_TAG=local-test ENV_FILE=.env.deployment ./scripts/build-and-push-image.sh

BUILD_WITH_CAMERAS=true IMAGE_TAG=deployment-0.0.3 ENV_FILE=.env.deployment ./scripts/build-and-push-image.sh

IMAGE_TAG=deployment-0.0.3 ENV_FILE=.env.deployment COMPOSE_FILE_EXTRA=./docker-compose.cameras.yml ./scripts/deploy.sh

# Deployment exposes the app at http://liamluimes-himalayas.nord:5000 through
# a host-level Meshnet proxy. Keep Docker on the default context for camera access.
ENV_FILE=.env.deployment ./scripts/deploy.sh

# To stop the app and Meshnet proxy:
ENV_FILE=.env.deployment ./scripts/stop.sh

IMAGE_TAG=local-test ENV_FILE=.env.deployment ./scripts/build-and-push-image.sh

BUILD_WITH_CAMERAS=true IMAGE_TAG=deployment-0.0.3 ENV_FILE=.env.deployment ./scripts/build-and-push-image.sh

IMAGE_TAG=deployment-0.0.3 ENV_FILE=.env.deployment COMPOSE_FILE_EXTRA=./docker-compose.cameras.yml ./scripts/deploy.sh
