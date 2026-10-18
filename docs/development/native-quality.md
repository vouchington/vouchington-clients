# Native quality and test planning

This repository owns Swift and .NET static analysis, test-authoring instructions, and changed-file
test planning. Run repository rules and contract assertions from this checkout:

```sh
pnpm run quality:check
pnpm test
```

Plan only the tests affected by committed native changes:

```sh
pnpm run test:plan:swift --base origin/main --head HEAD
pnpm run test:plan:dotnet --base origin/main --head HEAD
```

Pass one or more `--changed-file <path>` arguments instead of `--base`/`--head` when planning an
uncommitted edit. The planner emits the canonical client harness commands; run those commands from
this checkout so its host-lock and coverage policies apply.

Vouchington remains the producer of the shared API fixture and localization inputs. Its durable
references are the
[API fixture contract](https://github.com/vouchington/vouchington/blob/main/backend/test-helpers/api-fixtures/README.md)
and
[native localization exporter](https://github.com/vouchington/vouchington/blob/main/dev/native-localization.mts).
Stage those producer outputs with `scripts/stage-native-contract.mjs`, then point
`VOUCHA_FILAMENTS_CONTRACT_ROOT` at the isolated stage before running `contracts:sync` or
`contracts:check`. Native quality and test planning never read product source from Vouchington.

CI placement is a quality rule: portable .NET tests run once on Linux; Swift core tests run on
Linux and macOS only because those jobs compile different networking and Security slices. See
[native CI test placement](native-ci-test-placement.md).

## Refresh a stale stacked pull-request candidate

The native contract handoff is bound to the event's pull request number, base SHA, head SHA, and
merge revision. If verification reports that the live pull request no longer matches the artifact,
that run is stale. Do not reuse its artifact or rely on rerunning the same event: the original event
identity remains unchanged.

For a stacked pull request, wait until its parent has landed on the intended base. Then update the
child's base and branch to the current parent result and push the real child update. Use only the new
pull-request event run, after its artifact's number, base SHA, head SHA, and merge revision all
match the live pull request and merge ref. Artifact verification also binds the producing run ID
and attempt; both must match the new run. If any identity differs, keep the stale run rejected and
refresh the child again from the current parent result. This recovery preserves exact-candidate
verification; it does not turn a stale artifact into a valid one.
