#!/bin/sh
# The web app's static/ dir is bind-mounted from the host (see docker-compose.yml) so a refreshed
# SPA (`make ui`) shows up without an image rebuild. On a fresh clone that host dir is empty and
# would shadow the SPA baked into this image, so re-seed it from the image's build-time copy
# whenever the mounted dir is empty. Never overwrites a populated static/.
set -e

STATIC_DIR=/app/backend/src/korkut/api/static
SEED_DIR=/opt/korkut-static-seed

if [ -d "$SEED_DIR" ] && [ ! -f "$STATIC_DIR/index.html" ]; then
  echo "==> static/ is empty (fresh bind mount) - seeding built SPA from image"
  cp -r "$SEED_DIR"/. "$STATIC_DIR"/
fi

exec "$@"
