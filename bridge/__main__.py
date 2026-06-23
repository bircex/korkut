"""Run the Korkut Mattermost bridge:  python -m bridge

Requires MATTERMOST_TOKEN (bot token) and ANTHROPIC_API_KEY in the environment.
"""

from __future__ import annotations

import logging

from core.env import load_dotenv


def main() -> None:
    load_dotenv()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(message)s")
    from bridge.mattermost import Bridge

    Bridge().run()


if __name__ == "__main__":
    main()
