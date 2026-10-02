# Quest managed access and release integration

Status: attestation and account-link backend implemented, **disabled and not deployed**. This does
not make Google sign-in or managed Gemini usable on Quest yet. No production
credentials, Meta app, signing key or headset operation was created by this work.

## Why there is platform work despite one Maestro account

The original phone app uses Capacitor Firebase Authentication and App Check.
Quest's book is an Android WebView loading the original app from
`https://appassets.androidplatform.net`, without Capacitor. Quest now selects a Firebase JS CustomProvider backed by native Meta verification.
It never falls back to web reCAPTCHA, Play Integrity or debug tokens. Embedded
Google popup sign-in is explicitly refused while browser account linking remains
unfinished. The book keeps multiple windows disabled and main-frame
navigation restricted to its local origin.

Google disallows OAuth authorization in embedded user agents. The account
integration uses a real browser (on the headset or an existing phone/PC),
with a short-lived, explicit device-link approval. It must preserve the same
Firebase user, managed account, ledger and Gemini provider. It must not loosen
WebView navigation to host the Google login. [Google native-app OAuth guidance](https://developers.google.com/identity/protocols/oauth2/native-app).

## Implemented server boundary

The separate `questAttestation` HTTPS function accepts `POST /challenge` with `{}`
and `POST /exchange` with `{ "nonce": "…", "token": "…" }`. Its only result is a
Firebase App Check token. It cannot log in a user, create an account, change an
entitlement, reserve credits or call Gemini.

Meta documents server verification at `platform_integrity/verify`, returning a
`data` entry with `message: "success"` and base64url `claims`. The implementation
validates that server response, never claims decoded from a client JWT. It checks
nonce, freshness, package, signing certificate, minimum version, Store recognition,
advanced device integrity and any reported ban. New unrelated claims are allowed.
Basic integrity is rejected because it can include failed system-integrity checks.
[Meta attestation API](https://developers.meta.com/vr/documentation/android-apps/ps-attestation-api/).

The backend issues a random five-minute challenge. A Firestore transaction consumes
it once, **before** provider verification. Concurrent exchange, invalid proof,
provider failure or a lost reply requires a fresh challenge. Expiry is enforced
by timestamps independently of eventual database TTL cleanup. Bootstrap records contain nonce/address hashes, short-lived timing and counters;
Meta tokens, secrets and Meta device identifiers are not stored.

After validation, Firebase Admin mints an App Check token for the configured app,
valid for 30 minutes. The existing managed API still requires its usual Firebase
identity and App Check. This follows Firebase's custom-provider pattern; local
mocks are not evidence that the real provider or signing permissions work.
[Firebase custom App Check providers](https://firebase.google.com/docs/app-check/custom-provider).

The endpoint has a bounded JSON body, provider response and timeout, refuses
redirects and compressed request bodies, and returns no-store responses. Errors
must not expose upstream URLs containing the Meta secret. Its Meta secret is
bound separately from the original `api` function's secrets.

Bootstrap throttles are transactional and fail closed on storage errors: ten
challenges and thirty exchanges per minute per ingress-derived address. Missing
nonces consume exchange allowance too. This is a modest admission limit, not DDoS
protection or a per-person identity. NAT users share it. Production load/ingress
verification and suitable edge protection remain release gates.

## Configuration before enablement

Use `functions/.env.example`. All fields must be intentional:

| Setting | Meaning |
| --- | --- |
| `QUEST_ATTESTATION_ENABLED` | Exactly `true` enables the service; default false. |
| `QUEST_META_APP_ID` | Owner-created Meta app ID. |
| `META_QUEST_APP_SECRET` | Server-only Secret Manager binding on `questAttestation`. |
| `QUEST_FIREBASE_APP_ID` | Registered Firebase app used by the Quest custom provider. |
| `QUEST_ANDROID_PACKAGE` | Final release package; development package is refused. |
| `QUEST_SIGNING_SHA256` | Allowlisted actual Store signing certificate hashes. |
| `QUEST_MINIMUM_VERSION_CODE` | Lowest admitted Android version code. |
| `QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS` | Verified ingress addresses/ranges for every exposed endpoint. |

Do not guess trusted proxy ranges. Express walks only configured trusted hops;
client-prepended forwarding headers must not change the rate-limit subject.
An absent/invalid range list prevents enablement. Test the direct Functions URL,
any Cloud Run URL and any hosting rewrite before enabling public access. A
misconfigured narrow list can make callers share a proxy's rate bucket; a broad
list can permit spoofing. [Express proxy configuration](https://expressjs.com/en/guide/behind-proxies.html).

Deploy server-only Firestore rules and the two `purgeAt` TTL policies together
when deployment is approved. Expiration remains safe while TTL deletion is delayed.
Do not remove managed API App Check enforcement to make development APKs pass.
Store recognition is required, so sideloaded debug APKs are not production proofs.
No production deployment is authorized by this document.

## Native and shared-app implementation

Meta Platform SDK is pinned to **207.0.0**, matching Core/MRUK. On Android,
`QuestPlatformAccess` initializes it before scene load, checks entitlement, and
requires initialization plus entitlement to finish within eight seconds. A
non-development build exits after failure/missing configuration. The explicit
`MAESTRO_QUEST_DEVELOPMENT` build may run without Meta configuration, but cannot
produce a verified proof. The Editor never calls the native SDK. Actual startup,
offline entitlement behavior and failure presentation still need headset QA.
[Meta entitlement guidance](https://developers.meta.com/vr/documentation/unity/ps-entitlement-check/).

Public native settings are in `Assets/Maestro/Resources/QuestPlatform.json`:
version 1, `enabled: false`, owner-provided `appId: "1763835394893209"`.
The owner selected `com.maestro.quest` for release, subject to Store availability;
development remains `com.maestro.quest.development`. No dashboard changes or
uploads were performed. The web build additionally needs `VITE_QUEST_ATTESTATION_URL` pointing
to the separately deployed function. `QUEST_FIREBASE_APP_ID` must match the
`VITE_QUEST_FIREBASE_APP_ID` embedded in that Quest build; its Firebase project must be the
same account/backend project. No Meta app secret enters Unity or the web bundle.

The top-level book snapshot carries only a session ID, request number and server
nonce. Unity polls it, gets the native proof and returns quoted JSON through a
dedicated receiver on that document. No JavaScript-to-native object is exposed
to artifacts/iframes. Polling repeats a result until the book consumes it without
requesting another proof. Page replacement, suspension and teardown reject stale
responses; the source JWT is not part of room/agent observations or saved data.

The CustomProvider gets a fresh challenge, awaits native verification and performs
one exchange. Its network work and pending request abort when suspended or when
the owning book is replaced. Even a transport ignoring abort cannot complete an
old request. It bounds responses and token lifetime, and counts network delay
against expiry. Existing phone and ordinary web providers are unchanged.

## Remaining implementation and acceptance

1. Finish the browser approval page and book pairing UI/client, including lifecycle
   cancellation and Firebase custom-token sign-in. The backend protocol below is
   implemented but disabled; linking must not substitute for app integrity.
2. Restore/sign out the same Maestro identity and verify ordinary managed chat,
   Live, app-owned agent handoff, cancellation and usage accounting on Quest.
3. Verify provider responses, signing-certificate rotation, actual Firebase mint
   permissions, Store installation and tampered/expired/replayed proof rejection.
   Include shared-network and forwarding-header tests against real ingress.

## Other current release findings

The development package targets API 34 with minimum API 32. Current Quest
manifest guidance supports that selection; Google Play's target policy should
not be substituted for Meta's. [Meta manifest guidance](https://developers.meta.com/vr/resources/publish-mobile-manifest/).

Meta's payment rules include an exception for windows into an existing service.
Maestro's existing subscription may fit that category, but this is an inference,
not approval of a purchase UI. Confirm the intended Quest purchase flow before
exposing checkout. [Meta app policies](https://developers.meta.com/vr/policy/app-policies/).

## Verification for this checkpoint

`npm --prefix functions test` covers configuration, strict proof validation,
provider transport, disabled/failing services, HTTP boundaries and proxy trust.
`npm --prefix functions run test:emulator` now additionally exercises real Firestore
transactions for concurrent claims, replay, expiry and committed rate limits.
Web tests cover the actual book request client and token exchange pipeline with
synthetic provider replies, plus CustomProvider selection and the embedded OAuth
guard. Unity tests cover initialization, entitlement, deadlines, callback ownership
and repeated polling. Android tests verify bounded, quoted result delivery.

Provider/mint responses in local tests are synthetic. No real Meta/Firebase token
exchange, browser linking or Quest acceptance is claimed.


## Account-link backend protocol

The separate `questAccountLink` function is disabled unless
`QUEST_ACCOUNT_LINK_ENABLED=true`. It also requires enabled attestation, verified
proxy CIDRs, distinct same-project `QUEST_FIREBASE_APP_ID` and
`QUEST_WEB_FIREBASE_APP_ID`, and an HTTPS `QUEST_ACCOUNT_LINK_VERIFY_URL` whose
path is `/quest-link.html`. These are configuration gates, not authorization to
deploy. The frontend approval page and book pairing UI are still pending.

Every request requires App Check verification even when the ordinary managed
API rollback setting disables enforcement. Creation, status, cancellation and
redemption accept only the dedicated Quest `appId`; approval accepts only the
original web `appId`. The Quest registration must exclusively use the Meta custom
provider, without reCAPTCHA, debug or Play Integrity providers. The book uses a
named Firebase app and refuses a missing/shared registration; identity and ledgers
stay in the original Firebase project.

| POST route | Exact JSON body | Result and guard |
| --- | --- | --- |
| `/create` | `{}` | Ten-character code, 256-bit device secret, five-minute expiry, fixed verification URL, five-second recommended poll interval. |
| `/status` | `code`, `deviceSecret` | Pending/approved and expiry only; no account identity or credential. |
| `/approve` | `code`, `confirm: true` | Requires configured browser Origin and revoked-token-checked Firebase Google sign-in within five minutes. UID comes only from the verified token. |
| `/redeem` | `code`, `deviceSecret` | Atomically consumes approval before minting a custom token for the existing UID. |
| `/cancel` | `code`, `deviceSecret` | Invalidates pending/approved link and removes UID/secret hash. |

The public code carries 50 random bits. The secret never belongs in the approval
URL, browser page, agent observations, saved room state or logs. Firestore stores
hashes of code and secret. Approval/claim transactions check account-deletion
state; account deletion removes outstanding approvals. Consumed/cancelled rows
retain only timestamps/state until TTL deletion. Expiry is checked independently
of TTL cleanup. Browser approval cannot be overwritten by a competing account.

Committed per-minute limits count malformed/authentication failures too: 12 per
operation per ingress IP, 120 status polls, and six approvals per authenticated
UID. IP and UID rate subjects are hashed. NAT users share IP limits. Storage
failures deny access. Request bodies are capped at 2 KiB including Functions'
preparsed buffers; provider/storage error details never reach HTTP responses.

Only one custom token is **issued** per pairing approval; Firebase's custom token
itself has its own one-hour validity. Signing/delivery failure requires a new
pairing, with no cached credential or retry mint. The client must discard late
results after cancel, sign-out or book replacement. Firebase IAM signing
permissions and real custom-token sign-in still need acceptance testing.
[Firebase custom-token authentication](https://firebase.google.com/docs/auth/admin/create-custom-tokens).

## Meta dashboard / DUC scope recorded on 2026-10-02

Owner-provided public Meta app ID: `1763835394893209`. App secret and `OC|...`
credentials remain server-only and must not be pasted into chat or committed.
The package `com.maestro.quest` is provisional until Meta validates availability;
a visible Store title is independent of the Android identifier.
[Meta package requirements](https://developers.meta.com/vr/resources/publish-mobile-manifest/).

The implementation calls SDK initialization, local entitlement and integrity-token
APIs. It does not request Meta User ID/profile, Meta Avatars, Meta subscriptions,
platform rooms/friends or Meta VoIP. Our imported models, scanned rooms, Gemini
speech and existing Maestro subscription do not themselves use those APIs.
Based on this implementation and Meta's DUC table, none of those optional features
should be requested solely to unblock the dashboard's User ID/federated-ID panel.
This is the current integration assessment; verify against any actual provider
rejection and the final release scope before submitting certifications.
[Meta DUC guidance](https://developers.meta.com/vr/resources/publish-data-use/).

Device Ban is a separately reviewed optional feature; our app never calls the
ban APIs or retains attestation device identifiers. Requested audience/age category
and the final purchase flow remain release decisions and could change the required
features. Do not certify unimplemented data uses.
[Meta attestation and Device Ban](https://developers.meta.com/vr/documentation/android-apps/ps-attestation-api/).
