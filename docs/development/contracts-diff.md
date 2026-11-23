# Contract diff

`pnpm run contracts:diff --base <rev> --head <rev> [--changes <file>]` reports which Vouchington
producer commits this repository pinned at two of its own git revisions, and optionally which
contract files changed between them. It is a thin wrapper for test planning; the general
folder-to-changes comparison is moving into `no-mistakes changes diff --base-root <dir> --head-root <dir>`.

- `--base` and `--head` are git revisions of this repository. The pin is read with
  `git show <rev>:contracts/vouchington.json` and validated like `contracts:pull` validates it.
- Each pinned commit is fetched with the same SHA-keyed shallow fetch as `contracts:pull`
  (`.cache/vouchington/<sha>`). There is no other fetch path and no sibling-checkout discovery.
- `VOUCHA_CONTRACT_ROOT` is ignored. A diff needs two roots and both come from pins in this
  repository's history, so an explicit root would be ambiguous.
- Both pins must declare the allowlisted `paths` of the current client contract
  (`readContractConfig`). A pin with different paths is invalid, so it yields `complete: false`
  rather than a partial comparison.

## Stdout

```json
{
  "schemaVersion": 1,
  "base": { "revision": "<rev>", "commit": "<producer sha>", "root": "<checkout>" },
  "head": { "revision": "<rev>", "commit": "<producer sha>", "root": "<checkout>" },
  "complete": true,
  "changes": "<absolute path of --changes file, when requested>"
}
```

On any failure (missing or invalid pin, failed fetch, failed staging) stdout is still valid JSON
with `complete: false` and an `error` string, and the exit code is 1. Callers fall back to the
full suite.

## Changes file

Written only with `--changes`. It is temporary: file-level, no key-level localization diff.

```json
{
  "schemaVersion": 1,
  "base": { "revision": "<rev>", "commit": "<sha>" },
  "head": { "revision": "<rev>", "commit": "<sha>" },
  "fixtures": { "changed": [], "added": [], "removed": [] },
  "localization": { "changed": [], "added": [], "removed": [] },
  "contracts": { "changed": [], "added": [], "removed": [] },
  "complete": true
}
```

- `fixtures`: fixture IDs. A response body `api-fixtures/v1/responses/<id>.json` is the fixture
  `<id>` (the manifest `bodyFile` is `responses/<id>.json` for every fixture, and both loaders
  read `bodyFile` by ID). A `manifest.json` entry whose content changed, matched by `id`, also
  reports that ID.
- `localization`: repo-relative paths of changed generated files under the localization paths.
  The generated trees do not exist in the producer, so each commit is staged through the producer
  exporter (`dev/native-localization.mts --output-root`) into a temporary root, against the client
  tree of that side's own revision (`git archive`). This checkout's generated directories are never
  written.
- `contracts`: paths relative to `api-contracts/v1`, plus any other changed file under the
  allowlisted paths that is not a response body or generated localization (for example
  `api-fixtures/v1/manifest.json`, repo-relative).
- Lists are sorted and deduplicated. Identical commits give empty lists with `complete: true`.
