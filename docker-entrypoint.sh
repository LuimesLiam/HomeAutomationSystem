#!/bin/sh
set -eu

if [ -f /etc/supervisor/conf.d/programs/camera.conf ]; then
  echo "Starting HomeApp with Supervisor and camera services"
  exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
fi

echo "Starting HomeApp ASP.NET host"
exec dotnet /app/HomeApp.Host.dll
