#!/bin/bash
set -e

# Load environment variables from .env
if [ -f .env ]; then
    export $(grep -v '^#' .env | xargs)
fi

echo -e "Setting HomeApp.Host application secrets...\n"
dotnet user-secrets set "Discord:WebhookUrl" "$DISCORD_WEBHOOK_URL" --project backend/Core/HomeApp.Host/ 
dotnet user-secrets set "MOVIE_PATH_ROOT" "$MOVIE_PATH_ROOT" --project backend/Core/HomeApp.Host/
dotnet user-secrets set "VlcControlUrl" "$VLC_CONTROL_URL" --project backend/Core/HomeApp.Host/
dotnet user-secrets set "VlcPath" "$VLC_PATH" --project backend/Core/HomeApp.Host/
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$CONNECTION_STRING__DEFAULT" --project backend/Core/HomeApp.Host/
dotnet user-secrets set "OMDB_API_KEY" "$OMDB_API_KEY" --project backend/Core/HomeApp.Host/

echo -e "All secrets set for HomeApp.Host."
