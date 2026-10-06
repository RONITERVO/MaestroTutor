# Quest managed access and release integration

Status: attestation, account-link backend, browser approval and book sign-in client
implemented. The two Quest endpoints are now **deployed but disabled** following
the owner's scoped approval. The temporary private diagnostic is removed.
Real Meta/Firebase account verification and Quest acceptance remain open; the
existing API and browser pairing page still need their separate release work.

## Scoped backend rollout — 2026-10-06

The owner approved the frozen rollout from `d7bfee10`: two new disabled functions,
a temporary IAM-private diagnostic, and access to one Meta secret. Both new
Node.js 22 functions are active in `europe-west1`, with maximum three instances
and concurrency 20. Their `QUEST_ATTESTATION_ENABLED` and
`QUEST_ACCOUNT_LINK_ENABLED` settings remain `false`:

- `https://europe-west1-chatwithmaestro.cloudfunctions.net/questAttestation`
- `https://europe-west1-chatwithmaestro.cloudfunctions.net/questAccountLink`

All seven operation paths returned their expected unavailable response (HTTP 503,
no tokens, no-store) through both Functions and Cloud Run URLs: **14 checks**.
Deployed source archives exactly match the approved inputs: 198 files for each
Quest function and four for the diagnostic. Disabled handlers reject before the
store, provider and token-mint operations in that verified source.

The owner entered `META_QUEST_APP_SECRET` privately; only enabled-version metadata
was inspected. Only attestation binds version 1. Firebase granted the existing
runtime `secretAccessor` on that one secret. Project and runtime service-account
IAM policies, including their etags, are unchanged. All **10 existing functions**
match the predeployment inventory exactly. No existing API, scheduler, website or
database policy was deployed.

The private diagnostic rejected anonymous access with 403 on both URLs. Two fresh
server-generated **non-token** challenge signatures were independently verified
using the runtime identity's public certificate. This confirms runtime signing
access, not successful Firebase App Check/Auth minting or Meta verification.
The diagnostic was deleted; final inventory contains only the ten original and
two new functions, and both diagnostic URLs return 404.

Forwarding inspection covered normal requests and three attacker-prepended
header variants on each ingress. An exact-address candidate resolved all eight
recorded chains consistently in the locked parser. This is one revision and one
operator network, not a supported ingress-range guarantee or cold-start/network
acceptance. Production trusted ranges remain empty; **do not enable issuance**
until ingress and the remaining release prerequisites are verified.

The ignored release profile now uses URLs read back from the deployed functions
and passes strict configuration validation. This does not establish provider or
Store acceptance. The original API's Quest identity/origin/purchase boundary,
public approval page, database TTL/rules, real Store-channel integrity/entitlement,
account round trip and managed/BYOK behavior remain separate release gates.

Local evidence: `.quest-evidence/account-activation-20261006/verified.json`, the
frozen rollout plan, deployed source archives, metadata/IAM comparisons, private
probe and disabled-response results, and cleanup receipt. No secret payload or
operator bearer token was read into evidence.

## Verified account preparation — 2026-10-06

A dedicated **Maestro Quest** Firebase web registration is now active in the
existing `chatwithmaestro` project:
`1:47084692464:web:0b698a31e7e80cddac7335`. This is the client identity for the
book's JavaScript CustomProvider, not a second account, backend or public website.
The original web registration
`1:47084692464:web:7e83d9b9c36b6c59ac7335` and Android registration
`1:47084692464:android:1f3e885d2a423a63ac7335` read back unchanged.
The new registration has neither reCAPTCHA provider configured and has zero
App Check debug tokens. Registration alone cannot mint a Quest proof.

