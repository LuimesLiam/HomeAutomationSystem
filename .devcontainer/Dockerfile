ARG VARIANT

# This is a base devcontainer image that runs Ubuntu 24.04 LTS with some basic tools for development.
FROM mcr.microsoft.com/devcontainers/base:${VARIANT:-1.2.6-noble}

# install ffmpeg (and ffprobe) plus your OpenGL deps
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y \
      ffmpeg \
      libgl1 \
      libglib2.0-0 \
 && rm -rf /var/lib/apt/lists/*
 

# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

# [Optional] Uncomment this line to install global node packages.
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1
