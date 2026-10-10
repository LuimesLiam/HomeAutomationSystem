# Public copy review

Prepared from the current working tree on 2026-10-06, including uncommitted and untracked source files. The original HomeApp source was left unchanged. No commits or pushes were made.

## Exclusions

- Private HomeApp Git metadata and commit history. The existing PublicHomeApp Git metadata and history were restored from the original destination backup at the user's request.
- Local `.env` and `.env.deployment` files and private tool/configuration directories.
- Dependencies, build output, caches, generated .NET discovery files, deployment runtime folders, recordings, databases, certificates, keys and logs.

## Sanitization

- Replaced personal device hostnames in setup instructions. The Angular development-server `allowedHosts` entry is intentionally retained at the maintainer's request.
- Replaced personal names and identifiers in seeded expense groups with generic examples.
- Removed the saved development database password and hardcoded service password defaults; service passwords must be supplied locally.
- Retained only example credentials in `.env.example`. The example OMDb value is `change-me`; replace it locally.
- Restored the original PublicHomeApp `.gitignore`, including its existing credential protection rules, and added exclusions for private runtime data and local tooling.

## Verification

- Gitleaks 8.30.1 was downloaded using the version and verified SHA-256 pinned in the existing secret-scan workflow.
- Scanned the original working directory (about 430.61 MB); credential findings in local environment files were excluded from this copy. Other scanner findings were in excluded dependencies and build output. Git history was not included in this filesystem scan or this package.
- Scanned every packaged file with the repository rules plus default Gitleaks rules, archive inspection and recursive decoding: zero findings.
- Compared every packaged file against 5 unique non-placeholder local credential values: zero matches.
- Reviewed credential assignments, URLs, personal identifiers, local paths, seed data, binary files and symlinks.
- Python, shell and applicable JSON syntax checks passed. Application builds and runtime tests were not run for this packaging task.

Secret scanning reduces risk but cannot prove that every possible sensitive value has been identified. Review this folder before publishing.

## Public repository history retained

Only the original PublicHomeApp `.git` directory was restored, byte-for-byte. No private HomeApp Git metadata or history was copied. The sanitized current working files remain in place as uncommitted changes for review. No commits or pushes were made. The clean filesystem secret scan covers the packaged working files, not the retained PublicHomeApp history.

## Development container startup repair

The devcontainer now generates local passwords in the ignored `.devcontainer/.env` before Compose starts. Those generated credentials are local runtime data, not public source. Its Docker build context excludes environment files. The app receives the development PostgreSQL connection through Compose. Removed the post-create call to a secrets script at an incorrect path. Compose configuration and repeat initialization were verified without launching containers.

## Development container startup verified

Removed five fixed service container names that collided with the private HomeApp development containers. Ran the installed Dev Containers CLI with `up --skip-post-create`: startup returned success and all six public project containers started. The workspace is `/workspaces/PublicHomeApp`. The dependency-installation post-create command was skipped for this verification. No commits or pushes were made.

## Location removal and Discord history review (2026-10-09)

- Removed the tracked port catalogue, real place names/codes and real coordinate fixtures. Tests use explicitly fictional labels and synthetic geometry only.
- Removed region-specific provider names, API endpoint defaults, alert-region presets and locale overrides. Road traffic requires a locally configured HTTPS API root and key; region names come from saved user input.
- Destination lookup loads an optional ignored deployment asset, `frontend/src/assets/vessel-ports.private.json`. The CSV importer writes only that asset. No matching destination is guessed when it is absent or invalid.
- Scanned both commits reachable from all local Git refs and all 363 stored Git blobs (including unreachable objects) for Discord webhook URLs and token patterns: zero credential candidates. Discord references are configuration variables, empty settings and secret-scanner rules.
- The earlier historical OMDb Gitleaks alert is a false positive: it spans an empty API-key setting and the next non-secret setting. No OMDb key was saved at that reported location.
- Historical personal names, a device hostname and commit author identities remain in existing Git history. This task did not rewrite history or push changes.
- Verification: frontend development build, destination-matching tests, backend commute and expense tests, and importer/Git-ignore checks. The browser smoke test uses intercepted synthetic data.

## Release readiness repairs (2026-10-09)

- Kept the Angular development-server `allowedHosts` list unchanged at the maintainer's request.
- Updated Angular/PrimeNG to 21, refreshed the reviewed npm lockfile, and replaced the legacy Karma/Webpack test stack with Angular's Vitest runner. The complete npm audit reports zero vulnerabilities, including development dependencies.
- Updated OpenTelemetry and replaced ImageSharp with Magick.NET 14.17.2. A shared raster loader selects JPEG, PNG, GIF, BMP, TIFF or WebP from file signatures before decoding; delegate-based document/vector formats are rejected. NuGet auditing covers direct and transitive packages, with vulnerability warnings treated as restore errors.
- Invalid AI settings now return HTTP 400 with a useful message. Invalid or duplicate rows are rejected before database changes rather than silently discarded. Regression tests verify saved models survive rejected updates.
- Expanded production and development Docker exclusions for environment files, credentials, key material and runtime data. A synthetic Docker build confirmed nested credentials are excluded. Release images use `npm ci` without a fallback install.
- Added an exact historical Gitleaks fingerprint exception for the verified empty-OMDb-key false positive. History scanning still uses the default rules and the Discord webhook rule.
- Default production/development Compose port bindings are localhost. Deployment scripts use the same default. Explicit private-interface overrides and access requirements are documented; built-in user authentication has not been added.
- Added the MIT license, root README, synthetic desktop/mobile screenshots, current frontend instructions, build/test CI and npm/NuGet/Docker dependency-update configuration.
- Lazy-loaded commute, expenses, AI chat and settings pages. Preserved zone-based change detection after the Angular upgrade; the browser smoke test verifies asynchronous feed refreshes and stale indicators.
- Verified: 57 backend tests, five Angular tests, six vessel tests, synthetic browser smoke checks, production frontend build, full no-camera Docker image build, six native raster conversions inside that runtime image, YAML/Compose validation, localhost port bindings, and whitespace checks. Bundle/style budget warnings remain; the initial frontend bundle is approximately 1.42 MB before compression.
- No commits, pushes, repository visibility changes or deployments were made. The camera-enabled image and actual upstream traffic/AIS integrations were not exercised by this repair.
