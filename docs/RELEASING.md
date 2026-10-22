# Releasing

Korkut uses [Calendar Versioning](https://calver.org/): `YYYY.MM.PATCH`. The first release of a month
is `YYYY.MM.0`, each further release that month adds one to PATCH (`2026.10.0`, `2026.10.1`, …).
Months are not zero-padded. Tags are `v` + version (`v2026.10.0`).

## Day to day: keep the changelog

Every pull request that changes behaviour adds a line under **`## [Unreleased]`** in
[CHANGELOG.md](../CHANGELOG.md), in the right group (`Added`, `Changed`, `Fixed`, `Removed`,
`Security`). Those lines become the release notes, so write them for the people running Korkut.

## Cutting a release

1. Open **Actions → Release → Run workflow** on `main`.
2. Optionally tick **Dry run** first: it runs every step (tests, image builds for both
   architectures) but pushes nothing.
3. Run it for real. The workflow:

   | Job | What it does |
   |---|---|
   | Verify main | The .NET build and tests, and the UI lint, type-check and tests |
   | Version and tag | Next version from the existing tags (`scripts/release/next-version.sh`); moves the Unreleased notes into a dated section (`scripts/release/changelog.py cut`); sets the UI and website package versions; commits `Release vX` to `main` as BirceX and pushes the `vX` tag |
   | Image … | Builds each image (`korkut`, `korkut-migrator`, `korkut-ocr`, `korkut-kikproxy`) for `linux/amd64` and `linux/arm64` on native runners, with `KORKUT_VERSION` baked in (image label, environment and assembly informational version) |
   | Publish … | Joins both architectures under `ghcr.io/bircex/<image>:X` and `:latest` |
   | GitHub release | Builds the install bundle (`scripts/release/bundle.sh`): `korkut.tar.gz`, `korkut.zip`, `SHA256SUMS`; creates release `vX` with the changelog notes and install instructions; redeploys the website |

The workflow refuses to release when Unreleased is empty.

## What users download

The bundle contains `docker-compose.yml`, `install.sh`, `install.ps1`, `docker/init-db.sh`,
`.env.example`, the changelog, the license and a `VERSION` file, and no source code. Its asset names
carry no version, so the newest release is always at:

```
https://github.com/bircex/korkut/releases/latest/download/korkut.tar.gz
```

`install.sh` sees the `VERSION` file (and no `src/` folder), writes `KORKUT_VERSION` to `.env`,
pulls that version's images and starts them. Unpacking a newer bundle over the same folder and
running the installer again upgrades in place; `.env` is not in the bundle, so secrets and settings
are kept, and the data volumes belong to the fixed compose project `korkut`.

From a source checkout the same `install.sh` builds the images locally instead (tagged
`ghcr.io/bircex/<image>:local`).

## One-time repository settings

- **Packages:** after the first release, set each `ghcr.io/bircex/korkut*` package to **public**
  (Package settings → Change visibility) so installs can pull without signing in.
- **Pages:** Settings → Pages → Source: **GitHub Actions** (the website workflow deploys there).
- **Branch rules:** the release commits to `main` with the workflow's token. If a ruleset that
  requires pull requests is enforced on `main`, add **GitHub Actions** to its bypass list.

## Fixing a bad release

Releases are not edited in place. Fix forward on `main` with a changelog line and cut the next
patch release (`YYYY.MM.PATCH+1`). If a release must be withdrawn, mark it as a pre-release on
GitHub and point users at the fixed one.
