# Korkut web app image — builds the React SPA, then runs the FastAPI chat UI + brain.
# Self-hosted; the office's data never leaves the machine.
# Build context is the repository root (see deploy/docker-compose.yml: build.context = ..).

# 1) Build the single-page app. vite.config.ts emits to ../backend/src/korkut/api/static,
#    so with WORKDIR /app/frontend the output lands at /app/backend/src/korkut/api/static.
FROM node:26-alpine AS frontend
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2) Python runtime.
FROM python:3.14-slim
WORKDIR /app
ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    KORKUT_ROOT=/app

# tesseract for local image OCR (Turkish + English) — keeps raw images out of the cloud.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-tur \
    && rm -rf /var/lib/apt/lists/*

# Runtime assets live at the project root; KORKUT_ROOT points paths.py here.
COPY backend ./backend
COPY config ./config
COPY templates ./templates
# Built SPA from the frontend stage (the gitignored static dir).
COPY --from=frontend /app/backend/src/korkut/api/static ./backend/src/korkut/api/static
# Keep a copy outside the bind-mount path so entrypoint.sh can re-seed static/ when the compose
# volume mount shadows it with an empty host dir (see deploy/docker-compose.yml).
RUN cp -r ./backend/src/korkut/api/static /opt/korkut-static-seed

# Editable install keeps the package running from /app/backend/src so the built static dir and
# root-level config/templates resolve without packaging them as wheel data.
RUN pip install --upgrade pip && pip install -e "./backend[app]"

COPY deploy/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
CMD ["python", "-m", "korkut.api"]
