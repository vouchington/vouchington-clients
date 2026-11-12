# Fresh account confirmation

Fresh confirmation authorizes a protected settings change without signing the user into a new
session. The producer owns proof issuance, ownership checks, expiration, one-use consumption,
and revocation of other sessions after a committed change.

## Development contract

This change develops against producer [#5650](https://github.com/vouchington/vouchington/pull/5650)
at `228fcbb0f28a260fc98adf18217cef9b436cd04a`, supplied through `VOUCHA_CONTRACT_ROOT` and
`pnpm run contracts:pull`. The tracked `contracts/vouchington.json` pin remains unchanged while
that producer PR is unmerged. Before this client change can graduate from draft, merge the
producer and bump the pin to its merged commit, then regenerate and verify the contract.

The producer claims fifteen existing re-authentication messages for each native client. Method
selection, email/TOTP input, send/resend, verification progress, and failure states use those
generated keys. They are product references, not a registry added to satisfy usage validation.
Generated localization is not committed.

## Settings and proof boundaries

The confirmation component protects the existing email, linked-provider, and Bluesky settings
flows. Adding passkey or authenticator management screens is a separate change. Passkeys and
authenticator codes here are proof methods for an already signed-in account.

| Boundary                        | Required behavior                                                                                                                         |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Available methods               | Use the signed-in account's methods returned by the producer.                                                                             |
| Email                           | Request the `re_auth` purpose and use the ten-character email-code contract. The producer selects the account email destination.          |
| Authenticator                   | Keep six-digit TOTP input separate from email codes.                                                                                      |
| Passkey                         | Obtain a fresh assertion for the producer's account-confirmation challenge; verification returns a proof rather than a new login session. |
| Linked provider                 | Use a fresh nonce-bound SDK assertion or the existing purpose-specific broker handoff allowed by that provider.                           |
| Cancellation or changed context | Discard pending confirmation and do not submit the protected mutation.                                                                    |
| Proof use                       | Carry `re_auth_token` on one pending typed mutation; do not retain a reusable settings authorization.                                     |
| Session assurance               | A confirmation response is not a login or MFA upgrade. Keep the initiating session and its server-issued assurance.                       |

The producer binds each proof to the current user, device and session, with a short expiration.
The clients also guard their pending continuation against account/session/device changes. The
server remains authoritative for expired, replayed, revoked, cross-session or wrong-owner proofs.
Proof tokens and provider assertions do not belong in persistent settings or diagnostic output.

Opening an email sign-in link does not confirm a protected change. Confirmation requires the
user's explicit submission on the purpose-specific flow.

## Provider support and producer gaps

Google confirmation has a tested SDK adapter boundary but fails closed when no adapter is
configured. The current native Google broker login is not a substitute: the producer requires a
fresh nonce-bound SDK assertion for Google re-authentication. Adding Google SDK dependencies and
platform configuration is outside this change, as agreed with the owner.

Apple uses the existing platform assertion boundary with the producer challenge. Other supported
linked providers reuse the existing broker callback protocol and retrieve a confirmation proof;
they do not exchange that result for a new authenticated session.
Apple's SDK request uses SHA-256 of the server challenge nonce; Google SDK assertions use the
raw challenge nonce. Keep these provider-specific rules separate.

The supplied fifteen keys do not include specific last-sign-in-method or final-administrator-MFA
recovery messages. Those producer failures also lack stable machine-readable codes. Clients must
not classify English response text. They use canonical generic failure/retry presentation until
the producer publishes distinct stable codes and appropriate native message claims. A sent-code
message interpolating an email address, and distinct cancellation/expired/replayed/revoked-proof
copy, likewise require producer-owned messages if those distinctions are presented.

## Evidence

Transport and state-machine tests must establish proof attachment, one-use behavior, cancellation,
context changes, unavailable adapters and producer failures. Rendered view tests establish the
method controls and explicit submission states. These tests do not establish native OS prompts,
broker execution with real provider accounts, physical-device behavior or a signed release.
Keep those evidence classes separate in the client parity record.
