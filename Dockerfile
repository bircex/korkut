# Korkut web app image. Self-hosted; runs the FastAPI chat UI + the tool-calling brain.
FROM python:3.12-slim

WORKDIR /app

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

COPY pyproject.toml README.md LICENSE ./
COPY core ./core
COPY tools ./tools
COPY web ./web
COPY korkut ./korkut
COPY config ./config
COPY templates ./templates

RUN pip install --upgrade pip && pip install ".[app]"

EXPOSE 8080

CMD ["python", "-m", "web"]
