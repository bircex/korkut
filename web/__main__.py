"""Run the Korkut web app:  python -m web   (http://localhost:8080)

Host/port come from KORKUT_HOST / KORKUT_PORT. Loads .env first so the API keys and DB URL
are present (same loader the CLI uses).
"""

from __future__ import annotations

import os

from core.env import load_dotenv


def main() -> None:
    load_dotenv()
    import uvicorn

    host = os.getenv("KORKUT_HOST", "0.0.0.0")
    port = int(os.getenv("KORKUT_PORT", "8080"))
    uvicorn.run("web.app:app", host=host, port=port, log_level="info")


if __name__ == "__main__":
    main()
