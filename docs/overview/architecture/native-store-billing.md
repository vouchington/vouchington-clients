# Native store billing

This is the client architecture for Apple, Google Play, and Microsoft Store memberships.
It decides how the native apps present products, start store purchases, and recover, and how
those flows map onto the Filaments membership contract. It does not implement billing.
[The current footprint](reference-native-clients-current-footprint.md) remains presentation-only
until #89 lands.

Filaments stays the entitlement authority. A store sheet can take payment. Only `memberships/me`
changes the plan the app shows as effective. Issue #88 and Filaments plan
[#10630](https://github.com/jonathanong/filaments/issues/10630) are the source of this boundary.
The fixtures below were read from Vouchington commit `d9a5b03f8d0f00f861bbf6d75891689bef3e2ac2`.

## Invariants

- Purchase and restore stay inside the platform store UI for that target.
- The apps do not host a WebView, embedded checkout, Stripe Checkout session, or Stripe billing
  portal for membership billing.
- The apps submit store evidence. They do not validate receipts, keep provider secrets, or choose
  the effective plan.
- Pending, owned, conflict, rejected, retry, refund, revocation, and reconciliation states come
  from the contract. The screen does not send the person to a support queue.
- Relaunch, device change, duplicate delivery, delayed store state, and a backend outage converge
  by retrying the same client work and reading the server again.

## Shipped targets

Store code lives in the app target that owns that store. Portable Swift core stays Foundation-only.
`Voucha.Client.Core` does not reference MAUI or `Windows.Services.Store`. Shared code owns the
contract session, the outbox, and the lifecycle model. Adapters own the store SDK.

| Target                                            | Shipped floor                                         | Store API                  |
| ------------------------------------------------- | ----------------------------------------------------- | -------------------------- |
| Swift iOS and iPadOS, `ai.voucha.ios`             | iOS 17.4 (`swift-clients/apps/iOS/project.yml`)       | StoreKit 2                 |
| Swift macOS, `ai.voucha.macos`                    | macOS 14.4 (`swift-clients/apps/macOS/project.yml`)   | StoreKit 2                 |
| Swift Android, application id `ai.voucha.android` | minSdk 28, `arm64-v8a` (`swift-clients/apps/android`) | Play Billing Library 9.1.0 |
| .NET Windows                                      | `net10.0-windows10.0.26100.0`, minimum `10.0.17763.0` | `Windows.Services.Store`   |
| .NET Mac Catalyst                                 | `net10.0-maccatalyst`, minimum Mac Catalyst 15.0      | None                       |

Android's Play application id is `PRODUCT_BUNDLE_IDENTIFIER` in `swift-clients/apps/android/Skip.env`
(`ai.voucha.android`). `ANDROID_PACKAGE_NAME` is `voucha.android` and is the Kotlin package, not the
Play package used in a subscription deep link. The plans fixture uses `ai.voucha.android`.

`Windows.Services.Store` was introduced in Windows 10, version 1607 (`10.0.14393.0`). The Windows
app's `TargetPlatformMinVersion` is `10.0.17763.0`, so the app does not also target
`Windows.ApplicationModel.Store`. Mac Catalyst keeps the shared read model for parity tests and does
not call StoreKit or the Microsoft Store. The shipping Apple apps are the Swift apps. The shipping
Windows app is the MAUI Windows target. Its `ApplicationId` is `ai.voucha.dotnet`; that string is not
the Microsoft Store application id in the catalog.

Play Billing Library 9.1.0 (18 June 2026) requires Android API 23 or newer and is documented against
compile SDK 35. minSdk 28 satisfies the library floor. That compile SDK is the library floor. Google
Play's target API policy, updated 16 October 2026, requires new apps and app updates to target
Android 16 (API 36) from 31 August 2026. A granted extension runs only to 1 November 2026. This app
sets `targetSdk` from the Skip compile SDK. #89 stops if that SDK is below 35, and it does not submit
a Play build while the compile and target SDK are below 36 unless a granted extension is in force.
The Android billing dependency is `com.android.billingclient:billing-ktx:9.1.0`, added only in the
Android app module. The app does not call the Billing Choice or external-payment APIs added in 9.1.0.

## Shared contract session

Both cores implement one session. Adapters supply store identifiers and evidence bytes. They do not
interpret them.

1. `GET /api/v1/memberships/plans` loads the catalog.
2. The person chooses a canonical product. The client generates one idempotency UUID for that attempt
   and posts `POST /api/v1/membership-purchase-intents` with `provider`, canonical `product_id`, and
   `idempotency_key`.
3. `201` returns a typed `launch` payload. `409` returns `CONFLICT`, `eligible_at`, and `management`.
   The client does not open a store sheet after `409`.
4. The adapter runs the store UI with the launch payload.
5. On store success, the client posts `POST /api/v1/membership-verifications` with `provider`,
   `idempotency_key`, `evidence`, and the purchase-intent id when the purchase started from an intent.
   A new submission returns `202`. An exact replay returns `200` and the same verification.
6. The client polls `GET /api/v1/membership-verifications/:verificationId` until `status` is
   `verified`, `conflict`, or `rejected`, and refreshes `GET /api/v1/memberships/me`.
7. While a verification on `memberships/me` has `next_processing_at`, the next poll waits until that
   instant. Otherwise the wait backs off from 1 second through 2, 4, and 8 seconds, then every 15
   seconds. Foreground and a store update refresh immediately. The poll stops when the verification
   is terminal.

Retries of one attempt reuse its idempotency key. A new store transaction, a new Play token, or a
refreshed Microsoft key pair is a new submission with a new key. The sample Apple fixture reuses one
UUID for the idempotency key, the intent id, and `app_account_token`. The client still generates the
idempotency key. It copies `app_account_token` from the launch payload after the response.

The secure outbox holds unsent evidence in the Keychain, the Android encrypted store, or MAUI
`SecureStorage`. It is removed after `202` or replay `200`. Evidence, purchase tokens, and Store ID
keys are not written to logs or ordinary preferences.

A launch whose `kind` is `stripe_checkout` is not opened. Native clients do not post `provider:
stripe`. A `409` whose destination is `billing_portal` shows `eligible_at` and does not start a
portal session.

## Product and SKU presentation

`GET /api/v1/memberships/plans` returns `products` and `benefit_catalog`. Each product has a canonical
`id`, `plan`, and `interval`, plus `providers[]`. A provider row has `provider`, `environment`,
`application_id`, store `product_id`, `base_plan_id`, `offer_id`, `sku_id`, and `price`. Store
`price` values in the fixture are null. The store SDK is the price source. Stripe rows are not
purchasable in the native apps. Their amounts are not shown as the native price.

The client shows a product only when one provider row matches the running store and application:

| App                  | Provider          | Application id                                 |
| -------------------- | ----------------- | ---------------------------------------------- |
| Swift iOS and iPadOS | `apple_app_store` | `ai.voucha.ios`                                |
| Swift macOS          | `apple_app_store` | `ai.voucha.macos`                              |
| Swift Android        | `google_play`     | `ai.voucha.android`                            |
| .NET Windows         | `microsoft_store` | the `microsoft_store` rows the catalog returns |

The sample plans fixture has an Apple row only for `ai.voucha.ios`, a Play row for
`ai.voucha.android`, and a Microsoft row whose `application_id` is `Voucha`. Windows selects
`microsoft_store` rows and purchases the launch payload. It does not compare that application id with
`ai.voucha.dotnet`. macOS currently has no matching row. That gap is an open decision below.

Localized store prices:

- Apple: `Product.products(for:)` then `Product.displayName`, `Product.description`, and
  `Product.displayPrice`.
- Play: `BillingClient.queryProductDetailsAsync` for `ProductType.SUBS`. The selected
  `SubscriptionOfferDetails` pricing phase supplies `PricingPhase.getFormattedPrice()`.
- Microsoft: `StoreContext.GetAssociatedStoreProductsAsync` or `GetStoreProductsAsync` for the launch
  `product_id`. The localized price is the `StorePrice` on that `StoreProduct` or `StoreSku`.

`benefit_catalog` is rendered with the existing localized benefit keys. `free` is a catalog value,
not a product. `membership: null` with no effective source is the free state.

Play offer selection uses the launch payload, not a client promotion. Find the
`SubscriptionOfferDetails` whose base plan id equals `launch.base_plan_id`. When `offer_id` is
non-null, also match that offer id. When `offer_id` is null, use the offer whose offer id is null.
Pass that offer token to `ProductDetailsParams.setOfferToken`. If nothing matches, do not launch
billing and do not substitute another offer. An empty offer token is an error in Play Billing
Library 9.

## Apple StoreKit 2

Used from the Swift iOS and macOS app targets:

- `Product.products(for:)`
- `Product.purchase(options:)`
- `Product.PurchaseOption.appAccountToken(_:)`
- `Product.PurchaseResult.success`, `.userCancelled`, and `.pending`
- `VerificationResult.jwsRepresentation` for both `.verified` and `.unverified`
- `Transaction.updates`, `Transaction.unfinished`, and `Transaction.currentEntitlements`
- `Transaction.appAccountToken`, `Transaction.ownershipType`, `Transaction.finish()`
- `AppStore.sync()`
- `AppStore.showManageSubscriptions(in:)` on iOS and iPadOS

`Product.purchase(options: [.appAccountToken(uuid)])` uses the UUID from
`launch.app_account_token`. The client does not create that UUID. In the Apple purchase-intent
fixture and in Filaments `getNativeMembershipLaunch`, that value is the purchase-intent id.

Direct purchases require the token. Family Sharing does not. `Transaction.ownershipType.purchased`
is the purchaser. `Transaction.ownershipType.familyShared` is a family member's own transaction, and
`appAccountToken` may be nil. The client still submits `jwsRepresentation`. It does not copy a token
onto another account and it does not decode the JWS. Any other ownership value, including a value the
SDK may surface later, is submitted the same way and is not a local entitlement. Filaments accepts
family access only for `FAMILY_SHARED` evidence and does not expect an app account token on it.

Purchase results:

- `.success` submits `jwsRepresentation` with the purchase-intent id. `.verified` and `.unverified`
  both submit. Neither one changes the effective plan.
- `.userCancelled` submits nothing.
- `.pending` is Ask to Buy or another interrupted approval. Submit nothing until `Transaction.updates`
  delivers a transaction. The plan stays unchanged.

`Transaction.finish()` runs after the verification post returns `202` or replay `200`, including when
the later status is `rejected` or `conflict`. The server has the evidence then. A failed post leaves
the transaction unfinished. `Transaction.updates` starts at launch. Launch and explicit Restore also
walk `Transaction.unfinished` and `Transaction.currentEntitlements` and submit any transaction not yet
accepted. `AppStore.sync()` runs only from the Restore control, because Apple documents that it
prompts for App Store credentials. Reinstall and a new device already expose current entitlements
without `sync()`.

Renewal, revocation, and an Ask to Buy approval arrive through `Transaction.updates`. Each new
transaction is a new evidence submission. `revocationDate` is a reason to submit and refresh. It does
not by itself remove the plan. The screen changes when `memberships/me` changes.

Upgrade, downgrade, and crossgrade are a new purchase in the same App Store subscription group.
Apple applies the timing: an upgrade is immediate, a downgrade waits until the next renewal, and a
crossgrade depends on duration. The client purchases the product id from the new intent. It does not
compute the tier change. iOS and iPadOS cancellation and subscription management use
`AppStore.showManageSubscriptions(in: UIWindowScene)`, which does not need a subscription-group id.
Apple documents that this sheet is not available on macOS, including Mac Catalyst and iOS apps
running on a Mac. The macOS management action is an open decision below.

## Google Play Billing Library 9.1.0

Used from the Android app module:

- `BillingClient.newBuilder`, `PurchasesUpdatedListener`, `startConnection`
- `enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())`
- `enableAutoServiceReconnection()`
- `queryProductDetailsAsync` and `launchBillingFlow`
- `queryPurchasesAsync` for `ProductType.SUBS`
- `Purchase.getPurchaseToken()`, `getPurchaseState()`, `isAcknowledged()`, and
  `getPendingPurchaseUpdate()`
- `BillingFlowParams.setObfuscatedAccountId`
- `BillingFlowParams.setSubscriptionUpdateParams`
- `SubscriptionUpdateParams.setOldPurchaseToken`
- `BillingFlowParams.ProductDetailsParams.setOfferToken`
- `BillingFlowParams.ProductDetailsParams.setSubscriptionProductReplacementParams`
- `SubscriptionProductReplacementParams.setOldProductId` and `setReplacementMode`

`enablePendingPurchases` uses the parameterized API because Library 9 removed the no-argument method.
`enableOneTimeProducts()` is required to build those params. `enablePrepaidPlans()` is not called.
Google documents subscription pending transactions, including `PendingPurchaseUpdate`, for prepaid
plans only, and the published plans fixture has no prepaid base plan. Membership queries still
request `ProductType.SUBS` only. `setObfuscatedAccountId` receives
`launch.obfuscated_account_id` unchanged. The server sends the hex SHA-256 of the user id, which is
64 characters. The client does not hash it again.

The client does not call `BillingClient.acknowledgePurchase`. Filaments acknowledges a verified
purchased subscription once. `Purchase.isAcknowledged() == false` with state `PURCHASED` is the
finishing state on screen. Entitlement still comes from `memberships/me`.

An auto-renewing purchase is not treated as pending-capable. If Play still returns `PENDING` or
`PendingPurchaseUpdate`, the client submits that token and does not change the plan. When Play moves
the purchase to `PURCHASED`, `PurchasesUpdatedListener` runs and the client submits the new token.
Entitlement still comes from `memberships/me`.

`linkedPurchaseToken` is a field on the Play Developer subscriptions resource. The client does not
read it and does not send it. The client submits `Purchase.getPurchaseToken()` for the new purchase.
Filaments follows the replacement chain. For a replacement launch, Library 9 uses both levels.
`SubscriptionProductReplacementParams.setOldProductId` is the Play product id from
`queryPurchasesAsync`, and `setReplacementMode` is the item-level mode. The purchase being replaced
is identified by `BillingFlowParams.setSubscriptionUpdateParams` with
`SubscriptionUpdateParams.setOldPurchaseToken` set to that same purchase's `getPurchaseToken()`.
The client keeps that token only for the launch. It is not a verification field.
`SubscriptionUpdateParams.setSubscriptionReplacementMode` is not called. Once any product sets
`setSubscriptionProductReplacementParams`, that older mode setter has no effect, and its integer
constants differ from `SubscriptionProductReplacementParams.ReplacementMode`. Those product-level
values are `WITH_TIME_PRORATION` 1, `CHARGE_PRORATED_PRICE` 2, `WITHOUT_PRORATION` 3,
`CHARGE_FULL_PRICE` 4, `DEFERRED` 5, and `KEEP_EXISTING` 6. Which mode to send is an open decision
below. Without a mode, a purchase that would replace an owned subscription does not call
`launchBillingFlow`.

Restore and RTDN recovery use the same query. The client does not subscribe to Pub/Sub. Filaments
receives Real-time developer notifications, fetches authoritative subscription state, and projects
`memberships/me`. The client calls `queryPurchasesAsync` at launch, from Restore, and from
`PurchasesUpdatedListener`, submits tokens that are not yet accepted, and refreshes `memberships/me`.
`ITEM_ALREADY_OWNED` uses that same query. `USER_CANCELED` submits nothing. `SERVICE_UNAVAILABLE`,
`NETWORK_ERROR`, and `BILLING_UNAVAILABLE` keep the intent and retry. `enableAutoServiceReconnection`
covers a dropped Play connection.

Cancellation opens the Play subscriptions center as an Android view intent, not a WebView:

`https://play.google.com/store/account/subscriptions?sku=<play-product-id>&package=ai.voucha.android`

The product id is the Play product id of the owned subscription. The package is the application id,
not `voucha.android`. If the Play Store cannot handle the intent, the control is inactive.

## Microsoft Store

Used from the Windows MAUI target, on the UI thread for purchase UI:

- `StoreContext.GetDefault()`
- `WinRT.Interop.InitializeWithWindow.Initialize(storeContext, windowHandle)` before any call that
  shows UI. `StoreContext` implements `IInitializeWithWindow`. The handle is the MAUI window HWND.
- `RequestPurchaseAsync(storeId)` and `StoreSku.RequestPurchaseAsync()`
- `GetAssociatedStoreProductsAsync`, `GetStoreProductsAsync`, and `GetUserCollectionAsync`
- `GetAppLicenseAsync`
- `GetCustomerCollectionsIdAsync(serviceTicket, publisherUserId)`
- `GetCustomerPurchaseIdAsync(serviceTicket, publisherUserId)`
- `StorePurchaseStatus`: `Succeeded`, `AlreadyPurchased`, `NotPurchased`, `NetworkError`,
  `ServerError`

`POST /api/v1/memberships/microsoft-store/service-tickets` has an empty body and returns
`collections_service_ticket`, `purchase_service_ticket`, `publisher_user_id`, and `expires_at`. The
client passes `publisher_user_id` through and does not invent one. It then calls
`GetCustomerCollectionsIdAsync` and `GetCustomerPurchaseIdAsync`. Microsoft documents both returned
Store ID keys as valid for 30 days. The service ticket is a separate, shorter credential bounded by
`expires_at`.

`GetUserCollectionAsync` and `GetAppLicenseAsync` tell the client what the Store currently lists for
this install. They do not change the effective plan. Billing state, including active, canceled,
expired, revoked, and dunning, is read by Filaments from the collections and recurrence services with
those keys. The client does not call the Microsoft collections, purchase, or change-billing-state
REST APIs.

Purchase result handling:

- `Succeeded` and `AlreadyPurchased` mint or reuse unsent keys and submit evidence with the launch
  `product_id` and `sku_id`.
- `NotPurchased` submits nothing.
- `NetworkError` and `ServerError` keep the intent and retry.

Restore walks `GetUserCollectionAsync` for subscription add-ons, mints fresh keys when needed, and
submits evidence for owned subscription products that the server has not yet accepted.

Credential expiry recovery is automatic. Refresh the service ticket when `expires_at` has passed, when
a stored key is older than 30 days, on launch while a Microsoft source or outbox item exists, and
after a Microsoft verification returns `invalid_evidence`. A refreshed key pair is a new verification
submission. The screen does not extend access past `access_ends_at` on `memberships/me`. A failed
refresh keeps the last server snapshot and retries.

Filaments plan #10630 has no Microsoft in-place SKU migration in v1. The client does not send a
replacement or change-billing request. A different SKU is a new purchase intent. `409` shows
`eligible_at`. There is no `StoreContext` method that cancels a subscription. Customer cancellation
is documented by Microsoft on the account services page. Whether the Windows app may open that page
is an open decision below.

Which launch field is the argument to `RequestPurchaseAsync` is also open. The sample Microsoft
product id `9NBLGGH4V0Q1` has the shape of a Store ID. The sample `sku_id` is `0010`. Both values are
submitted to Filaments with the Store ID keys once the evidence fixture exists. The purchase UI needs
one Store ID.

## Upgrade, downgrade, and cancel

Same-provider changes follow the store. Cross-provider purchase attempts stop at `409` and do not
open a second store.

| Store     | Upgrade and downgrade                                                                                         | Cancel                                                                   |
| --------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| Apple     | Purchase the new product id. Apple applies subscription-group timing.                                         | iOS and iPadOS: `showManageSubscriptions(in:)`. macOS: open decision.    |
| Play      | Product replacement params plus `setOldPurchaseToken` when a mode is supplied. The new token is the evidence. | Play subscriptions deep link.                                            |
| Microsoft | No in-place change. A new intent purchases another SKU when the server returns `201`.                         | Open decision. The client does not call the publisher billing-state API. |

A pending switch on `memberships/me` (`pending.switches[]` with `eligible_at`) stays visible until
that source becomes effective. The client does not promote `is_effective: false` to the current plan.

## Pending, interrupted, and offline recovery

| Situation                                                      | Client behavior                                                                                                                                            |
| -------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Store approval pending (Ask to Buy, unexpected Play `PENDING`) | Show pending. Submit an unexpected Play token. Submit Apple evidence only after a transaction exists.                                                      |
| Store UI cancelled                                             | Clear the in-flight sheet. Keep the last `memberships/me`.                                                                                                 |
| Network or store outage before payment                         | Keep the purchase-intent idempotency key and retry the launch.                                                                                             |
| Evidence not accepted                                          | Keep the secure outbox and retry the same verification idempotency key.                                                                                    |
| App relaunch or new device                                     | Walk unfinished Apple transactions, current entitlements, Play purchases, and the Microsoft collection. Submit missing evidence. Refresh `memberships/me`. |
| Backend delay                                                  | Show the verification `pending` state and honor `next_processing_at`.                                                                                      |
| Refund, revocation, or renewal                                 | Submit the new observation. Render the next `memberships/me` source status.                                                                                |
| Expired Microsoft keys                                         | Mint a new pair and submit a new verification.                                                                                                             |

The last successful `memberships/me` may stay on screen during an outage, labeled as the last
confirmed server snapshot. It is not a newly granted plan.

## Lifecycle UX and accessibility

The visible plan is `membership.plan` when `membership` is non-null. Sources, switches, verifications,
and financial operations explain how that plan was reached. They do not override it.

The lifecycle fixture is the contract example: `membership` is null, a revoked Stripe Plus source is
not effective, an Apple Pro source is active at the store but `is_effective` is false until
`2027-01-01`, one grant is pending, one automatic refund is pending, a Play verification is
`conflict` with `competing_direct_source`, and a Microsoft verification is `rejected` with
`wrong_account`. The screen shows free or the last effective plan, the future switch, the pending
refund, and the two verification outcomes together.

| Screen state                                 | Contract source                                                                                                                       |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Free                                         | `native.memberships.me.default.json`: `membership`, `management`, and `sources` empty                                                 |
| Catalog and store price                      | `native.memberships.plans.default.json` plus the store price API                                                                      |
| Purchase in flight                           | Local, after `201` and before the store result                                                                                        |
| Store pending                                | Apple `.pending`, or an unexpected Play `PurchaseState.PENDING`                                                                       |
| Verification pending                         | `verification-status.pending` and the `202` verification body, `status: pending`                                                      |
| Verified, waiting for projection             | `verification-status.verified`, `reason_code: verified`, then refresh `memberships/me`                                                |
| Conflict                                     | `verification-status.conflict`, `reason_code: competing_direct_source`; purchase-intent `409`                                         |
| Rejected                                     | `verification-status.rejected`, `reason_code: wrong_account` in the fixture                                                           |
| Effective plan                               | `membership.status`: `active`, `cancelled`, `expired`, `past_due`, or `paused`                                                        |
| Source revoked, paused, or not yet effective | `sources[]` in `native.memberships.me.lifecycle.default.json`                                                                         |
| Scheduled renewal                            | `sources[].renewal` and `should_auto_renew`                                                                                           |
| Pending grant, switch, or refund             | `pending.grants`, `pending.switches`, `pending.financial_operations`                                                                  |
| Management                                   | `management.destination`: `apple_subscriptions`, `google_play_subscriptions`, `microsoft_services_subscriptions`, or `billing_portal` |

Other documented verification reason codes, from the request contract, use the same rejected or
conflict presentation: `expired`, `invalid_evidence`, `missing_account_token`, `purchase_pending`,
`revoked`, `stale_evidence`, `wrong_application`, `wrong_environment`, and `wrong_product`.
`purchase_pending` stays on the pending screen rather than the rejected screen. Financial operation
kinds are `automatic_refund`, `ineligible_purchase_reversal`, `cancel_source`, and
`collision_resolution`, with status `pending` or `failed`.

Every state has a text label. Color is not the only signal. Purchase, restore, and manage controls
are buttons with localized accessibility names and hints. A disabled control exposes why it is
disabled, including a future `eligible_at`. Status changes use an accessibility announcement. Copy
goes through `UiMessageKey` on Swift and generated MAUI resources on .NET. The apps do not show a
contact-support recovery string.

## Fixture map

| Flow                                         | Fixture path under `api-fixtures/v1/responses/`                                                                                                                                                                                                               | HTTP                                                             |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Plans and provider SKUs                      | `native.memberships.plans.default.json`                                                                                                                                                                                                                       | `GET /api/v1/memberships/plans` `200`                            |
| Empty current membership                     | `native.memberships.me.default.json`                                                                                                                                                                                                                          | `GET /api/v1/memberships/me` `200`                               |
| Lifecycle, revocation, future switch, refund | `native.memberships.me.lifecycle.default.json`                                                                                                                                                                                                                | `GET /api/v1/memberships/me` `200`                               |
| Apple launch                                 | `native.memberships.purchase-intent.apple.default.json`                                                                                                                                                                                                       | `POST /api/v1/membership-purchase-intents` `201`                 |
| Play launch                                  | `native.memberships.purchase-intent.google.default.json`                                                                                                                                                                                                      | same route `201`                                                 |
| Microsoft launch                             | `native.memberships.purchase-intent.microsoft.default.json`                                                                                                                                                                                                   | same route `201`                                                 |
| Ineligible launch                            | `native.memberships.purchase-intent.conflict.default.json`                                                                                                                                                                                                    | same route `409`                                                 |
| Stripe launch, not used by native purchase   | `native.memberships.purchase-intent.stripe.default.json`                                                                                                                                                                                                      | same route `201`                                                 |
| Accepted evidence                            | `native.memberships.verification.pending.default.json`                                                                                                                                                                                                        | `POST /api/v1/membership-verifications` `202`                    |
| Status pending, verified, conflict, rejected | `native.memberships.verification-status.pending.default.json`, `native.memberships.verification-status.verified.default.json`, `native.memberships.verification-status.conflict.default.json`, `native.memberships.verification-status.rejected.default.json` | `GET /api/v1/membership-verifications/:verificationId` `200`     |
| Microsoft service tickets                    | `native.memberships.microsoft.service-tickets.default.json`                                                                                                                                                                                                   | `POST /api/v1/memberships/microsoft-store/service-tickets` `200` |

The published Apple verification request in `api-fixtures/v1/manifest.json` is
`{ evidence: { signed_transaction }, idempotency_key, provider: apple_app_store, purchase_intent_id }`.
The request schema `MembershipVerificationRequestBody` types `evidence` as an open object. Play and
Microsoft do not have published verification request fixtures. That gap is an open decision.

## Evidence required for #89

Unit tests use fixtures and store doubles. Integration tests run the shared session against a scripted
HTTP stack and the adapter boundary. Provider-sandbox runs are separate and are not implied by a
green unit run.

| Case                  | Unit                                                                                                                               | Integration                                                    | Provider sandbox                                                                  |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Purchase              | Launch payload mapping, `201` decoding, evidence outbox                                                                            | Intent, store double success, `202`, then `memberships/me`     | StoreKit purchase, Play license-tester purchase, Microsoft Store sandbox purchase |
| Restore               | Unfinished Apple transactions, Play `queryPurchasesAsync`, Microsoft collection                                                    | Restore control resubmits only missing evidence                | Sandbox restore on a second install                                               |
| Replacement           | No Play replacement call without a mode; a launched replacement includes `setOldPurchaseToken`; Apple purchases the new product id | `409` does not open a sheet; `201` does                        | Apple subscription-group change and a Play replacement once a mode exists         |
| Pending               | Apple `.pending` submits nothing; an unexpected Play `PENDING` submits and does not become the plan                                | Poll stays on `purchase_pending`                               | Ask to Buy. Prepaid Play plans are out of scope                                   |
| Recovery              | Outbox survives relaunch; Microsoft refresh after `invalid_evidence`                                                               | Backoff and `next_processing_at`                               | Airplane mode, then the same purchase completing                                  |
| Refund and revocation | `sources[].status` `revoked` and `pending.financial_operations`                                                                    | Lifecycle fixture renders refund and revocation together       | Store refund and family-sharing revoke                                            |
| Renewal               | A new transaction id gets a new idempotency key                                                                                    | `should_auto_renew` and `renewal` render from `memberships/me` | Sandbox renewal for each store                                                    |

StoreKit Testing in Xcode, Play test tracks, and the Microsoft Store sandbox are the sandbox tools.
This decision records no sandbox run.

## Implementation tasks for #89

These are ordered. A later task can start only after the tasks it names are merged. Open decisions
gate the tasks that name them. Tasks 1 and 2 are not gated.

1. **Shared read model.** Decode the plans, both `memberships/me` fixtures, and the four verification
   status fixtures in Swift core and .NET core. Render the lifecycle states in the existing native
   shells. No store SDK.
2. **Shared purchase session.** Implement intent, `409`, verification post, polling, replay, and the
   secure outbox behind an adapter interface. Test with the published fixture bodies. The adapter is
   a fake in this task.
3. **Apple adapter, iOS and iPadOS.** StoreKit 2 purchase, restore, updates, family-shared
   submission, and `showManageSubscriptions(in:)`. Gated on open decision 1.
4. **Apple adapter, macOS.** Same StoreKit purchase and restore behavior. Gated on open decisions 1
   and 3. Management UI is gated on open decision 4.
5. **Play adapter.** Billing Library 9.1.0 in the Android app: products, purchase, unexpected
   pending without `enablePrepaidPlans()`, acknowledgement visibility, query-based restore, and the
   subscriptions deep link. The Play submission uses target API 36 unless a granted extension is in
   force. Replacement launch is gated on open decisions 2 and 5.
6. **Microsoft adapter.** Windows target only: service tickets, both Store ID keys, purchase,
   collection restore, and credential refresh. Gated on open decisions 2 and 6. The management
   control is gated on open decision 4.
7. **Accessibility pass.** Apply the lifecycle labels and announcements to every shell that the
   preceding tasks enabled.
8. **Provider sandbox.** Record the sandbox rows above as separate evidence. This task does not
   block the design, and unit tests do not close it.

#89 stays open while any open decision below remains. Tasks 1 and 2 can proceed.

## Open decisions for the user

### 1. Apple evidence field name

The published verification request uses `evidence.signed_transaction`. The Apple verifier in the same
Vouchington commit accepts only `evidence.signed_transaction_info`
(`backend/services/memberships/apple/types.mts`). StoreKit's signed value is
`VerificationResult.jwsRepresentation`.

- Option A: change the fixture to `signed_transaction_info` and have the client send that.
- Option B: change the verifier to accept `signed_transaction`.
- Option C: accept both keys on the server.

Recommendation: option A. One key, matching the verifier that already exists, with the fixture updated
in Filaments before #89 submits Apple evidence.

### 2. Play and Microsoft verification request fixtures

Play evidence in the server parser is `purchase_token`. Microsoft evidence is
`collections_store_id_key`, `purchase_store_id_key`, `publisher_user_id`, `product_id`, and `sku_id`,
with optional recovery ids. No native fixture publishes those request bodies. The OpenAPI evidence
schema is an open object.

- Option A: Filaments publishes those request fixtures, matching the parsers, before the adapters
  submit evidence.
- Option B: #89 treats the server parsers as the contract without new fixtures.

Recommendation: option A. The native contract is the fixture set, not an untyped object.

### 3. macOS App Store application id

The macOS bundle id is `ai.voucha.macos`. The plans fixture has an `apple_app_store` row only for
`ai.voucha.ios`.

- Option A: add an `ai.voucha.macos` provider row, with its own product ids, before macOS can purchase.
- Option B: put macOS and iOS on one bundle id and one row.
- Option C: leave macOS as status and plan presentation until a later release.

Recommendation: option A. The repository already ships two bundle ids. macOS must not submit
transactions under the iOS application id.

### 4. macOS and Windows subscription management

iOS can present `AppStore.showManageSubscriptions(in:)`. Play can open the documented subscriptions
deep link. Apple documents no macOS management sheet. Microsoft documents customer cancellation at
`https://account.microsoft.com/services` and does not provide a `StoreContext` cancel method. The web
client uses `https://apps.apple.com/account/subscriptions` and `https://account.microsoft.com/services`.

- Option A: the OS opens those two HTTPS pages in the system handler. The app does not embed them.
- Option B: macOS and Windows show status and `eligible_at` only, with no external page.
- Option C: embed the pages in a WebView.

Recommendation: option A. These are the provider account pages, not checkout. Option C conflicts with
the no-WebView invariant.

### 5. Play replacement mode

Filaments asks for prorated Play upgrades and deferred downgrades. The purchase-intent launch payload
has no replacement mode. Library 9 expresses the choice on `SubscriptionProductReplacementParams`, and
its constants differ from the deprecated `SubscriptionUpdateParams` values.

- Option A: Filaments adds an explicit mode to the Play launch payload. The client sets
  `setReplacementMode` only from that field, `setOldProductId` from the owned Play product, and
  `setOldPurchaseToken` from that purchase's token returned by `queryPurchasesAsync`.
- Option B: the client ranks Plus and Pro itself and chooses `WITH_TIME_PRORATION` or `DEFERRED`.
- Option C: v1 Play purchases never replace an owned subscription.

Recommendation: option A, with `with_time_proration` for an upgrade and `deferred` for a downgrade.
The server already decides eligibility. The client should not rank plans.

### 6. Microsoft purchase Store ID

`RequestPurchaseAsync(string)` takes a Store ID. The sample launch `product_id` looks like a Store
ID and `sku_id` looks like a Partner Center SKU id. Both must be available to the server evidence
submission.

- Option A: purchase `launch.product_id` with `RequestPurchaseAsync`, and pass `sku_id` through to
  evidence unchanged. One catalog row per purchasable SKU.
- Option B: load the product, find a `StoreSku` whose id equals `sku_id`, and call
  `StoreSku.RequestPurchaseAsync()`.

Recommendation: option A, unless Filaments states that `sku_id` is itself a Store ID. The client does
not guess a SKU match that the fixture does not define.

## Sources

- [StoreKit `Product`](https://developer.apple.com/documentation/storekit/product)
- [StoreKit `Transaction`](https://developer.apple.com/documentation/storekit/transaction), including
  `updates`, `unfinished`, `currentEntitlements`, `jwsRepresentation`, and `finish()`
- [`AppStore.sync()`](<https://developer.apple.com/documentation/storekit/appstore/sync()>)
- [`AppStore.showManageSubscriptions(in:)`](<https://developer.apple.com/documentation/storekit/appstore/showmanagesubscriptions(in:)>)
- [Family Sharing](https://developer.apple.com/documentation/storekit/supporting-family-sharing-in-your-app)
- [Play Billing integration](https://developer.android.com/google/play/billing/integrate) and
  [Library 9.1.0](https://developer.android.com/google/play/billing/release-notes)
- [Play subscriptions](https://developer.android.com/google/play/billing/subscriptions), including
  prepaid pending purchases, `linkedPurchaseToken`, and the subscriptions deep link
- [Play target API level requirement](https://developer.android.com/google/play/requirements/target-sdk)
- [`PendingPurchasesParams.Builder`](https://developer.android.com/reference/com/android/billingclient/api/PendingPurchasesParams.Builder)
- [`SubscriptionProductReplacementParams`](https://developer.android.com/reference/com/android/billingclient/api/BillingFlowParams.ProductDetailsParams.SubscriptionProductReplacementParams)
- [`StoreContext`](https://learn.microsoft.com/en-us/uwp/api/windows.services.store.storecontext)
- [`GetCustomerCollectionsIdAsync`](https://learn.microsoft.com/en-us/uwp/api/windows.services.store.storecontext.getcustomercollectionsidasync)
  and [`GetCustomerPurchaseIdAsync`](https://learn.microsoft.com/en-us/uwp/api/windows.services.store.storecontext.getcustomerpurchaseidasync)
- [`StorePurchaseStatus`](https://learn.microsoft.com/en-us/uwp/api/windows.services.store.storepurchasestatus)
- [Display WinRT UI objects that depend on CoreWindow](https://learn.microsoft.com/en-us/windows/apps/develop/ui-input/display-ui-objects)
- [Microsoft subscription add-ons](https://learn.microsoft.com/en-us/windows/uwp/monetize/enable-subscription-add-ons-for-your-app)