The ignored release draft now contains the verified original web SDK settings,
its existing reCAPTCHA Enterprise site key, the existing production API URL and
the dedicated Quest app ID. All six shared public settings also match the
currently deployed website bundle. API-key metadata shows no referrer allowlist
on either public browser key and includes the required Firebase APIs; this is
configuration evidence, not a successful Quest authentication request. The read
used [Google's metadata-only endpoint](https://docs.cloud.google.com/api-keys/docs/reference/rest/v2/projects.locations.keys/get).

The pre-rollout inventory established this setup (superseded where noted by the
scoped rollout above):

- `api` is active in `europe-west1`, requires App Check, and does not yet configure
  the Quest app ID or allow the book origin.
- Neither `questAttestation` nor `questAccountLink` is deployed. Their release
  profile URLs stay blank, and strict release validation still refuses the draft.
- `https://chatwithmaestro.com/quest-link.html` returns **404**, while the original
  home page returns 200. Deploy the actual approval page through the website's
  release process; a chosen URL is not a working pairing flow.
- The Meta server secret, token-mint/signing permissions, verified ingress ranges,
  Store-channel integrity/entitlement and real account/provider acceptance remain
  outstanding. No secret, ingress range or working endpoint was inferred.

A public server-settings merge draft is prepared locally with both Quest
bootstrap switches **false**, the exact app/package/certificate identities and
the proposed additional book origin. It is outside `functions/` and has not been
applied. It must preserve existing production settings, including App Check,
when deployment is approved. Keep the Quest ID on the main API after deployment
even if new proof issuance is later disabled, as described below.

The four Quest TTL entries in `firestore.indexes.json` cover
`questAttestationChallenges`, `questAttestationRateWindows`, `questAccountLinks`
and `questAccountLinkRateWindows`. Include all four with the server-only rules;
no database policy was changed during this preparation.

Evidence: `.quest-evidence/account-release-config-20261006/verified.json`, the
before/after app inventories, provider/debug-token readbacks, public SDK/bundle
comparison, HTTP response and disabled server merge draft. The only remote
mutation was creating the dedicated Firebase registration (including its
Firebase-created public browser key). No service, website, provider, permission,
account balance or Store deployment changed.

## Why there is platform work despite one Maestro account

The original phone app uses Capacitor Firebase Authentication and App Check.
Quest's book is an Android WebView loading the original app from
`https://appassets.androidplatform.net`, without Capacitor. Quest now selects a Firebase JS CustomProvider backed by native Meta verification.
It never falls back to web reCAPTCHA, Play Integrity or debug tokens. Embedded
Google popup sign-in is replaced by the browser account-link flow below. The book keeps multiple windows disabled and main-frame
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

Deploy server-only Firestore rules and all four Quest `purgeAt` TTL policies together
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

1. Configure and verify the disabled browser/book flow with real Meta and Firebase
   providers. Linking must not substitute for app integrity.
2. Verify restoring/signing out the same Maestro identity and ordinary managed chat,
   Live, app-owned agent handoff, cancellation and usage accounting on Quest.
3. Verify provider responses, signing-certificate rotation, actual Firebase mint
   permissions, Store installation and tampered/expired/replayed proof rejection.
   Include shared-network and forwarding-header tests against real ingress.

## Other current release findings

The development package targets API 34 with minimum API 32. Current Quest
manifest guidance supports that selection; Google Play's target policy should
not be substituted for Meta's. [Meta manifest guidance](https://developers.meta.com/vr/resources/publish-mobile-manifest/).

Payment-policy audit, 2026-10-04: Meta's existing-service exception has an
interactivity limit. Our substantial room creation and play make eligibility
uncertain; sharing Maestro's existing backend alone does not establish it.
Obtain a decision on the actual product and purchase flow before enabling Quest
commerce. No Meta IAP or subscription implementation is implied by shared credits.
[Meta app policies, sections 1.1 and 4.1](https://developers.meta.com/vr/policy/app-policies/).

The Quest purchase boundary is enforced in three places: the account UI hides
checkout, the browser service refuses direct checkout calls before credentials or
network use, and the managed HTTP route rejects the server-verified Quest Firebase
app ID. The local book origin can also deny a request; it can never authorize one.
Omitting/changing Origin or claiming a different app ID in JSON does not override
the verified proof. The backend refuses before creating a Stripe session/customer.
Original web checkout and Quest account/balance access retain the shared account.
This boundary is not a claim that the remaining access model has Store approval.

Configure `QUEST_FIREBASE_APP_ID` on the **main api function as well as the bootstrap
functions**, with distinct web and Quest registrations. Keep that public ID set
when disabling new attestation or linking: previously minted proofs can still be
valid. While the Quest ID is configured, `REQUIRE_APPCHECK=false` also closes checkout
for every client until verification is restored; it cannot silently weaken this
purchase boundary. Other existing managed-service rollback behavior is unchanged.
The main API purchase-boundary change remains undeployed. The client-service
guard is included in
the audited `049188fd` development APK, `MaestroQuest-scanned-ink-45468330.apk`;
the server guard remains undeployed. See [packaging evidence](QUEST_RELEASE_BUILD.md#development-checkpoint-2026-10-05).

Offline acceptance exercises the real HTTP route with verified-token, Firebase
identity, rate-limit and Stripe/Firestore adapters replaced by local fakes. It
checks absent/forged origins, forged body app IDs, missing/invalid proofs, disabled
issuance, incident rollback, and preserved account/web behavior. It makes no provider
calls and is not proof of real Meta/Firebase configuration or a settled payment.

Local verification for the purchase-boundary increment: 2,359 web tests across
259 files, app lint/type checking, and 96 Functions checks passed (including the
13 HTTP purchase/account cases and their parent test). No native code changed;
Unity/Android/device tests were not rerun for this source-only boundary change.

## Purchase-model decision and prepared Meta request (2026-10-05)

The current implementation sells **prepaid managed-credit packs**, not recurring
subscriptions: `functions/src/stripeBilling.ts` creates Stripe Checkout with
`mode: 'payment'`, and only its verified webhook grants the shared balance.
[Stripe-only billing](STRIPE_ONLY_BILLING.md) is the authoritative contract.
BYOK is a separate existing access mode; native editing and local programs do
not create a second AI connection. A request to Meta must describe these actual
flows rather than calling the product a subscription wrapper.

Meta's published Payments section 1.1.2 describes the existing-service exception
in terms of off-platform subscription content. Section 4.1 also limits the role
of interaction. Room creation, physics, playable objects and delegated AI room
actions mean eligibility cannot be inferred from using the original Maestro
backend or hiding checkout. This is an unresolved eligibility assessment, not a
claim that Meta has rejected or approved this product.
[Policy checked 2026-10-05](https://developers.meta.com/vr/policy/app-policies/).

Recommended next step: obtain a written answer describing both shared prepaid
credits and BYOK before changing the billing architecture. The owner has been
asked whether to pursue that route first or plan Meta purchases credited to the
same Maestro account. Neither choice is recorded yet. Adding a Meta grant path
would be a new reviewed architecture decision under the existing billing
contract; it must retain the original account and exactly-once ledger semantics.
No purchase route has been added or enabled by this review.

### Draft for the owner to review and send

Subject: Quest app 1763835394893209 — existing Maestro credits and BYOK access

We are preparing Maestro for Quest, for an adult 18+ audience. It uses the same
Maestro account and AI service as our existing web/phone app. The familiar chat
and artifacts are displayed on an animated book in mixed reality. Users can also
create/import objects and avatars, draw, animate, build playable structures and
ask Maestro's AI agent to perform room actions. These are substantial interactive
features, not only a viewing environment.

Our implemented managed-access model uses prepaid credit packs purchased through
Stripe in the existing web service. These are one-time purchases, not recurring
subscriptions. The proposed Quest experience uses the same account balance for
AI requests, including room-agent requests. Users may alternatively supply their
own Gemini API key. Native room editing and local programs do not purchase or
consume managed AI credits by themselves.

The Quest UI has no credit purchase button or checkout link. The client and
prepared server code also refuse Quest Stripe-checkout requests. The original
web purchase flow remains separate. There is no Meta IAP integration yet, and
we are not assuming that the existing-service exception applies.

Please confirm:

1. May this interactive Quest app use the user's existing shared prepaid Maestro
   balance and BYOK access as described, without offering purchases in Quest?
   If written approval or an exception is required, what review should we submit?
2. If Meta purchases are required, may their credit grants feed the same Maestro
   account and balance used on web/phone? What restrictions apply to consuming
   credits originally purchased outside Quest and to the BYOK alternative?
3. Which payment configuration and platform-feature approvals are required for
   the permitted model, so our submission and Data Use Checkup describe its
   actual implementation?

Public Meta app ID: `1763835394893209`. Proposed Android package:
`com.maestro.quest` (availability still to be confirmed). We can provide a current
build and a recording of these flows for review when requested.

### Evidence and release boundary

This is a prepared draft, **not a sent request or approval**. Do not send keys,
signing material, private chats or account identifiers with it. Record Meta's
case/reference, exact reviewed product scope and written outcome here when
available. A generic payments FAQ or approval of a less interactive app does not
close this gate. Then implement any required changes, verify the real account/
payment flow and update the release profile and disclosures before submission.

## Verification for this checkpoint

`npm --prefix functions test` covers configuration, strict proof validation,
provider transport, disabled/failing services, HTTP boundaries and proxy trust.
`npm --prefix functions run test:emulator` now additionally exercises real Firestore
transactions for concurrent claims, replay, expiry and committed rate limits.
Web tests cover the actual book request client and token exchange pipeline with
synthetic provider replies, CustomProvider selection, browser confirmation,
account switching, cancellation, crash recovery and the unpublished account
handshake. `node scripts/run-quest-account-ui.mjs` exercises the real React
components through a development-only synthetic adapter at phone and book sizes;
it fails on external network requests. It requires Vite on localhost:5182. Unity tests cover initialization, entitlement, deadlines, callback ownership
and repeated polling. Android tests verify bounded, quoted result delivery.

Provider/mint responses in local tests are synthetic. No real Meta/Firebase token
exchange, browser linking or Quest acceptance is claimed.


## Browser approval and book identity lifecycle

The original account dialog now starts pairing on native Quest. It shows only the
public code, a countdown and the fixed `https://chatwithmaestro.com/quest-link.html`
address; the 256-bit device secret and tokens remain private to the request client.
The native host opens that exact credential-free URL in the system browser only
after a main-frame user gesture. Queries, fragments, other hosts and custom
schemes cannot launch through this path. Phone/PC browsers can use the same page.
The native queue is cleared on suspension/page replacement. WebView navigation,
multiple-window restrictions and the original page surface remain unchanged.

The standalone page uses the existing Google/Firebase browser identity and web
App Check registration. It requires manually entering the book code, displays the
account and requires explicit confirmation before approval. Codes in URLs never
autofill or autoapprove. Editing the code or changing accounts clears confirmation.
The approval checks the currently signed-in UID still matches the displayed
account; only the verified bearer token, code and confirmation go to the server.
An expired recent-login requirement offers Google sign-in again. Account restore
and approval have bounded deadlines; late responses cannot update a closed page.

The book polls every five seconds without overlapping requests. It pauses while
suspended and resumes the same unexpired code after browser approval. A book
replacement, Cancel or closing the account dialog cancels locally immediately and
attempts one bounded server cancellation. Expiry is rechecked against wall time.
Interrupted creation or redemption requires a fresh link; redemption is never
retried because a lost response may already have consumed its one issuance.

Firebase custom-token sign-in stays on the named Quest app in the original
Firebase project. One attempt owns the complete transaction through persistence
and the shared `/auth/session` handshake. Ordinary identity reads refuse an
uncommitted user; the handshake sends the newly approved token directly and
verifies the returned UID before publishing account/balance state. A non-secret
pending marker, Firebase's blocking auth-state hook and rollback cover cancellation
or process death during the SDK's non-abortable persistence window. On restart,
a marker clears the incomplete SDK identity and cached managed session before
restoration. Failed cleanup retains the marker and refuses successful sign-in.
This is client crash/cancellation recovery, not revocation of the already issued
Firebase custom token; the server issuance/lifetime distinction below still applies.

Set both public build values intentionally: `VITE_QUEST_ACCOUNT_LINK_URL` points
to the deployed function; `VITE_QUEST_ACCOUNT_LINK_VERIFY_URL` must match the
server configuration and the native allowlisted public URL. The page also checks
its exact top-level origin/path before offering approval. Endpoint fields stay
blank until enablement; localhost fixtures cannot bypass production approval.
Quest currently shows existing balance and BYOK, without a checkout button or
Stripe purchase note. Phone and ordinary web payment/sign-in paths are retained.

## Account-link backend protocol

The separate `questAccountLink` function is disabled unless
`QUEST_ACCOUNT_LINK_ENABLED=true`. It also requires enabled attestation, verified
proxy CIDRs, distinct same-project `QUEST_FIREBASE_APP_ID` and
`QUEST_WEB_FIREBASE_APP_ID`, and an HTTPS `QUEST_ACCOUNT_LINK_VERIFY_URL` whose
path is `/quest-link.html`. These are configuration gates, not authorization to
deploy. The browser approval page and book pairing client use this same protocol.

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
ban APIs or retains attestation device identifiers. The final purchase flow and any future audience change could change the required
features. Do not certify unimplemented data uses.
[Meta attestation and Device Ban](https://developers.meta.com/vr/documentation/android-apps/ps-attestation-api/).


## Quest v1 adult audience decision (2026-10-02)

The owner chose **adults 18+ for Quest v1**. `QuestAudienceGate` blocks mounting
both the functional book surface and the original App until an unchecked adult-confirmation
checkbox and a separate Continue action are completed. Declining leaves chat
inaccessible. A new book document asks again; no date of birth or persisted age
record is collected. Ordinary phone/web entry is unchanged. Native manual room
editing is separate; this is an AI/book gate, not verified-age enforcement across
the whole platform. Existing room programs are not a separate Gemini client.

[Gemini's current terms](https://ai.google.dev/gemini-api/terms) prohibit API clients
directed toward or likely accessed by under-18s. A checkbox alone does not prove
eligibility: adult positioning, distribution controls and final onboarding need
release review. These terms also require Paid Services for clients made available
in the EEA, Switzerland or UK. Validate the managed project and eligible BYOK
configuration for launch; this UI does not verify billing status or location.

Meta's `TEENS_AND_ADULTS` self-certification covers **13+**, not our stricter 18+
audience. IARC content ratings are separate; do not misstate the content to obtain
an 18 rating. Review the actual dashboard distribution controls and certify the
truthful category before release. No Meta User Age Group API has been added or
DUC submitted as part of this change.
[Meta age-group guidance](https://developers.meta.com/vr/resources/age-groups/).

See [Quest privacy data map](QUEST_PRIVACY.md) for actual retention and remaining
release verification. Public policy changes are prepared in `public/privacy.html`;
they have not been deployed by this checkpoint.
