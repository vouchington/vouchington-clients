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

The hosted job builds an unsigned iOS device archive using Xcode 26.6. It then
packages that app as an inspection IPA: a zip whose only top-level directory is
`Payload/Voucha.app`. The packaged app must be `arm64`, must not contain a
provisioning profile, and must carry the same version, build, and
`ai.voucha.ios` bundle identifier. The staged copy's `Info.plist` is converted
to XML before it is zipped so the checker can read those keys; the archived
app zip is left unchanged. The workflow accepts only an ad hoc no-team
signature or no signature at all. It retains both the zipped app and
`Voucha-unsigned.ipa` for seven days.

This IPA is layout evidence, not an App Store export. `xcodebuild -help` lists
the iOS export methods `app-store-connect`, `release-testing`, `enterprise`,
`debugging`, and `validation`; each re-signs the archive. The unsigned macOS
method `mac-application` does not apply to an iOS archive, so this job does not
call `-exportArchive`. It also does not call `altool` or an upload API. A
signed IPA and a TestFlight upload still need the Apple team, signing assets,
and App Store Connect access tracked by
[#72](https://github.com/vouchington/vouchington-clients/issues/72) and
[#70](https://github.com/vouchington/vouchington-clients/issues/70).
