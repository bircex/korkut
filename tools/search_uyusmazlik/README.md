# search_uyusmazlik

Search **Uyuşmazlık Mahkemesi** (Court of Jurisdictional Disputes) decisions via the official
portal `kararlar.uyusmazlik.gov.tr`. The court resolves jurisdiction (*görev*) and judgment
(*hüküm*) conflicts between the judicial and administrative court orders.

The portal is an ASP.NET WebForms search page (no JSON API): a GET seeds the `__VIEWSTATE`
tokens, a POST replays them with the query and runs the search, returning a `GridView1` of 20
rows/page parsed here. Each result links to the decision **PDF**. No API key. (The host's TLS
certificate is rejected by modern stacks, so verification is disabled — see
`tools/_base/uyusmazlik.py`.)

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `query` | string | yes | — | search text |
| `scope` | string | no | `all` | `all` (full content), `esas` (case no), `karar` (decision no) |
| `limit` | integer | no | 10 | results from the page (portal serves 20/page) |
| `page` | integer | no | 1 | 1-indexed (server-side paging walked to this page) |
| `case_sensitive` | boolean | no | false | match case exactly |

## CLI
```
python -m tools.search_uyusmazlik --query "imar" --limit 5
echo '{"query": "imar", "scope": "all"}' | python -m tools.search_uyusmazlik
```

## Output
`{"total", "page", "count", "results"}`; each result is `{id, esasNo, kararNo, date, url}`. Pass
`url` (the decision PDF) to `fetch_uyusmazlik`.

## Endpoint
`POST https://kararlar.uyusmazlik.gov.tr/` (WebForms postback; `txtSearch` / `rblSearchScope`).
