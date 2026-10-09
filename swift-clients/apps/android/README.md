# Voucha Android

The Android client is a Skip Fuse 1.9.13 app targeting Android API 28 and newer. Its pinned Swift 6.4.0 Android SDK ships API 28 target triples, which sets the effective client floor even though ML Kit Prompt API itself supports API 26.
Swift compiles natively with the Swift Android SDK. Jetpack Compose renders the SwiftUI shell.

## Local models

- Android AICore uses ML Kit Prompt API (beta; see `skip.yml` and the app Gradle file).
- The model downloads only after the user selects **Download on-device model**.
- Chat history is bounded before inference.
- Generation failures restore the draft. They do not retry through another provider.
- The provider-validation shell keeps successful responses in memory because it has no signed-in session or server conversation context. #6745 owns integration into authenticated canonical chat persistence before this shell becomes the full Android client.
- OpenAI-compatible endpoint settings validate the shared native endpoint policy. Cleartext
  requests also re-validate the resolved peer address in `VouchaCore` before `URLSession`
  connects. API keys use SkipKeychain.

Play Integrity, Play Store signing, and release packaging remain tracked by #6620 and #6745.

## Checks

```sh
mise install
mise install swift@6.3.3
mise exec -- skip android sdk install --version swift-6.4.0-RELEASE_android
mise exec -- skip checkup --native
# Point this shell at an installed Xcode 26.6; adjust the app name if needed.
export DEVELOPER_DIR="/Applications/Xcode_26.6.app/Contents/Developer"
bash swift-clients/apps/android/tooling/verify-android-host-swift.sh
mise exec swift@6.3.3 -- swift test --package-path swift-clients/apps/android
mise exec -- bash swift-clients/apps/android/tooling/pre-push.sh
```

Despite its historical filename, `pre-push.sh` is a manually and CI-invoked validation wrapper; it
does not install or run as a Git hook. The wrapper runs the host Swift tests, verifies that `:app`
is the only root Gradle subproject, compiles the generated Android project with Gradle
`:app:assembleDebug`, then builds Skip test libraries without ADB device discovery. Qualifying the
task builds only the app's `:skipstone:` dependency tree.
CI prefetches every pinned `skip-macos.zip` (source.skip.tools and the GitHub releases alias,
same checksum) into SwiftPM's artifact cache so the build does not live-fetch the archive. Seeding
both aliases also protects Android Studio and other direct Gradle callers from whichever canonical
artifact URL their SwiftPM resolution uses.
It covers Swift, Kotlin bridge, manifest, and Gradle changes without requiring an attached device.
CI installs the pinned Swift 6.4.0 toolchain with mise into `RUNNER_TEMP`; the wrapper exposes that
verified toolchain to Skip's Xcode-style discovery path and keeps SwiftPM SDK state job-scoped.
For local host-side SwiftPM tests, set `DEVELOPER_DIR` to an installed Xcode 26.6 developer
directory; the verification script checks both Xcode and `xcrun` compiler paths without changing
the machine's global Xcode selection. The Android package's host-side SwiftPM tests use mise Swift
6.3.3, matched to the selected Xcode 26.6 compiler; SwiftPM 6.4 rejects duplicate static products
in Skip's non-bridge macOS test graph.
The Swift 6.4 compiler still builds the Android SDK and Gradle application. Host archives,
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
