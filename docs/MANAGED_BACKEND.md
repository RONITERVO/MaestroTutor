# Managed backend architecture

Maestro supports two access modes:

- BYOK calls Gemini with the user's local API key and does not use managed credits.
- Managed access authenticates with Firebase, enforces App Check, reserves shared
  credits, calls Gemini from Cloud Functions and settles actual provider usage.

Managed credit purchases are Stripe-only. See
[`STRIPE_ONLY_BILLING.md`](./STRIPE_ONLY_BILLING.md) for provider setup and release
proof, and [`HEADLESS_CLIENT.md`](./HEADLESS_CLIENT.md) for the real client harness.

## Runtime layout

```text
React / Android adapter / CLI JSON-RPC
       |
       +-- Core SDK controllers and media streams
       |
       +-- Firebase ID token + App Check token
                    |
              Cloud Functions API
       +------------+-------------+
       |            |             |
   Firestore      Gemini        Stripe
   ledger/files   generate/live checkout/webhook
```

The Core SDK owns business transitions and provider request assembly. React,
Capacitor, browser automation and filesystem profiles are adapters. Headless tests
must call the same controllers/routes; they must not reproduce UI behavior in a
parallel model.

## Public API routes

Unauthenticated but provider-signed:

- `POST /billing/stripe/webhook` — raw-body Stripe signature verification and
  idempotent fulfilment.

Unauthenticated health:

- `GET /health` — Firestore readiness and provider/configuration presence. It does
  not spend money and is not a provider smoke test.

Authenticated + App Check:

- `GET /auth/session`
- `GET /account/summary`
- `GET /account/usage-ledger`
- `GET /account/billing-ledger`
- `POST /account/delete`
- `POST /billing/stripe/checkout`
- `POST /gemini/generate-content`
- `POST /gemini/generate-content-stream`
- `POST /gemini/generate-music`
- `POST /gemini/upload-media`
- `POST /gemini/file-statuses`
- `POST /gemini/delete-file`
- `POST /gemini/clear-files`
- `POST /gemini/live-token`
- `POST /gemini/live-token/release`

App Check required, Firebase Authentication optional:

- `POST /reports/ai-content` — accepts anonymous reports when no bearer token is
  supplied and applies the anonymous rate limit; a valid bearer token associates
  the report with its managed account.

There is no client-triggered purchase fulfilment endpoint. The retired
`/billing/google-play/verify` route must not return.

## Authentication and App Check

`requireAuthContext` verifies Firebase ID tokens and resolves the token subject as
the account id. The caller cannot choose another uid. `REQUIRE_APPCHECK=true`
requires a valid Firebase App Check token for product routes, including anonymous
content reports. The provider-signed Stripe webhook is outside App Check and
trusts only its raw-body signature.

Web attestation uses a domain-restricted reCAPTCHA Enterprise key. Android uses
Play Integrity with registered signing fingerprints. CI uses a staging-only debug
token. A debug token is a secret and does not belong in a committed dotenv file.

Rejections carry a stable `code` beside the human-readable `error`, so a client
can explain the condition instead of showing server prose:

| code | meaning |
| --- | --- |
| `app-check/missing` | the request carried no `X-Firebase-AppCheck` header |
| `app-check/invalid` | the token was present and failed verification |
| `auth/missing-token` | no `Authorization: Bearer` header on a required route |
| `auth/invalid-token` | the Firebase ID token failed verification |

Only errors raised through `createHttpError` carry a code; runtime and library
error codes are never echoed to callers. The client adds `app-check/unavailable`
for its own side of the same condition: the device could not attest at all, so
the request is refused before it is sent.

Rate limits are per uid and operation bucket. Account deletion removes current
rate-limit windows using the same SHA-256 uid/bucket ids as the limiter. Legacy
collections are cleaned only for backward compatibility.

## Billing data and invariants

Authoritative collections use the managed-data helpers in `functions/src/managedData.ts`:

- user root and account summary;
- reservations;
- usage and billing ledgers;
- entitlements;
- managed file records and cleanup jobs;
- Stripe checkout grant snapshots;
- provider-scoped purchase claims;
- deletion claims and reports.

Critical invariants:

1. Stripe creates an immutable grant snapshot when Checkout is created.
2. A signed webhook must describe a paid session matching that snapshot.
3. The claim id is `stripe_` plus SHA-256 of the Checkout session id.
4. Account summary, claim, entitlement and ledger entry are written in one
   Firestore transaction.
5. Duplicate delivery returns success without another grant.
6. Account deletion claims prevent delayed requests from recreating deleted data.
7. Reservations prevent concurrent operations from overspending one balance.
8. Failed/abandoned provider operations release reservations; successful operations
   settle actual usage metadata.

`google-play` may appear only when reading/anonymizing historical draft records.
The v1 Play implementation existed only in code and was never configured or used in
a deployed purchase. It is not an alternate provider.

## Credit catalogue

Functions configuration is one list:

```dotenv
MANAGED_CREDIT_PACKS=pack_1000:1000:299,pack_6000:6000:999
BILLING_CURRENCY=eur
```

