# Apple privacy inventory

Repository-side inventory for the Swift iOS and macOS apps, reviewed on 2026-11-11
against source baseline `c9764824`, the current analytics changes from #616, and
the manifests in this change. It is a source and packaging audit, not an App Store
Connect submission or a legal determination.
The .NET Mac Catalyst parity head is not one of these Swift distribution targets.

## Required-reason APIs

The direct Swift uses below remain covered by the existing reasons. Apple's
[current API/category definitions](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacyaccessedapitypes/nsprivacyaccessedapitype)
associate app-private defaults with `CA92.1` and local elapsed-time calculations with
`35F9.1`.

| Symbol         | Category                                     | Reason   | Production files                                                                                                                                                                                                                                                                                    |
| -------------- | -------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UserDefaults` | `NSPrivacyAccessedAPICategoryUserDefaults`   | `CA92.1` | `swift-clients/apps/shared-app/ViewModelFactory.swift`, `swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkStore.swift`, `swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkPersistence.swift`, `swift-clients/ui/Sources/VouchaFeatures/NativeParity/NativeCommentThreadViewModel.swift` |
| `systemUptime` | `NSPrivacyAccessedAPICategorySystemBootTime` | `35F9.1` | `swift-clients/core/Sources/VouchaAPI/MemberMCPClient.swift`                                                                                                                                                                                                                                        |

`ViewModelFactory` reads the iOS request-signing preference. Bluesky link state uses
app-private defaults; production pending OAuth state uses keychain storage. Signed-out
comment-collapse preferences use app-private defaults; signed-in collapse state uses
server bookmarks. `MemberMCPRetryCooldown` uses uptime only for local retry deadlines;
it does not send boot time off device.

Both app manifests declare these two categories. `VouchaCore` and `VouchaFeatures`
declare only user defaults; `VouchaAPI` declares only system boot time. Their package
targets copy `PrivacyInfo.xcprivacy` into resource bundles. Third-party SDK reasons
remain in the SDK's own bundled manifest, rather than being added to app declarations.

The bounded source guard scans shipped Swift under apps/core/UI, excluding tests and
build/dependency directories. It is a conservative symbol/call inventory, not a full
Swift semantic analyzer: a new match requires source review. No new direct file-time,
volume-capacity, or active-keyboard use was found in this audit. File-size reads,
`DispatchTime.now().uptimeNanoseconds`, and `ProcessInfo.environment`/`arguments` are
not treated as the covered APIs merely because they involve files or time.

## Packaged dependencies

These are the current committed pins, not versions copied from the earlier inventory.

| Package       | Version / revision                                  | Shipping and manifest evidence                                                                                                                          |
| ------------- | --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| swift-crypto  | 5.1.1 / `f8fad2f95ad67b45f6340c0960e2396094d67e07`  | Core/UI locks; shipped cryptographic products include their privacy resources.                                                                          |
| swift-asn1    | 1.7.1 / `a9a5efd40eaf558a2bcd48d64b1d1646be686008`  | Transitive shipped dependency; no standalone privacy manifest.                                                                                          |
| SwiftSoup     | 2.14.3 / `e7f85f4d3dd91ca9a1e94d3328aa1ed5120a8c0f` | UI lock, used by `VouchaDesignSystem`; no standalone privacy manifest.                                                                                  |
| ViewInspector | 0.10.5 / `2dacb6e514be19379c605a5a11564d0f3fea89d9` | Test-only; not a product dependency of either app.                                                                                                      |
| sentry-cocoa  | 9.31.0 / `2f64c77f77514acd01780d1aac86ae75b7fccd04` | Both app projects pin the version. `swift-clients/tooling/generate.sh` injects this revision into the generated app lock. Core/UI do not import Sentry. |

The downloaded pinned Sentry xcframework's iOS and macOS manifests declare user
defaults (`CA92.1`), boot time (`35F9.1`), and file timestamps (`C617.1`). Its vendor
collection rows are CrashData, PerformanceData, and OtherDiagnosticData, nontracking
and vendor-declared unlinked, for AppFunctionality. Do not replace the SDK manifest
with the app's manifest or assume that its default linkage describes the configured
app integration. The app explicitly declares `NSPrivacyCollectedDataTypeCrashData` and
`NSPrivacyCollectedDataTypePerformanceData` as linked because it
adds the opaque account ID. Effective linkage of the SDK's other diagnostics remains
part of the release-owner review below.

`SentryAppSupport.swift` and `SentryLaunchPlan.swift` start Sentry only with a valid
configured `VOUCHA_SENTRY_DSN`. Default PII is disabled; performance sampling is 0.1
when enabled. Event, breadcrumb, and span projections scrub sensitive request data.
`SentryAccountBinding` clears or replaces the bound opaque ID when account ownership
changes. Email and username are not Sentry user properties.

## Collected data in the app manifests

Every app row is linked to the user and not used for tracking. Purposes are listed
explicitly below; package manifests have no app-level collection rows. All five
first-party manifests set tracking false and omit tracking domains. The declarations
cover configured collection even when a particular build has no Sentry DSN.

Apple's [data-type identifiers](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacycollecteddatatypes/nsprivacycollecteddatatype)
and [purpose identifiers](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacycollecteddatatypes/nsprivacycollecteddatatypepurposes)
are the source for the strings below. These are declarations supported by observed
native behavior, not final store questionnaire answers.

| Manifest suffix (`NSPrivacyCollectedDataType…`) | Purpose                     | Source evidence                                                                                                                                                                                                                                              |
| ----------------------------------------------- | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `EmailAddress`                                  | AppFunctionality            | Settings email verification and signed-in email identity.                                                                                                                                                                                                    |
| `UserID`                                        | AppFunctionality, Analytics | Account identity and authenticated first-party product signals.                                                                                                                                                                                              |
| `DeviceID`                                      | AppFunctionality            | Server `dt` cookie persistence and App Attest key submission. This is not an analytics installation-ID implementation.                                                                                                                                       |
| `PhotosorVideos`                                | AppFunctionality            | User-selected profile, post, and topic image uploads. Apple's token uses lowercase `or`.                                                                                                                                                                     |
| `OtherUserContent`                              | AppFunctionality            | Posts, comments, profile markdown, and other member-authored API content.                                                                                                                                                                                    |
| `OtherFinancialInfo`                            | AppFunctionality            | `PaymentCardDraft.updateBody` and `PaymentCardService.update` submit credit-limit amounts; `SpendingCategoryDraft` and `SpendingCategoryService` submit budget amounts and frequency. No full payment-card number or CVV form is established by these paths. |
| `CrashData`                                     | AppFunctionality            | Configured Sentry crash/error reporting with the opaque account ID.                                                                                                                                                                                          |
| `PerformanceData`                               | AppFunctionality            | Configured Sentry traces, with the approved account linkage.                                                                                                                                                                                                 |
| `ProductInteraction`                            | Analytics                   | Signed-in landing visits/clicks and RSS feed/item views.                                                                                                                                                                                                     |
| `EmailsOrTextMessages`                          | AppFunctionality            | `DirectMessagesViewModel+Mutations.swift` submits member text through `Endpoint+Messages.swift`; private in-app messages are a distinct category from public content.                                                                                        |

### Current analytics boundary

`NativeRouteSurfaceViewModel+ProductSignals.swift` sends landing visits/clicks and RSS
feed views; `NativeRouteSurfaceViewModel+FocusedRssItem.swift` sends item views.
`dispatchProductSignal` requires `isSignedIn` and uses the normal authenticated client.
These are the existing first-party feature endpoints, not a generic event-ingestion SDK.
Failures do not block the UI. This is why ProductInteraction is declared now even
though [#478](https://github.com/vouchington/vouchington-clients/issues/478) remains open.

The analytics requests use the existing authenticated session for account attribution;
they do not add email, username, IP address, or free-form content properties. Signed-out
views send no analytics request. No installation identifier or app-open event is added.
Landing visits omit all optional referrer and campaign strings, including `utm_*` and
`ref` URL values; the page identifier is carried by the endpoint route, with an empty
request body. Click bodies contain only the item and optional group-member identifiers.
These source declarations do not establish a signed archive privacy report or an App
Store Connect submission.

The approved direction is account identity while signed in and an opaque installation
identity before sign-in, without email/username/IP event properties or cross-company
tracking. The currently audited Swift paths do not emit installation-ID events while
signed out. Linkage on sign-in, identifier rotation/clearing, remaining surface coverage,
and any generic app-open events remain in #478. Do not describe that planned behavior
as already shipped or add an ATT prompt for the approved nontracking design.

## Export and owner/release determinations

`ITSAppUsesNonExemptEncryption` remains false in both Apple Info.plists and project
settings. Shipped code uses HTTPS/ATS, swift-crypto/CryptoKit, App Attest, and keychain
services. This preserves repository metadata; the owner must confirm the appropriate
export-compliance answers for the actual selected distribution path.

## Open decisions

These remain outside repository-only completion. They must be listed in the release
review and resolved by the owner; the manifests do not silently answer them.

- **Account deletion and retention:** `SettingsViewModel+ManagementActions.swift`
  sends `deleteUser` and handles immediate deletion versus a deletion request. Confirm
  all account types, retained records, third-party deletion, and the store's deletion
  requirements before making a compliance claim.
- **Purchases and financial disclosures:** membership checkout and billing portal links
  remain server/web flows; no StoreKit integration was found. Confirm the selected
  storefront/distribution rules, payment-provider data access, purchase-history and
  payment-data answers. Review dynamic MCP financial tools and any future credit-score
  forms separately; a DTO alone is not evidence of a shipped native form.
- **Names, sessions, and other data:** determine whether display names contain legal
  names, how `st`/`dt` credentials and App Attest identifiers appear in store answers,
  whether retained server search/social-graph data requires additional categories,
  and whether optional external support-email collection needs disclosure. The Swift
  settings support action is a `mailto:` link, not an in-app support-ticket POST.
- **SDK diagnostics:** reconcile Sentry's vendor-declared OtherDiagnosticData and its
  effective linkage/purposes with the configured release and the server-side retention
  policy. The app's linked crash/performance declarations already preserve the owner's
  stated decision; do not treat the SDK's default unlinked rows as proof of anonymity.
- **Analytics identity:** resolve #478's pre-sign-in linkage/rotation and any additional
  events in the producer contract before expanding native behavior or store answers.
- **Store review:** choose final age-rating, user-generated-content/moderation, privacy
  policy/deletion links, and privacy/export questionnaire answers. Recheck push-token
  collection if Apple targets gain remote-notification registration.
- **Signed artifacts and App Store Connect:** build each selected release artifact with
  owner-provided configuration/signing; inspect its aggregate privacy report and submit
  the final answers in App Store Connect. No store access or submission occurred here.

## Static and artifact verification

Run from the repository root:

```sh
node --test tests/apple-privacy-manifest.test.mjs
python3 scripts/verify-apple-privacy.py --source-root .
python3 scripts/verify-apple-privacy.py --source-root . --artifact /path/to/Voucha.app
python3 scripts/verify-apple-privacy.py --source-root . --artifact /path/to/Voucha.xcarchive
```

The Node tests run in the existing Validate suite on Linux. They verify reason maps,
collection types/linkage/tracking/purposes, app/package resource wiring, and the bounded
source inventory. Positive iOS/macOS app/archive fixtures and negative malformed,
missing-resource, reason, and policy cases exercise the portable plist checker.
The artifact check inspects actual bundle resources, including Sentry's pinned SDK
reason declarations; source configuration tests alone do not establish packaging.

Generate projects only after `pnpm run contracts:pull`, using
`bash swift-clients/tooling/generate.sh macOS` and the equivalent `iOS` command.
Both projects were generated during this audit. A local unsigned Release archive
attempt stopped at the existing non-test `VOUCHA_TURNSTILE_SITE_KEY` requirement;
no key was invented and no release gate was bypassed. The unsigned macOS Debug build
succeeded with Xcode 27.2; the checker passed against its actual app, all three
first-party resource bundles, and embedded Sentry manifest. The app also contains
the parseable `swift-crypto_Crypto.bundle` privacy resource.
That result establishes local packaging only, not signed-release acceptance.

For the official aggregate report, archive the final configured target in Xcode,
then use **Organizer → archive context menu → Generate Privacy Report** and review the
PDF alongside the SDK manifests. Apple's [documented report workflow](https://developer.apple.com/documentation/bundleresources/describing-data-use-in-privacy-manifests)
is an Xcode Organizer action; this runbook does not invent an `xcodebuild` export flag.
A structurally valid plist or a local bundle check does not establish that the
App Store Connect answers, encryption attestation, or final signed archive agree.
