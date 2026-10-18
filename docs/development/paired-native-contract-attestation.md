# Paired native contract review evidence

The `Paired native contract review evidence` workflow is a manually invoked, informational
snapshot check for one open Vouchington pull request and one open native-clients pull request.
It does not add a required status to either pull request or replace the default native contract
workflow. The default workflow continues to validate its event-bound producer `main` contract.

An operator with live `maintain` or `admin` permission in the clients repository supplies both
pull request numbers, the exact producer and clients head SHAs they reviewed, and an explicit
confirmation that they reviewed those snapshots. It verifies both the selected ref and workflow
definition ref are clients `main`, rejects reruns, and resolves the PRs in the two fixed
repositories. The submitted SHAs are identity assertions, never checkout refs. The workflow
checks each PR is open and targets its repository's `main`, checks the resolved head against the
reviewed SHA, and checks out only GitHub's merge ref for that PR number. It verifies the merge
commit and its base/head parents, then rechecks both live PR snapshots after staging. If a head,
base, merge revision, or PR state changes, the run fails.

The workflow uses three jobs so candidate-controlled package install and exporter code never run
in a job that later receives the GitHub API token. The candidate job has no explicit token or
secrets; its uploaded staging data is treated as untrusted input by the final job. The final job
runs trusted code on a fresh runner. It checks out the same exact merge snapshots as data, verifies
their identities, validates the uploaded staging tree with trusted representative and generated-tree
checks, compares the contract declaration with trusted clients `main`, and rechecks live PR
snapshots before writing evidence. The fixture subtree is compared byte-for-byte with the exact
producer merge checkout; staged localization is checked against the exact clients merge checkout.
It does not execute the candidate exporter in the authenticated job. The manifest labels staged
bytes as `untrusted-candidate-exporter` output; the evidence remains an informational paired-snapshot
preflight, not an independent security review of candidate code.

The workflow stages only the paths already declared by the trusted clients
`contracts/filaments.json`, checks the candidate clients generated-localization copies, and writes
a separate strict manifest with both PR snapshots, the operator and confirmation, the workflow
run identity, allowlisted paths, and every staged file's size and SHA-256. The short-lived
artifact is evidence for those exact snapshots; it is not GitHub approval and cannot be rebound
to a later PR head by changing metadata.

The manifest records the final identity recheck, not an atomic lock on either pull request. A
later push makes the evidence stale for that new source revision; dispatch the workflow again
with the new exact heads after reviewing them. The workflow does not publish a check run, approve
either pull request, or claim the full native compiler and platform test matrix passed. A real
dispatch requires this workflow to be present on the repository's default branch first.
