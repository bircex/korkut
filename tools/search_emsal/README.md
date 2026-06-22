# search_emsal

Search **UYAP Emsal** precedent decisions — local civil courts (`YERELHUKUK`) and civil
courts of appeals / istinaf (`ISTINAFHUKUK`) — via the Bedesten API. No key required.

## CLI
```bash
python -m tools.search_emsal --query "kira tespiti" --limit 5
```

## Output
Same shape as `search_yargitay`. Pass a `documentId` to **fetch_decision** for full text.
