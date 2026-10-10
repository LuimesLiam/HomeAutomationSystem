# Development container

Open the repository in VS Code and run **Dev Containers: Rebuild and Reopen in Container**. Before Compose starts, `initialize-env.sh` generates database, pgAdmin and Grafana passwords in `.devcontainer/.env`. Existing values are preserved on subsequent starts. The file is Git-ignored, excluded from the development image build context, and readable only by its owner. Do not publish it.

For manual startup, run `bash .devcontainer/initialize-env.sh` from the repository root before running Docker Compose. Optional production API keys belong in your own local configuration; none are copied from the private repository.

The app receives its development PostgreSQL connection through Compose. Dependency installation runs after container creation without writing empty optional API keys to .NET user secrets.

VS Code's shell environment probe is disabled because it can terminate the container connection with `Error reading shell environment` / `stream ended with:0 but wanted:9`. The development image already supplies the Node, .NET and Python paths. After changing this setting, run **Dev Containers: Rebuild and Reopen in Container** to apply it.

Service container names are assigned by Compose for this project so the public copy can coexist with the private HomeApp devcontainer.

Private commute seed SQL belongs in `.devcontainer/private-migrations/commute/`. This directory is Git-ignored and excluded from both Docker build contexts. The app discovers it and applies numbered migrations once to PostgreSQL on startup; no coordinates are embedded in tracked code. Back it up separately. Add a new numbered script for changes instead of editing an applied migration.
