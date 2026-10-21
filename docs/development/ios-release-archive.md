# iOS unsigned release archive

The `iOS unsigned release archive` workflow runs on matching pull requests, pushed
`ios-vMAJOR.MINOR.PATCH` tags, and manual dispatch. Manual dispatch requires an
existing release tag that points at the selected revision. Pull requests use
`ios-v0.0.0` only as an archive preview; they do not create a release.

The tag sets `CFBundleShortVersionString`. `CFBundleVersion` is the workflow's
`run_number.run_attempt.0`, so each run and rerun has a different build number.
The workflow fails if the run number exceeds 9999 or the attempt exceeds 99,
keeping the values within [Apple's documented build-version component limits](https://developer.apple.com/library/archive/documentation/General/Reference/InfoPlistKeyReference/Articles/CoreFoundationKeys.html#//apple_ref/doc/uid/TP40009249-SW1).
An older run rerun after a newer run can have a lower build number; do not use
that rerun as a later App Store upload. The workflow checks the final archived
app's version keys.

The hosted job builds an unsigned iOS device archive using Xcode 26.6 and
retains a zipped `Voucha.app` for inspection for seven days. It is not a signed
IPA, installable distribution build, or TestFlight upload. Producing those
requires the Apple team, signing assets, provisioning, and App Store Connect
access tracked by [#72](https://github.com/vouchington/vouchington-clients/issues/72)
and [#70](https://github.com/vouchington/vouchington-clients/issues/70).
