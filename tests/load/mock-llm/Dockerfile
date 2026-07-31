# Tiny stdlib-only mock LLM (no pip deps). Used only by the load stack.
FROM python:3.12-slim
WORKDIR /app
COPY app.py .
EXPOSE 8080
CMD ["python", "app.py"]
