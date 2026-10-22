# Voucha Android

The Android client is a Skip Fuse 1.9.13 app targeting Android API 28 and newer. Its pinned Swift 6.4.0 Android SDK ships API 28 target triples, which sets the effective client floor even though ML Kit Prompt API itself supports API 26.
Swift compiles natively with the Swift Android SDK. Jetpack Compose renders the SwiftUI shell.

## Local models

- Android AICore uses ML Kit Prompt API (beta; see `skip.yml` and the app Gradle file).
- The model downloads only after the user selects **Download on-device model**.
- Chat history is bounded before inference.
- Generation failures restore the draft. They do not retry through another provider.
- Email OTP, Turnstile, and MFA sign-in use the shared Voucha API/authentication services. The session cookie store uses SkipKeychain, and the app restores or signs out the shared session.
- On-device generated responses use the shared conversation transcript path; issue #149 tracks
  broader transcript parity.
- Turnstile requires an embedded Android WebView challenge because Cloudflare does not support native mobile SDKs for Turnstile ([Cloudflare mobile implementation](https://developers.cloudflare.com/turnstile/get-started/mobile-implementation/)). The challenge is limited to this provider-controlled flow; account entry and sign-in UI remain native. [The adopted provider-only exception and SwiftUI replacement follow-up](https://github.com/vouchington/vouchington-clients/issues/370) record the concrete blocker, strict boundary and replacement acceptance criteria.
- Android packaging reads `VOUCHA_API_BASE_URL` and `VOUCHA_TURNSTILE_SITE_KEY` from Gradle project properties or the build environment. The public Turnstile key also accepts `NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY` as a fallback; Gradle passes both values through manifest metadata, and the Android account passes them into the auth UI. For example, pass `-PVOUCHA_TURNSTILE_SITE_KEY=<public-site-key>` to the Android Gradle build. The API URL defaults to `https://voucha.ai`; an absent site key keeps OTP disabled. The site key is public configuration, not a secret.
- OpenAI-compatible endpoint settings validate the shared native endpoint policy. Cleartext
  requests also re-validate the resolved peer address in `VouchaCore` before `URLSession`
  connects. API keys use SkipKeychain.

## Pending-turn recovery

The #363 Android path saves a completed on-device turn to an owner-scoped, Keystore-encrypted
app-private record before its first `client-generated-chat` POST. Explicit Retry sends the same
serialized request bytes and message IDs after an uncertain response. A normal user sign-out first
checks that any in-memory completed turns were saved, then retains the encrypted records; only the
same user at the same configured API base can restore them after sign-in. The storage owner includes
normalized scheme, host, effective port, and API base path. Restoration never posts automatically.
A verified server response or complete transcript acknowledgement removes a record only after
checked deletion.

This path still needs full Android app and physical-device force-stop/disk-failure validation before
#363 acceptance. Forced session invalidation revokes the active account and retries encrypted save;
if that save fails, the completed turn remains locked to the original owner and API in this app
process. The same account can restore it after reauthentication, but process termination before an
encrypted-save acknowledgement can lose it. Intentional sign-out with a failed save keeps the
authenticated account and turn in place. A permanently unreadable Keystore or ciphertext record
blocks intentional sign-out until successful Retry or explicit owner-bound confirmed deletion. The
destructive recovery copy is paired with Vouchington #3304; its native contract and physical-device
acceptance remain open.

Play Integrity, Play Store signing, and release packaging remain tracked by #6620.

## Checks

```sh
mise install
mise install swift@6.3.3
mise exec -- skip android sdk install --version swift-6.4.0-RELEASE_android
mise exec -- skip checkup --native
# Point this shell at an installed Xcode 26.6; adjust the app name if needed.
export DEVELOPER_DIR="/Applications/Xcode_26.6.app/Contents/Developer"
bash swift-clients/tooling/harness.sh --checks test-android-host
mise exec -- bash swift-clients/apps/android/tooling/pre-push.sh
```

Despite its historical filename, `pre-push.sh` is a manually and CI-invoked validation wrapper; it
does not install or run as a Git hook. The wrapper runs the host Swift tests, verifies that `:app`
is the only root Gradle subproject, compiles the generated Android app and instrumentation APKs
with Gradle `:app:assembleDebug :app:assembleDebugAndroidTest`, then builds Skip test libraries
without ADB device discovery. Qualifying the tasks builds only the app's `:skipstone:` dependency
tree.
CI prefetches every pinned `skip-macos.zip` (source.skip.tools and the GitHub releases alias,
same checksum) into SwiftPM's artifact cache so the build does not live-fetch the archive. Seeding
both aliases also protects Android Studio and other direct Gradle callers from whichever canonical
artifact URL their SwiftPM resolution uses.
It covers Swift, Kotlin bridge, manifest, and Gradle changes without requiring an attached device.
CI installs the pinned Swift 6.4.0 toolchain with mise into `RUNNER_TEMP`; the wrapper exposes that
verified toolchain to Skip's Xcode-style discovery path and keeps SwiftPM SDK state job-scoped.
Host-side SwiftPM source tests use one command everywhere: the planner, the local
`test-android-host` host-lock check, and the macOS Android host CI job all run
`bash swift-clients/apps/android/tooling/run-android-host-swift-tests.sh --package-path swift-clients/apps/android --filter VouchaAndroidTests`.
The local check wraps that argv with `with-build-lock.sh`. CI runs it directly. Point
`DEVELOPER_DIR` at an installed Xcode 26.6; the entrypoint checks that selection and its Swift
6.3.3 compiler, installs mise Swift 6.3.3, and exits before SwiftPM when the pair is absent. It
does not change the machine's global Xcode selection. SwiftPM 6.4 rejects duplicate static products
in Skip's non-bridge macOS test graph, so it is not the host source-test compiler.
The Swift 6.4 compiler still builds the Android SDK and Gradle application. Those APK and
instrumentation builds, the emulator cookie-restart job, and physical-device acceptance are
separate from the host source tests. Host archives,
including the Swift Android SDK, NDK, and `skip-macos.zip`, are reused from
`$HOME/.cache/voucha/swift-android/downloads` after a checksum check via
`cached-archive.sh`. Only these immutable, checksum-verified archives persist across jobs;
each job materializes the Swift toolchain, SDK, and NDK into job-scoped `$RUNNER_TEMP` before
use, so an untrusted job cannot leave those extracted executables for a later trusted job. CI then seeds SwiftPM's
shared artifacts cache and runs
`swift package resolve --force-resolved-versions` so unlocked `swift test` does not live-fetch
Skip's CDN. Gradle uses
`$HOME/.cache/voucha/gradle` so job-scoped `HOME` remapping does not throw away the
dependency cache. Mise installs the pinned Swift toolchain into the job-scoped directory without
macOS Installer writing to the runner account. Local runs use the developer's installed Skip and
the repository-pinned mise Swift toolchain. Skip's Gradle bridge lists
`$HOME/Library/Developer/Toolchains`; that path stays a real job-scoped directory and the wrapper
links only the verified `.xctoolchain` into it because Foundation rejects a directory-level
symlink (NSPOSIX Code 20).

The ML Kit Prompt API requires supported physical hardware for inference evidence. An emulator can
validate the shell and unavailable-device state, but it cannot prove AICore generation.

The Android CI cookie-restart job installs the exact candidate debug app and instrumentation APKs
on a Google APIs emulator. Four separate instrumentation invocations seed synthetic entries in the
app's existing encrypted preference file, read them after a host-driven force-stop, commit removal
of only the cookie entry, and read again after another force-stop. The unrelated entry must remain
unchanged. Four more invocations save synthetic pending chat request bytes for two API-scoped owners
through the Android Keystore bridge, reload the exact bytes after a force-stop, delete only the
matching first-owner record, and verify that deletion and second-owner retention after another
force-stop. The runner checks the downloaded APK hashes, candidate revision, API level, and ARM64
ABI support before accepting the result. This proves the Android storage primitives across process
restarts; the Swift tests cover sign-out ordering, owner fencing, explicit retry, and no automatic
POST. It does not exercise a real account, the complete sign-out UI, server-side replay, sudden
power loss, or physical-device storage failure modes.
