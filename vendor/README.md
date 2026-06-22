# vendor/ — Reference source (not committed to the repo)

The third-party code in this folder is kept outside the repo via `.gitignore`. We adapt the
case law (içtihat) / legislation (mevzuat) **tools** (`tools/ictihat_ara`, `tools/mevzuat_ara`, `tools/karar_metin`) from their
httpx clients (see KAYNAKLAR.md §8, MIMARI.md §12). MIT — the copyright notice (telif notu) is preserved in the
imported files.

## Re-fetching
```bash
mkdir -p vendor && cd vendor
git clone https://github.com/saidsurucu/yargi-mcp.git
git clone https://github.com/saidsurucu/mevzuat-mcp.git
```

## Pinned versions (the commits the adaptation is based on)
| Repo | Commit | Date | License |
|------|--------|-------|--------|
| saidsurucu/yargi-mcp | `6bbc656dc6143208201bec5edda07da322cd67ee` | 2026-06-18 | MIT |
| saidsurucu/mevzuat-mcp | `5671233b3e77ea82d11cf35a22af1985c27bf892` | 2026-06-18 | MIT |

> Important: Do NOT USE the remote MCP endpoints he hosts (surucu.dev) — we run them on our own
> server (privacy). Data access does not require a token (plain httpx).
