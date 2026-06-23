# Korkut web app image — builds the React SPA, then runs the FastAPI chat UI + brain.
# Self-hosted; the office's data never leaves the machine.

# 1) Build the single-page app (outputs to /app/web/static via vite.config.ts).
FROM node:22-alpine AS frontend
WORKDIR /app/web/frontend
COPY web/frontend/package.json web/frontend/package-lock.json* ./
RUN npm ci
COPY web/frontend/ ./
RUN npm run build

# 2) Python runtime.
FROM python:3.12-slim
WORKDIR /app
ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

# tesseract for local image OCR (Turkish + English) — keeps raw images out of the cloud.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-tur \
    && rm -rf /var/lib/apt/lists/*

COPY pyproject.toml README.md LICENSE ./
COPY core ./core
COPY tools ./tools
COPY korkut ./korkut
COPY config ./config
COPY templates ./templates
COPY web ./web
# Built SPA from the frontend stage (overwrites the gitignored web/static).
COPY --from=frontend /app/web/static ./web/static

RUN pip install --upgrade pip && pip install ".[app]"

EXPOSE 8080

CMD ["python", "-m", "web"]
