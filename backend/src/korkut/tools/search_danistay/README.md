# search_danistay

Search **Danıştay (Council of State)** decisions via the Bedesten API
(`bedesten.adalet.gov.tr`). No key required. Same contract as `search_yargitay`, with
`itemTypeList=["DANISTAYKARAR"]`.

## CLI
```bash
python -m tools.search_danistay --query "idari işlem" --chamber "3. Daire" --limit 5
```

## Output
Same shape as `search_yargitay` (`total`, `page`, `count`, `results[]` with `documentId`,
`court`, `caseNo`, `decisionNo`, `decisionDate`, `decisionType`). Pass a `documentId` to
**fetch_decision** for full text.
