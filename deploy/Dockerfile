# syntax=docker/dockerfile:1
# Korkut web app image — runs the FastAPI chat UI + brain. The React SPA is prebuilt in the
# korkut-ui repo and committed under backend/src/korkut/api/static, so this image just ships it
# (no Node build here). Self-hosted; the office's data never leaves the machine.
# Build context is the repository root (see deploy/docker-compose.yml: build.context = ..).

FROM python:3.14-slim
WORKDIR /app
ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    KORKUT_ROOT=/app

# tesseract for local image OCR (Turkish + English) — keeps raw images out of the cloud.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-tur \
    && rm -rf /var/lib/apt/lists/*

# Python source first, static/ excluded — it's the built SPA, and its hashed filenames change on
# every korkut-ui build, which would otherwise bust the cache for the slow pip install below on
# every rebuild even when no backend code changed.
COPY --exclude=src/korkut/api/static backend ./backend

# Editable install keeps the package running from /app/backend/src so the built static dir and
# root-level config/templates resolve without packaging them as wheel data.
RUN pip install --upgrade pip && pip install -e "./backend[app]"

# Runtime assets live at the project root; KORKUT_ROOT points paths.py here.
COPY backend/src/korkut/api/static ./backend/src/korkut/api/static
COPY config ./config
COPY templates ./templates
# Keep a copy outside the bind-mount path so entrypoint.sh can re-seed static/ when the compose
# volume mount shadows it with an empty host dir (see deploy/docker-compose.yml).
RUN cp -r ./backend/src/korkut/api/static /opt/korkut-static-seed

COPY deploy/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
CMD ["python", "-m", "korkut.api"]
