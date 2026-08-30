#!/bin/bash

echo "Starting dotnet app in debug mode..."
dotnet watch run --project backend/Core/HomeApp.sln &

echo "Starting frontend app in debug mode..."
cd frontend && npm start &

echo "Both apps are running in debug mode. Press Ctrl+C to stop all."

# Wait for background processes
wait
