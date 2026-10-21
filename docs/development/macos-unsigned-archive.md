# macOS unsigned archive and DMG check

`release-macos-unsigned.yml` runs on macOS release-source pull requests and by manual dispatch.
It generates the macOS project, archives a Release app with code signing disabled, exports
the archive with Xcode's `mac-application` Copy App method, and assembles and verifies a DMG.
The build number is `<GitHub run number>.<attempt>.0` in `CFBundleVersion`, checked in the
archive and export. The workflow fails if the first two components exceed Apple's 4-digit
and 2-digit limits. This counter is specific to the macOS workflow; rerunning an older run
does not imply a globally increasing build number. The CI artifact is
named `macos-unsigned-ci-*` and the image is `Voucha-UNSIGNED-CI.dmg`.

This artifact is build evidence only. It has no Developer ID signature or notarization ticket,
and the workflow deliberately fails if the exported app verifies as signed. Do not publish it
as a direct download or submit it to the Mac App Store. A release path still needs separate
Developer ID signing, notarization, stapling, Gatekeeper validation, and Mac App Store signing
and submission with the corresponding accounts and app records.

Apple documents [Copy App as an unsigned macOS export](https://developer.apple.com/documentation/xcode/distributing-your-app-for-beta-testing-and-releases)
and [DMG as a direct-distribution container](https://developer.apple.com/documentation/xcode/packaging-mac-software-for-distribution).
The exact export method is listed by `xcodebuild -help` on the selected Xcode installation.