Each entry is `id:credits:priceInSmallestCurrencyUnit`. Client configuration lists
the same stable ids:

```dotenv
VITE_MANAGED_CREDIT_PACK_IDS=pack_1000,pack_6000
```

There is no Play product alias. Run `npm run verify:release-config` after changing
the catalogue. Stripe Checkout uses inline price data from the server-side pack;
the client never supplies price or credit quantity.

## Gemini accounting

The backend allowlists configured generation, Live and music models. It estimates
and reserves credits before a paid operation, then settles from provider usage or
the configured Live/music fixed reservation rules. Search usage is included in the
reservation and settlement metadata. A provider failure after partial streaming
must still release or settle deterministically; it must not leave reserved credits.

Uploads reserve an active-file slot and upload cost, wait for Gemini File state
`ACTIVE`, write ownership metadata, then settle. Generation verifies every
referenced file belongs to the caller and is active. Both UI and headless chat/image
paths sanitize expired history URIs through the same Core SDK helper.

Managed Gemini Live uses short-lived backend-minted tokens and leases. Lyria is
different: its music WebSocket does not accept Gemini ephemeral tokens, so
`/gemini/generate-music` opens the provider stream with the server-held key and
returns PCM through the authenticated backend. The shared Core SDK gives the UI
and headless harness the same managed route and stream-observer boundary; BYOK
music remains a direct client connection. Synthetic speech PCM enters after
device capture so CI exercises packetization, speech gating, provider Live
transport and final lease release without a physical microphone.

Every Live token request also carries a validated open reason. Validation happens
before reservation or token minting, and the lease plus billing metadata record
the trigger, origin, request id and timestamp. The reviewed allowlist and extension
procedure are in [`GEMINI_LIVE_OPEN_POLICY.md`](./GEMINI_LIVE_OPEN_POLICY.md).

## Account deletion

Deletion:

- creates a deletion claim first;
- releases and deletes reservations;
- deletes managed user/account/ledger/entitlement/file data;
- requests remote Gemini file cleanup and records bounded retry jobs;
- anonymizes purchase claims and content reports needed for abuse/idempotency;
- deletes live/test Stripe customer profiles when configured;
- removes current rate-limit window ids plus legacy collections defensively;
- deletes the Firebase Authentication user last.

The public delete-account page and in-app panel call the same controller/backend
route. A headless delete additionally requires `DELETE` and an expected disposable
test uid.

## Configuration and secrets

Functions environment (non-secret):

- region, allowed origins, `APP_URL`, currency and catalogue;
- App Check enforcement;
- model allowlists, rate limits, reservation/file/live limits;
- managed credit conversion and music session charge.

Secret Manager:

- `GEMINI_API_KEY`
- `STRIPE_SECRET`
- `STRIPE_WEBHOOK_SECRET`

Client public configuration includes Firebase ids, backend URL, App Check site key,
Google OAuth ids and managed pack ids. Do not embed server secrets in Vite variables;
every `VITE_` value is shipped to the client.

## Local validation

Use Node 22 and JDK 21:

```powershell
npm ci
npm --prefix functions ci
npm run verify:release-config
npm test
npm run lint
npm run build
npm --prefix functions test
npm --prefix functions run test:emulator
```

Functions unit tests cover configuration, CORS, raw webhook body handling, async
Stripe payment fulfilment and account/rate-limit helpers. The emulator test proves
reservation contention, release, Stripe idempotency and deletion-claim blocking.

## Staging validation

The weekly/manual `Headless staging journey` is required provider evidence. It
authenticates with Firebase + App Check, completes a controlled Stripe test-card
purchase, verifies one grant, then runs text, attachments, image, audio note, music and Live
through real managed routes. It clears generated files and signs out.

Do not weaken the workflow to skip paid routes when the balance is zero; the first
step deliberately creates test credits. Do not use production Stripe credentials
or a production identity.

## Deployment order

1. Run all local gates.
2. Add/rotate Secret Manager versions without printing values.
3. Deploy Functions, Firestore rules and indexes.
4. Verify `/health` and webhook delivery.
5. Deploy Hosting/client.
6. Run the staging Stripe-first journey.
7. Require independent review before production.

Deploy staging explicitly:

```powershell
npm run build:staging
npm --prefix functions exec firebase -- deploy --config firebase.json --project staging --only functions,firestore:rules,firestore:indexes,hosting
```

## Failure rules

- Firestore not ready: stop; do not treat configured secrets as readiness.
- App Check failure: fix app/key/domain/fingerprint alignment; do not disable it as
  normal operation.
- Paid Checkout with no credit: repair/redeliver the signed webhook; do not grant
  from the browser.
- Duplicate credit: stop release and preserve claim/session evidence.
- Reserved credits remain after a failed journey: stop release and fix lifecycle
  accounting.
- Android checkout not enrolled: keep the production flag false; users can use a
  balance purchased on the hosted web app.
- Provider model unavailable/high demand: preserve error telemetry and verified
  fallback behavior; do not silently bill a failed primary request.
