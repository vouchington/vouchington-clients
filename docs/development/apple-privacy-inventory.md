# Apple privacy inventory

Source-backed inventory for the iOS and macOS apps. Nutrition-label rows below are the
manifest entries justified by current native code. Rows under Open decisions are not
answered here.

Audit date: 2026-10-21, stacked on the in-app display-metadata change for #71.

## Required-reason APIs

Apple's required-reason categories were checked across `swift-clients/apps`,
`swift-clients/core`, and `swift-clients/ui`, excluding tests and `test-support`.
Only the symbols below appear in shipped Swift.

| Symbol         | Category                                     | Reason   | Production files                                                                                                                                                                                         |
| -------------- | -------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UserDefaults` | `NSPrivacyAccessedAPICategoryUserDefaults`   | `CA92.1` | `swift-clients/apps/shared-app/ViewModelFactory.swift`, `swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkStore.swift`, `swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkPersistence.swift` |
| `systemUptime` | `NSPrivacyAccessedAPICategorySystemBootTime` | `35F9.1` | `swift-clients/core/Sources/VouchaAPI/MemberMCPClient.swift`                                                                                                                                             |

`CA92.1` matches app-private `UserDefaults.standard`. `ViewModelFactory` reads
`requestSigningDisabled` only in the `DeviceCheck` build, which is the iOS app target.
`NativeBlueskyLinkStore()` stores the Bluesky link result in `UserDefaults.standard`.
Pending OAuth and Bluesky link state in that production initializer uses the keychain,
not user defaults. `UserDefaultsNativePendingState` is the injected storage used by
tests and by `NativeBlueskyLinkStore.init(defaults:)`.

`35F9.1` matches `MemberMCPRetryCooldown`, which uses `ProcessInfo.processInfo.systemUptime`
as a local monotonic deadline for MCP rate-limit backoff. The uptime value is not sent
off device. `retryAfterSeconds` is the server-provided limit or the remaining local
interval.

The same two categories are declared on both app `PrivacyInfo.xcprivacy` files because
those targets ship the calling code. `VouchaCore` declares only user defaults.
`VouchaAPI` declares only system boot time. Both package manifests are copied into the
target resource bundles with `.copy("PrivacyInfo.xcprivacy")`.

## Reviewed and not declared

These calls were reviewed and are not required-reason symbols:

- `DispatchTime.now().uptimeNanoseconds` in `ImageUploadService.swift` is not
  `systemUptime` or `mach_absolute_time`.
- `url.resourceValues(forKeys: [.fileSizeKey])` reads a file size, not a file timestamp
  or a volume-capacity API.
- `FileManager` use elsewhere creates, moves, removes, or checks files. It does not read
  creation or modification dates.
- `ProcessInfo.processInfo.environment` and `.arguments` are not boot-time APIs.
- No shipped Swift uses `mach_absolute_time`, `volumeAvailableCapacity`, `statfs`,
  `statvfs`, `fstatfs`, `getattrlist`, `activeInputModes`, or the file-timestamp resource
  keys.

## Packaged dependencies

Pins are the committed `Package.resolved` revisions.

| Package              | Revision                                   | Shipped?                                                 | Manifest                                                                                                                                                |
| -------------------- | ------------------------------------------ | -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| swift-crypto 5.0.1   | `1c80d3aff53fedc445b60692980cb4f19a618657` | Yes, through `VouchaCore`, `VouchaAPI`, and `VouchaAuth` | Each product ships `PrivacyInfo.xcprivacy` with tracking off and an empty accessed-API array. The checkout has no `UserDefaults` or `systemUptime` use. |
| swift-asn1 1.7.1     | `a9a5efd40eaf558a2bcd48d64b1d1646be686008` | Yes, transitive through swift-crypto                     | No `PrivacyInfo.xcprivacy`. The checkout has no required-reason symbols from the scan above.                                                            |
| SwiftSoup 2.14.2     | `7a185638b31911a9980655b70a26af1279d4917b` | Yes, through `VouchaDesignSystem`                        | No privacy manifest. Tag `2.14.2` has no required-reason symbols from the scan above.                                                                   |
| ViewInspector 0.10.5 | `2dacb6e514be19379c605a5a11564d0f3fea89d9` | No. `VouchaUITests` only                                 | Not part of either app target. Not scanned as a shipped dependency.                                                                                     |

## Collected data in the app manifests

All four manifests set `NSPrivacyTracking` to false and omit `NSPrivacyTrackingDomains`.
No App Tracking Transparency, advertising identifier, or third-party analytics SDK is
linked. Package manifests declare no collected data types; collection is an app-level
claim.

Each row is linked to the account, not used for tracking, and declared only for app
functionality.

| Manifest type                                | Source                                                                                                                                        |
| -------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `NSPrivacyCollectedDataTypeEmailAddress`     | Settings email-address verification and the email shown for a signed-in account.                                                              |
| `NSPrivacyCollectedDataTypeUserID`           | Account id, username, and `displayName` sent and shown for the signed-in member.                                                              |
| `NSPrivacyCollectedDataTypeDeviceID`         | Server `dt` device token persisted as a cookie in `APIClient+Session.swift`, and the App Attest key sent to `/api/v1/app-attestation/attest`. |
| `NSPrivacyCollectedDataTypePhotosorVideos`   | User-selected images uploaded from `fileImporter` surfaces (profile, post compose, topic image).                                              |
| `NSPrivacyCollectedDataTypeOtherUserContent` | Posts, comments, and other member-authored content sent to the API.                                                                           |

`NSPrivacyCollectedDataTypePhotosorVideos` is Apple's manifest identifier, including
the lowercase "or".

## Export compliance

`ITSAppUsesNonExemptEncryption` is false on both Apple `Info.plist` files. Shipped
cryptography is HTTPS with ATS (`NSAllowsArbitraryLoads` false, plus the existing
localhost exception), SHA-256 from swift-crypto and CryptoKit, App Attest, and the
keychain. No non-standard encryption algorithm is implemented in the native clients.

## Open decisions

These are not encoded as final nutrition-label or review answers.

- Account deletion. `SettingsViewModel.deleteAccount()` sends `deleteUser` after the
  member types "delete my account". The response either logs the member out or reports
  that deletion was requested. Whether that meets Guideline 5.1.1(v) for every account
  type, including data kept after a requested deletion, is open.
- Purchases. The app does not use StoreKit. `purchaseMembership` creates a server
  checkout URL, and `openMembershipPortal` opens a billing portal URL. Payment data,
  purchase history, and Guideline 3.1.1 are open.
- Legal name. `displayName` is declared with the user id, not as `Name`. Whether any
  display name is a legal name that must also be declared as `Name` is open.
- Session cookie `st`. It is an account session credential stored beside `dt`. It has
  no separate manifest type. Whether App Store Connect should describe it separately
  is open.
- Tracking and diagnostics. The false tracking flag covers the current dependency set.
  A future ads, attribution, or crash SDK would change it. Crash logs and product
  interaction are not declared.
- Age rating. The questionnaire is not answered.
- Guideline 1.2. Members can report a post, comment, or detail
  (`NativeDetailSurface+Reporting.swift`, `NativeCommentThreadViewModel+Mutations.swift`).
  A complete block control and moderation-response claim was not established.
- Push tokens. iOS and macOS sources do not register for remote notifications. The
  Android Darwin host does. Apple push collection is open if that registration is added.

## CI

`tests/apple-privacy-manifest.test.mjs` fails when an app or package manifest is
removed, when a declared reason or collected type changes, or when a new shipped Swift
file uses a required-reason symbol outside the table above. The test runs with the
Node suite in the Validate workflow.

## Not proven

XcodeGen 2.46.0 generated both app projects and added `PrivacyInfo.xcprivacy` to each
target's Resources build phase. This inventory does not include an archived Xcode
privacy report (`xcodebuild -exportPrivacyReport` or an archive). `plutil -lint` only
checks that the property lists parse. App Store Connect nutrition-label submission and
TestFlight export-compliance prompts were not exercised. The local Xcode 27.1 SDK does
not ship the collected-data type string catalog, so the nutrition-label identifiers were
not reconfirmed from SDK headers.
