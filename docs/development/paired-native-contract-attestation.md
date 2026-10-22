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

The candidate job runs the producer exporter only in a pinned Node container with no network,
read-only source mounts, a fresh output mount, no forwarded environment or credential/cache/socket
mounts, a private PID namespace, no capabilities, and bounded resources. It does not install
candidate dependencies. The trusted runner treats the container output as untrusted data and stages
only the declared localization directories plus the fixture subtree from the producer checkout.
Trusted host scripts validate and copy those bytes; they never execute candidate code. A required
Docker negative test checks that the container cannot read a runner sentinel, reach the network, see
a host-only file/process or Docker socket, or write to its source mounts. Candidate output is uploaded
as untrusted data for the final job.

The final job runs trusted code on a fresh runner. It checks out the same exact merge snapshots as
data, verifies their identities, validates the uploaded staging tree with trusted representative and
generated-tree checks, compares the contract declaration with trusted clients `main`, and rechecks
live PR snapshots before writing evidence. The fixture subtree is compared byte-for-byte with the
exact producer merge checkout; staged localization is checked against the exact clients merge
checkout. The manifest labels staged bytes as `untrusted-candidate-exporter` output; the evidence
remains an informational paired-snapshot preflight, not an independent security review of candidate
code.

The workflow stages only the paths already declared by the trusted clients
`contracts/filaments.json`, checks the candidate clients generated-localization copies, and writes
a separate strict manifest with both PR snapshots, the operator and confirmation, the workflow
run identity, allowlisted paths, and every staged file's size and SHA-256. The short-lived
artifact is evidence for those exact snapshots; it is not GitHub approval and cannot be rebound
to a later PR head by changing metadata.

The manifest records the merge commit's actual ordered parents, the PR head as its second parent,
and the live `main` SHA. The first parent must be an ancestor of that live main snapshot; PR base
metadata is retained separately because it can advance after GitHub creates a merge ref. The
manifest records the final identity recheck, not an atomic lock on either pull request. A
later push makes the evidence stale for that new source revision; dispatch the workflow again
with the new exact heads after reviewing them. The workflow does not publish a check run, approve
either pull request, or claim the full native compiler and platform test matrix passed. A real
dispatch requires this workflow to be present on the repository's default branch first.

The optional `run_native_tests` input adds a selected native evidence pass. It defaults to false,
so the existing data-only review path stays unchanged. When enabled, Linux runs Swift Core and
portable .NET tests against the exact captured clients merge SHA, and macOS runs Swift Core against
that same SHA because Darwin networking and security behavior differs from Linux. A separate macOS
runner runs the Android host tests and builds the app and instrumentation APKs. A fresh Linux API 35
emulator runner executes the selected restart profile using those reviewed APK bytes. The explicit
`native_test_profile` choice is either `cookie-snapshot` (the four cookie persistence phases) or
`pending-turn-recovery` (all four cookie phases plus all four pending-turn phases). There is no
automatic phase detection or downgrade: the runtime artifact and trusted finalizer both bind the
selected profile and exact ordered phase list. The Linux and macOS Swift Core jobs and Linux .NET
job install toolchains from the exact candidate's pinned configuration. The Android host build uses
the workflow's fixed Swift 6.3.3 host compiler and pinned Skip Swift 6.4.0 Android SDK/NDK target.
Candidate test jobs have no GitHub token or secrets, do not persist checkout credentials, and share
no dependency cache.

Each native execution job uploads an untrusted, short-lived artifact. A separate trusted finalizer
binds each artifact's ID and digest to the current workflow run and the upload action outputs,
validates the fixed APK file set, source SHA, emulator properties, and every phase required by the
captured profile, then rechecks both live pull request snapshots. The runtime artifact also carries
cleanup evidence for the run-owned `paired-native-<run id>` AVD: a bounded allowlisted snapshot of
the captured launcher and its descendants at launch and shutdown. Observed launch evidence includes
that launcher and only rows whose parent chain reaches it. A PID whose start time changed, and a
scan that exceeds its time or byte bound, is `unknown` and does not extend the shutdown budgets.
Missing, ambiguous, truncated, malformed, or bounded observations are recorded as `unknown`. The
finalizer rejects cleanup evidence that does not match that same run, attempt, profile, candidate
merge SHA, and runtime artifact ID and digest. The supplemental artifact is
informational and candidate-controlled; it is not a signed test result or a GitHub check. The
workflow does not change default required checks. Successful evidence reflects only the selected
commands and platforms, not the full native matrix or physical-device acceptance.

APK validation uses separate fixed file budgets because Swift Android debug libraries make the app
larger than ordinary contract artifacts. The app is bounded at 384 MiB, the instrumentation APK at
32 MiB, and revision/checksum metadata at 128 bytes/1 KiB. The generic 256 MiB per-file and 512 MiB
aggregate artifact limits remain in effect outside that APK-specific policy; the aggregate limit
also applies to APK bundles. File allowlists, source identities and checksums remain required.
