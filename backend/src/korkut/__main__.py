"""Terminal chat with Korkut — a quick local test of the brain.

    ANTHROPIC_API_KEY=... python -m korkut

Type a question, press Enter, Korkut answers using the tools. Ctrl-C to quit.
"""

from __future__ import annotations

import sys

from korkut.core.env import load_dotenv

load_dotenv()

from korkut.brain.agent import answer  # noqa: E402 — after load_dotenv so keys are present


def main() -> None:
    print("Korkut (terminal). Soru sor, çıkmak için Ctrl-C.\n")
    try:
        while True:
            text = input("» ").strip()
            if not text:
                continue
            print("\nKorkut: " + answer(text) + "\n")
    except (KeyboardInterrupt, EOFError):
        print("\nGörüşürüz.")
        sys.exit(0)


if __name__ == "__main__":
    main()
