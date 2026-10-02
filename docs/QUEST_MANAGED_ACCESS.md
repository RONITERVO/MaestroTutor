# Quest managed access and release integration

Status: backend attestation checkpoint, **disabled and not deployed**. This does
not make Google sign-in or managed Gemini usable on Quest yet. No production
credentials, Meta app, signing key or headset operation was created by this work.

## Why there is platform work despite one Maestro account

The original phone app uses Capacitor Firebase Authentication and App Check.
Quest's book is an Android WebView loading the original app from
`https://appassets.androidplatform.net`, without Capacitor. Current platform
selection therefore reaches `signInWithPopup` and web reCAPTCHA. The book disables
multiple windows and restricts main-frame navigation to its local origin. These
paths are not a completed Quest sign-in integration.

Google disallows OAuth authorization in embedded user agents. The next account
integration should use a real browser (on the headset or an existing phone/PC),
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

## Remaining implementation and acceptance

1. Add Meta Platform initialization/entitlement handling and native attestation,
   bound to the local book session. Cancel stale browser/native responses.
2. Add a Quest-only Firebase JS CustomProvider. Keep phone and ordinary web
   providers unchanged; refresh safely and provide actionable retry states.
3. Implement explicit account-link approval through the existing web account,
   protected by Firebase Auth and App Check, with expiry, cancellation and atomic
   one-time redemption. Linking must not substitute for app integrity.
4. Restore/sign out the same Maestro identity and verify ordinary managed chat,
   Live, app-owned agent handoff, cancellation and usage accounting on Quest.
5. Verify provider responses, signing-certificate rotation, actual Firebase mint
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
Provider/mint responses in local tests are synthetic. No real Meta/Firebase token
exchange, browser linking or Quest acceptance is claimed.
