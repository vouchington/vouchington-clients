# Android Play Console policy checklist

Draft for [#65](https://github.com/vouchington/vouchington-clients/issues/65), reviewed against
source `8cd3ce2e4f29482443ea3ef53205550f79945fe6` and official guidance on 2026-10-21.
This is a release preparation worksheet, not submitted declarations, a privacy certification, or
release approval. Every unchecked item needs release-owner evidence. Recheck policy links and
answers against the exact release candidate before entering anything in Play Console.

## Source inventory

Paths below are relative to the repository root. These are source observations; the merged
release manifest and resolved runtime dependency graph have not been audited here.

| Evidence                                                                                              | Current observation                                                                                                                                                                                      | Release follow-up                                                                                                                                                   |
| ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `swift-clients/apps/android/Android/app/src/main/AndroidManifest.xml`                                 | Active permission: `INTERNET`. Location and network-state examples are comments. Backup disabled; cleartext traffic enabled. Launcher activity exported; no app-link intent filter in this template.     | Inspect merged release permissions, components, providers and network configuration, including transitive manifests.                                                |
| `swift-clients/apps/android/Android/app/build.gradle.kts`                                             | ML Kit `genai-prompt:1.0.0-beta2`, coroutines `1.10.2`, Security Crypto `1.0.0`; AndroidX runner/JUnit are instrumentation-only. Minimum SDK 28; target SDK comes from Skip's generated version catalog. | Record resolved release SDK, full transitive dependency versions, SDK disclosures and release-only differences. Do not infer a numeric target SDK from the minimum. |
| `swift-clients/apps/android/Package.swift`, `Package.resolved`, `Sources/VouchaAndroid/Skip/skip.yml` | Skip 1.9.13, Skip Fuse/bridge, SkipKeychain 0.3.2 and local core/UI dependencies; generated Gradle also declares ML Kit/coroutines.                                                                      | Inventory generated code and packaged libraries; top-level Gradle declarations alone are incomplete.                                                                |
| `swift-clients/apps/android/Skip.env`                                                                 | Bundle/application identifier input `ai.voucha.android`, version `0.0.1`, build `1`; Kotlin package `voucha.android`.                                                                                    | Confirm final Play package identity, version code and signed artifact; these are source defaults.                                                                   |
| `swift-clients/apps/android/Android/app/build.gradle.kts`                                             | API defaults to `https://voucha.ai`; endpoint/site-key configuration can override build inputs. Release signing requires a local properties file.                                                        | Record effective public configuration without recording credentials or signing material.                                                                            |

## Data Safety worksheet

Use Google's [Data Safety definitions and form guidance](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).
Off-device transmission includes SDKs and app-controlled WebViews. Review collection and sharing
separately; provider and user-initiated-transfer exceptions require evidence. Local-only storage
is not itself collection. For each applicable type, record purposes, required/optional status,
retention, deletion and any ephemeral-processing basis. Do not infer these from permission count.

In the flow table, app Swift filenames are under
`swift-clients/apps/android/Sources/VouchaAndroid/`; Kotlin paths are relative to
`swift-clients/apps/android/`; shared module filenames are under `swift-clients/core/Sources/`.

The categories below are candidates for the form, not final classifications. Account management,
app functionality and fraud prevention/security are starting purposes; analytics needs separate
provider/backend verification. Confirm whether sign-in creates new accounts and which processing
is mandatory for the shipped experience.

| Data / candidate types                                                                                                   | Implemented Android path and destination                                                                                                                                                                                                                                                       | Answer still needed                                                                                                                                                                                                                                               |
| ------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Email, user ID, authentication data/session cookies                                                                      | `AndroidAccount+Authentication.swift`, `AndroidAccount+Session.swift`, shared `VouchaAuth/SignInService.swift` and `SessionManager.swift`: email OTP, Turnstile token, MFA TOTP/login attempt, session restore/sign-out through configured Voucha API.                                         | Email/user IDs are collection candidates. Map session identifiers and verification data to the actual form taxonomy. Confirm server logs, account creation, retention and required purposes; do not treat login as anonymous.                                     |
| Prompt, assistant output, conversation/message IDs and model identity; candidate other user-generated content / messages | `ViewModel+TranscriptSending.swift`: after either provider completes a turn, encrypted pending request is saved, then `clientGeneratedChat` sends text and IDs to Voucha. `ViewModel+TranscriptLoading.swift` / `ViewModel+TranscriptMessages.swift` read server transcripts.                  | Confirm taxonomy and backend retention, visibility, processors and deletion. On-device inference does **not** make this chat path local-only. Stored server transcripts are not presumed ephemeral.                                                               |
| Prompt/history and optional API credential to a user-selected endpoint                                                   | `AndroidSettingsView.swift`, `ViewModel+Endpoint.swift`, `ViewModel+Generation.swift`; shared `OpenAICompatibleResponsesClient.swift` sends bounded text/history, model and optional bearer key to the configured endpoint.                                                                    | This is selectable implemented behavior, not a future cloud-provider feature. Evaluate remote and local-network destinations, disclosures, purposes and sharing exception evidence per recipient. Do not assume the endpoint is OpenAI or a contracted processor. |
| Turnstile browser/network data; candidate device identifiers, diagnostics, interactions, inferred location               | `AndroidTurnstileHTML.swift` loads Cloudflare challenge JavaScript in the controlled provider WebView; token goes to Voucha. `AndroidSignInView.swift` disables OTP without a configured site key.                                                                                             | Enabled when configured, not absent from release merely because the default key is empty. Obtain provider data inventory/retention and backend handling. Map actual collected data; no location permission does not prove no inferred location.                   |
| ML Kit/AICore usage and diagnostics; candidate app performance, interactions and device/other IDs                        | `AndroidAICore.swift` / `Android/app/src/main/kotlin/AICoreBridge.kt`: model status, explicit download and text inference. Google documents on-device input/output processing, but also SDK metrics/network activity.                                                                          | Audit the pinned GenAI SDK and device services against current provider disclosures. Do not declare zero telemetry because inference is local. Confirm categories, linkage, purposes and provider roles.                                                          |
| Local credentials, endpoint settings, encrypted pending turns                                                            | `AndroidAICore.swift`, `AndroidSessionCookieStorage.swift`, `AndroidPendingTurnStore.swift`, `Android/app/src/main/kotlin/CookieSnapshotBridge.kt` and `PendingTurnStoreBridge.kt`. Cookies/keys use encrypted storage; pending completed turns can survive sign-out for the same account/API. | Document retention and owner-bound recovery/deletion. Encryption at rest does not answer collection or encryption-in-transit questions. [#363](https://github.com/vouchington/vouchington-clients/issues/363) still owns physical-device/durability acceptance.   |
| Uploaded images/files, support messages, public posts/profile data and moderation reports                                | Current Android `ContentView.swift` exposes chat/settings/sign-in; no upload, support composer, social feed, report or block UI was identified in that shell. Shared API/DTO availability alone is not a reachable Android feature.                                                            | Feature-dependent: re-inventory before enabling these flows or exposing existing server content. Include collected attachments/support text and recipients if introduced. Do not copy another client's declarations.                                              |
| Advertising/tracking, crash reporting, payments and sensitive device access                                              | No explicit ad, billing or crash SDK in the reviewed direct app dependencies; no camera, microphone, contacts, location or advertising-ID permission in the source template.                                                                                                                   | Provisional absence only. Verify transitive SDKs, merged manifest, web content, backend logging and release behavior before answering “no”; inspect Play's other app-content questions for applicable declarations.                                               |

Provider starting points: Google's [ML Kit privacy terms](https://developers.google.com/ml-kit/terms)
separate local input/output processing from SDK metrics; its
[Android disclosure guide](https://developers.google.com/ml-kit/android-data-disclosure) includes
GenAI, shared device/app information and diagnostics/usage data. Review the pinned dependency,
not every unrelated ML Kit feature. Cloudflare's actual service use and any selected endpoint's
processing terms remain release-owner inputs; this checklist does not certify their practices.

- [ ] Release engineering: reconcile every active artifact for this package, including generated
      manifests and resolved dependencies, with the worksheet; retain artifact hashes and evidence.
- [ ] Backend/privacy owner: supply per-recipient data, purpose, retention, deletion and sharing
      determinations, including logs, Turnstile and model services. Record justification for each
      exception rather than assuming all third parties are service providers.
- [ ] Security/release owner: resolve the encryption-in-transit answer. The manifest allows
      cleartext, the API is configurable, and `VouchaCore/LocalLLMEndpointProfile.swift` accepts
      HTTP for restricted local hosts. Verify actual network paths and Google's criteria before
      any affirmative declaration; HTTPS defaults and encrypted preferences are insufficient.

## App content, privacy and safety gates

The release owner should review the live
[App content preparation guide](https://support.google.com/googleplay/android-developer/answer/9859455?hl=en)
for restricted-access instructions, privacy, ads and questionnaire tasks.

- [ ] **Privacy policy URL — unresolved product input.** Confirm the exact public URL and named
      developer/app identity; do not invent a URL from the API hostname. Cover authentication,
      transcript sync, endpoint transfers, providers, retention, deletion and contact details.
      Verify an accessible in-app link in the release build; none was identified in the reviewed
      Android chat/settings/sign-in shell. Check current
      [User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en)
      against the final text and disclosures.
- [ ] **Account/data deletion — unfinished Android acceptance.** Sign-out and encrypted-record
      discard are not account deletion. Determine whether OTP enables account creation (including
      a linked signup flow). If so, provide both an in-app request path and an external web resource,
      and verify associated server-data deletion and disclosed retention exceptions against Google's
      [account deletion requirements](https://support.google.com/googleplay/android-developer/answer/13327111?hl=en).
      Record tested URLs and owner; no deletion path is proven by this draft.
- [ ] **Content rating — unresolved.** Complete the IARC questionnaire for actual reachable chat,
      generated content, any user interaction and unrestricted content exposure; retain resulting
      ratings. Do not assign an age rating from this source audit. Review
      [content rating guidance](https://support.google.com/googleplay/android-developer/answer/9859655?hl=en).
- [ ] **Target audience — product decision.** Choose intended age groups and markets, reconcile
      store copy/assets and access controls, and evaluate Families obligations if children are
      included. Do not assume adult-only or child-safe operation. Use Google's
      [target audience guidance](https://support.google.com/googleplay/android-developer/answer/9867159?hl=en).
- [ ] **Ads/tracking — provisional.** Direct source inventory suggests no displayed advertising;
      confirm all packaged SDKs and web surfaces before choosing the Contains ads answer. Treat
      diagnostics and tracking purposes separately from whether banners are displayed.
- [ ] **UGC — feature/visibility-dependent.** Establish who can see server transcripts and any
      imported/shared content. If UGC is accessible, verify terms acceptance, defined prohibited
      content, ongoing moderation and the applicable report/block controls. Public UGC and 1:1
      interaction have different requirements in Google's
      [UGC policy](https://support.google.com/googleplay/android-developer/answer/9876937?hl=en).
      Android's reviewed shell does not prove these controls; broader clients' moderation APIs
      are not Android acceptance evidence.
- [ ] **Generative AI — current feature gate.** Google requires in-app offensive-output reporting
      and prevention of restricted generated content under its
      [AI-generated content policy](https://support.google.com/googleplay/android-developer/answer/13985936?hl=en).
      Verify these for both AICore and configured endpoints; no reporting action was identified in
      the current Android shell. A private chat or local model does not establish this gate is met.
- [ ] **Digital purchases/billing — unfinished feature.** No Play Billing dependency or purchase
      UI was identified in the reviewed Android shell. Shared checkout endpoints or disabled native
      purchase controls do not prove a store purchase implementation. Recheck digital-goods,
      subscription and external-payment behavior against Google's
      [Payments policy](https://support.google.com/googleplay/android-developer/answer/9858738?hl=en)
      for the final markets and any applicable program eligibility.
      [Architecture #88](https://github.com/vouchington/vouchington-clients/issues/88) and
      [implementation #89](https://github.com/vouchington/vouchington-clients/issues/89) remain
      dependencies for final purchase-related answers; do not declare store billing compliant.
- [ ] **App access — release operations.** Supply reviewer access and repeatable instructions for
      OTP, Turnstile, MFA, unavailable AICore and endpoint setup through the approved private channel.
      Ensure restricted features can be reviewed; never put real codes, credentials or keys here.

## Tracks and final handoff

- [ ] Record the resolved release target SDK and compare it with Google's
      [current target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en).
      As reviewed on 2026-10-21, ordinary mobile new-app/update submissions require API 36 or
      higher from 2026-08-31. Confirm any applicable extension in Console; none is evidenced here.
      The source's minimum SDK 28 is a separate value and proves no submission eligibility.

- [ ] Confirm Play developer account type, creation date, verification status, owner and authorized
      release operator. For personal accounts created after 2023-11-13, Google's current
      [testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)
      require at least 12 continuously opted-in closed testers for 14 days before applying for
      production access. Account applicability and production approval remain unverified.
- [ ] Prepare this worksheet before internal testing. Google's
      [Data Safety guidance](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)
      exempts exclusively internal-track apps from that form; closed/open/production distribution
      needs it. This exception does not certify overall policy compliance or production access.
- [ ] Reconcile final app content answers, privacy/deletion links, rating, audience and reviewer
      instructions with the shipped candidate. Record who approved each unresolved decision,
      evidence date, SHA/version, artifact hashes, SDK graph and screenshots/test receipts privately.
- [ ] Keep independent release gates open: [Android epic #67](https://github.com/vouchington/vouchington-clients/issues/67),
      including signing/distribution acceptance,
      [Play Integrity #79](https://github.com/vouchington/vouchington-clients/issues/79) and
      [device/durability #363](https://github.com/vouchington/vouchington-clients/issues/363).
      A policy worksheet, host tests or emulator storage checks do not prove these gates.
- [ ] Authorized release operator enters and reviews the final Console declarations for the chosen
      track only after the above evidence and feature decisions. This documentation task performs
      no Console access, submission, signing or distribution.
