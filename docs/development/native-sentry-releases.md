# Native Sentry releases

Issue [#505](https://github.com/vouchington/vouchington-clients/issues/505) separates
repository integration from owner configuration and release acceptance. A blank DSN leaves
reporting disabled. No upload token belongs in an app, plist, assembly metadata, artifact, or
pull-request build.

## SDK configuration

Swift iOS/macOS apps read `VOUCHA_SENTRY_DSN`, `VOUCHA_SENTRY_RELEASE`, and
`VOUCHA_SENTRY_ENVIRONMENT` from the runtime environment before packaged Info.plist values.
The matching Xcode build settings package these values. .NET reads the same environment names
before assembly metadata; MSBuild properties are `VouchaSentryDsn`, `VouchaSentryRelease`, and
`VouchaSentryEnvironment`. Installed applications need packaged configuration; a developer's
runtime environment does not configure an installed release.

An empty release keeps the existing version/build identifier; an empty environment keeps the
Debug/Release default. Explicit release identifiers should be stable across retries of the same
artifact. Debug files are associated by their debug IDs, rather than by guessing a filename or
release version. Never rebuild symbols independently of the application being diagnosed.

The existing event, breadcrumb, and transaction scrubbers remain in force. Account binding sends
only the opaque account ID while signed in and clears or replaces it on sign-out/account switch.
Do not enable default PII, source upload, request bodies, typed content, or broader user identity.
The Apple app manifests and the pinned Sentry SDK manifest remain covered by
[the privacy inventory](apple-privacy-inventory.md); symbol upload does not add runtime collection.

## Trusted uploads

Uploads use a separate job and protected `sentry-release` GitHub environment. Configure protection
rules to admit only `main` and approved iOS release tags, with required reviewers as appropriate.
PR builds never enter the upload job or receive the upload token. A missing token or project
configuration skips upload; configured uploads fail visibly if symbols are absent or rejected.

Apple jobs retain the dSYMs from the exact archive. The .NET upload uses PDBs from the existing
Release Mac Catalyst app build on `main`, including the app and its Core dependency. This does
not add Windows packaging or signing. Windows release PDB upload remains pending a trusted
Windows release artifact producer; Mac Catalyst PDBs must never be used for a Windows binary.
Preserve matching binaries and symbols for each shipping
artifact; an unsigned CI build is inspection evidence, not a signed/store release.

## Owner configuration and acceptance

The owner must create/select separate Sentry projects for iOS, macOS, and .NET as needed, configure
the following configuration, and ensure the public DSNs point to the projects used for uploads:

| Location                            | Name                                                                           | Purpose                                      |
| ----------------------------------- | ------------------------------------------------------------------------------ | -------------------------------------------- |
| `sentry-release` environment secret | `SENTRY_AUTH_TOKEN`                                                            | Debug-file upload only                       |
| Repository or environment variable  | `SENTRY_ORG`                                                                   | Sentry organization slug                     |
| Repository variables                | `SENTRY_PROJECT_IOS`, `SENTRY_PROJECT_MACOS`, `SENTRY_PROJECT_DOTNET`          | Project slugs for symbol uploads             |
| Repository variables                | `VOUCHA_SENTRY_DSN_IOS`, `VOUCHA_SENTRY_DSN_MACOS`, `VOUCHA_SENTRY_DSN_DOTNET` | Public SDK DSNs packaged into trusted builds |

Trusted builds package `voucha-ios@<commit>`, `voucha-macos@<commit>`, or
`voucha-dotnet@<commit>` as the release and `production` as the environment. The upload credential needs only the
organization/project permissions required for debug-file upload. This change does not create,
inspect, or request any credential.

After configuration, run a trusted archive/build, confirm successful debug-file processing in the
selected Sentry project, and verify a controlled crash resolves to the matching application stack.
Repeat against the actual signed release/store artifact. Check environment/release values, account
binding transitions, and that privacy manifests are included in archived apps. These owner checks
cannot be replaced by mocked CLI tests or an unsigned hosted build.

## Android gap

Swift-on-Android currently has no Sentry runtime integration in this repository.
[Skip Sentry](https://skip.dev/docs/modules/skip-sentry/) provides an Android wrapper, but its
documented `beforeSend` event is read-only (keep/drop only), and it does not wrap performance
transactions/spans. It cannot preserve this client's field-level scrubbing and tracing contract
without a native Android adapter. Compatibility with this app's pinned Skip Fuse toolchain and
privacy/account boundaries remains unverified. Android Release minification can generate a ProGuard
mapping, but uploading that file alone does not enable crash reporting or associate obfuscated
stacks with a Sentry event. No mapping upload is enabled until a suitable Android SDK/Skip adapter,
privacy scrubber and account lifecycle boundary, and mapping UUID injection are implemented and
tested together. Track that product/platform gap under #505. Do not treat Apple/.NET uploads as
Android acceptance.
