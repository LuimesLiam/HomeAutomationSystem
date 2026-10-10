# ----------------------------
# Stage 1: Build Angular frontend
# ----------------------------
FROM node:24-bookworm-slim AS frontend-build
WORKDIR /app

# Install frontend dependencies
COPY frontend/package*.json ./

# Release builds use the reviewed lockfile and fail if it is stale.
RUN npm ci

# Copy the rest of the Angular app
COPY frontend/ .

# Build the production browser bundle used by ASP.NET static file hosting
RUN npm run build -- --configuration production --output-path dist/web-app
RUN if [ -f dist/web-app/browser/index.csr.html ]; then mv dist/web-app/browser/index.csr.html dist/web-app/browser/index.html; fi

# ----------------------------
# Stage 2: Build .NET backend
# ----------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /src

# Copy & publish your ASP.NET Core app
COPY backend/ .
# The runtime starts the DLL with dotnet; omit an Alpine-specific native launcher.
RUN dotnet publish Core/HomeApp.Host/HomeApp.Host.csproj \
    -c Release \
    -f net10.0 \
    -p:UseAppHost=false \
    -o /app/publish

# ----------------------------
# Stage 3: Final runtime image
# ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ARG INCLUDE_CAMERA_SERVICES=true

# 1) Copy .NET artefacts
COPY --from=backend-build /app/publish/ ./

# 2) Copy only the **browser/** build into wwwroot
#    (this ensures index.html lives at /app/wwwroot/index.html)
RUN rm -rf wwwroot
COPY --from=frontend-build /app/dist/web-app/browser/. ./wwwroot

# 3) (Optional) copy Python services & supervisor config
COPY backend/PythonServices ./PythonServices
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY supervisord.camera.conf /tmp/supervisord.camera.conf
COPY docker-entrypoint.sh /usr/local/bin/homeapp-entrypoint

# 4) Install runtime deps and optionally camera service deps
RUN apt-get update \
 && apt-get install -y supervisor \
 && mkdir -p /etc/supervisor/conf.d/programs \
 && if [ "${INCLUDE_CAMERA_SERVICES}" = "true" ]; then \
      apt-get install -y python3-pip python3-venv libgl1 libglib2.0-0 libsm6; \
      python3 -m venv /opt/venv; \
      /opt/venv/bin/pip install --upgrade pip; \
      /opt/venv/bin/pip install -r PythonServices/requirements.txt; \
      cp /tmp/supervisord.camera.conf /etc/supervisor/conf.d/programs/camera.conf; \
    else \
      rm -rf PythonServices; \
    fi \
 && rm -f /tmp/supervisord.camera.conf \
 && chmod +x /usr/local/bin/homeapp-entrypoint \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/venv/bin:${PATH}"

# 5) Expose your listening port
EXPOSE 5000

# 6) Ensure Kestrel listens on 0.0.0.0
ENV ASPNETCORE_URLS=http://0.0.0.0:5000

# 7) Start ASP.NET directly unless camera services are included.
CMD ["/usr/local/bin/homeapp-entrypoint"]
