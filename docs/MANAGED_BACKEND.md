# Managed mode: architecture and runbook

For the live provider inventory, beginner release steps, credential rotation,
monitoring and rollback procedures, use
[PRODUCTION_OPERATIONS.md](./PRODUCTION_OPERATIONS.md). This document explains
the design and data invariants.

Managed mode lets someone use Maestro Tutor without supplying a Gemini API key.
They sign in, buy credits — through Google Play in the Android app, or Stripe
Checkout on the web — and the backend proxies Gemini on their behalf, charging
credits against real usage.

The source template ships **dark**: `VITE_MANAGED_MODE_ENABLED` is `false` by
default. The production web build has been deliberately enabled after the live
backend and App Check verification described in the operations runbook.

---

## 1. Why it is shaped this way

The user's own API key never leaves their device today, and that is the whole
security story. Managed mode breaks that premise: the service now holds a key,
spends money on the user's behalf, and has to be right about the amount. That
changes what "careful" means, so three properties drive the design.

**The client is never trusted about money.** It may ask for work and report what
it thinks happened, but every charge is computed server-side from the usage
Gemini reports, and every purchase is verified with the storefront that took the
money rather than believed.

**Charges are bounded before they happen.** Credits are reserved up front and
settled afterwards, so a request cannot run without the balance to pay for it.
Reservations expire, so a crashed request cannot strand a balance.

**The same numbers are shown and charged.** The app already displayed cost
estimates for BYOK usage. Managed mode charges for the same requests, so a
second pricing implementation would eventually disagree with the first and the
user would be overcharged or the service would lose money. There is one.

---

## 2. Shape

```text
  Android ──Play Billing──▶ api ──verify──▶ Play ──▶ grant ──▶ consume
  Web     ──Checkout────▶ Stripe ──webhook──▶ api ──▶ grant
                                                      │
                                        one credit balance, either way

  app  ──HTTPS──▶  api (Cloud Function, Express)
                     │
                     ├── auth: Firebase ID token, optional App Check
                     ├── rate limit: per user, per bucket (Firestore)
                     ├── reserve credits ──▶ Firestore transaction
                     ├── call Gemini with the service key
                     └── settle credits ──▶ Firestore transaction + ledgers

  Play ──verify──▶  api  ──grant──▶  credits  ──consume──▶ Play
```

Firestore holds the balance, reservations, ledgers and purchase claims. Rules
deny everything by default: a signed-in user may **read** their own account
summary, ledgers and entitlements. Every write goes through the Admin SDK,
which bypasses rules, so there is no path for a client to write its own balance.

The v2 layout deliberately follows ownership and transaction boundaries:

```text
users/{uid}                         lifecycle + schema metadata only
  managedAccounts/default          money only
    entitlements/{claimId}
    reservations/{reservationId}
    billingEvents/{eventId}
    usageEvents/{eventId}
  runtime/fileQuota                upload counter only
  runtime/liveQuota                live leases only
  files/{fileId}
  liveLeases/{leaseId}

purchaseClaims/{provider}_{hash}   global idempotency claim
checkoutGrants/{stripeSessionId}   immutable fulfilment input
reports/{reportId}
rateLimitWindows/{subjectBucketHash}
cleanupJobs/{remoteFileHash}
accountDeletionClaims/{uidHash}    permanent deletion tombstone
```

Billing, file uploads and live sockets do not update one shared summary
document. Purchase claim IDs are provider-scoped hashes; raw Play tokens and
Stripe session IDs are not put in document paths or returned in entitlements.
The global collections are small operational indexes/claims, not second
canonical copies of user data. Recursive deletion of `users/{uid}` therefore
removes all canonical user-owned records.

### Where the money logic lives

| Concern | Location | Tested |
|---|---|---|
| Rate card, model matching, usage → USD | `shared/pricing/` | yes |
| Credits, reservation estimates | `shared/pricing/credits.ts` | yes |
| Balance arithmetic and its invariants | `shared/billing/ledger.ts` | yes |
| Reconnect pacing | `shared/reconnect/policy.ts` | yes |
| Firestore persistence of the above | `functions/src/managedBilling.ts` | core concurrency/idempotency path in emulator |
| Play verification | `functions/src/playBilling.ts` | no — needs a real purchase |
| Stripe fulfilment rules | `shared/billing/stripeFulfilment.ts` | yes |
| Stripe checkout and webhook | `functions/src/stripeBilling.ts` | fulfilment and delayed-payment regression tests; a real purchase is still required |

`shared/` is compiled into the functions bundle (see `functions/tsconfig.json`,
which roots at the repo so `shared/` is emitted alongside `functions/src`). The
app imports the same files directly.

---

## 3. Deploying

### Once per project

1. Create the Firebase project; enable Firestore and Google sign-in.
2. Enable `androidpublisher.googleapis.com`, then invite the functions runtime
   service account from Play Console's **Users and permissions** page. Scope it
   to the Maestro app and grant **View financial data** plus **Manage orders and
   subscriptions**. Google no longer requires linking the Play developer
   account to a Cloud project; purchase verification fails without the API and
   Play permissions.
3. Configure App Check for web and Android. Leave `REQUIRE_APPCHECK=false`
   until both are verified, then turn it on — it is the main defence against
   someone driving the backend outside the app. Production enforcement is on;
   use the rollback procedure in `PRODUCTION_OPERATIONS.md` for an outage.
4. Define the credit packs once in `MANAGED_CREDIT_PACKS` as
   `id:credits:cents[:playProductId]`. Create the matching consumable products
   in Play for any pack that names a `playProductId`.
5. Store `GEMINI_API_KEY`, `STRIPE_SECRET` and `STRIPE_WEBHOOK_SECRET` in
   Google Secret Manager with `firebase functions:secrets:set`. For web
   payments, set `APP_URL` in `functions/.env` and add a webhook
   endpoint in the Stripe dashboard pointing at
   `<api>/billing/stripe/webhook` subscribed to `checkout.session.completed` and
   `checkout.session.async_payment_succeeded`; put its signing secret in
   `STRIPE_WEBHOOK_SECRET`.
6. Deploy `firestore.indexes.json` as checked in. It enables TTL on `purgeAt`
   for operational collection groups as well as creating the reservation and
   cleanup-job indexes.

### Every deploy

```bash
cp functions/.env.example functions/.env    # fill in non-secret values
firebase deploy --only functions,firestore:rules,firestore:indexes
```

Production secrets are declared and bound in `functions/src/index.ts`; they do
not belong in dotenv files. Local emulator-only values may be placed in the
gitignored `functions/.secret.local` file.

`firebase.json` runs the functions build first, which compiles `shared/` into
the bundle. Deploying without that build would ship a bundle whose pricing code
is missing.

### Turning it on in the app

```bash
cp .env.example .env                        # fill in, never commit
# then, only once the backend is verified against staging:
VITE_MANAGED_MODE_ENABLED=true
```

---

## 4. What is verified, and what is not

Everything in `shared/` is unit tested, including each money defect the original
draft carried. Those tests run in the normal suite. The core Firestore
concurrency, provider-scoped idempotency and deletion-tombstone path runs with:

```bash
cd functions
npm run test:emulator
```

**Verified against the live project:**

- Web reCAPTCHA Enterprise App Check issues a token on
  `chatwithmaestro.com`; enforced requests without it are rejected, and a valid
  token crosses CORS and reaches the Firebase Authentication gate.
- Android Play Integrity is registered with the existing release and debug
  SHA-256 fingerprints.
- The live Stripe destination is active with both Checkout completion events,
  and the signing secret is bound to the deployed API.
- The Play consumable is active and the runtime service account has the minimum
  app-scoped billing permissions.

**Still requiring live human testing:**

- Production Firestore behaviour under representative load. The emulator test
  proves two concurrent reservations cannot both overspend one balance, but it
  is not a latency/load test against the selected production region.
- Play purchase verification end to end. Requires a real or licence-tested
  purchase; the failure modes that matter are a pending purchase, a replayed
  token, and a token belonging to another account.
- Account deletion actually removing everything, which is a compliance
  obligation and not only a feature.
- Stripe end to end with a real payment. The endpoint and signatures are live,
  but the customer record, credit grant and redirect round trip still need a
  controlled purchase.

### Retention and deletion

- Rate-limit windows expire two minutes after their window starts.
- Released live leases and deleted file metadata remain for 30 days.
- Settled/released reservations and checkout grant snapshots remain for 90
  days. The billing and usage events are canonical and are not TTL'd.
- AI content reports remain for 365 days unless account deletion anonymizes
  them sooner.
- Purchase claims are not TTL'd: removing an idempotency claim would allow the
  same external purchase to grant credits again. Account deletion removes its
  user link and raw provider payloads while retaining the non-personal claim.
- Account-deletion tombstones contain only a one-way UID hash and prevent a
  delayed webhook or in-flight request from recreating a deleted account.
- Account deletion removes Stripe live/test customer objects as well as local
  customer IDs. A Stripe failure stops deletion before Firebase Auth is
  removed, allowing a safe retry instead of silently leaving remote PII.
- Failed remote Gemini file deletion is copied to `cleanupJobs` before the user
  tree is recursively removed. The hourly retry worker applies backoff; only a
  completed cleanup job receives `purgeAt`.

Firestore TTL is asynchronous (normally within about 24 hours), so expired
documents must still be treated as present until Firestore removes them. None
of the TTL-managed documents owns a subcollection.

### v1 was code-only

The v1 paths existed only in unreleased implementation code. They were never
deployed, no Firebase project was configured with them, and no v1 Firestore
collections, users, balances, purchases, reservations, files or ledgers were
created. Consequently, v2 is the first live managed-mode schema and requires no
backfill, dual reads/writes or production data migration.

The account-deletion handler still recognizes the abandoned v1 collection
names defensively, but this is cleanup hardening rather than a migration
contract. Deploy the functions, rules and indexes while
`VITE_MANAGED_MODE_ENABLED=false`; enable managed mode only after staging has
verified the complete v2 deployment.

---

## 4b. Payments, per platform

Android uses Google Play Billing and the web uses Stripe Checkout. That split is
a policy requirement, not a preference: Play's payments policy requires Play
Billing for digital goods bought inside the Android app, so Stripe cannot serve
the app even though it serves the same product. Both fund one credit balance,
and nothing downstream of the grant knows which was used.

Two rules hold on the Stripe side, and both are what separate a billing
integration that works from one that gives money away:

- **Credits are granted from the webhook, never from the redirect.** The return
  from Checkout can be forged, replayed, closed early, or never arrive. The
  webhook is the only statement from Stripe that a payment settled.
- **The quantity comes from an immutable server snapshot** written under the
  Checkout session id before its URL is returned. Fulfilment never re-reads the
  mutable catalogue or trusts session metadata, so later catalogue edits cannot
  change what an already-created Checkout session buys.

The webhook route is registered with a raw body parser *before* `express.json()`.
Stripe signs the exact bytes it sent; parsing and re-serialising changes them and
every signature check fails with an error that reads like a bad secret rather
than middleware ordering.

### Verifying it locally

```bash
stripe listen --forward-to localhost:5001/<project>/<region>/api/billing/stripe/webhook
stripe trigger checkout.session.completed
```

The signing secret `stripe listen` prints is what goes in `STRIPE_WEBHOOK_SECRET`
for local runs. Worth testing specifically: replaying the same event (must grant
once), a session with `payment_status: unpaid` (must not grant), and a tampered
`credits` value in metadata (must grant the catalogue amount).

---

## 5. Failure modes worth knowing

**A request costs more than its reservation.** Settlement charges what the
balance can cover and records the remainder as `shortfallCredits` on the usage
ledger entry, rather than driving the balance negative. A negative balance would
silently swallow the user's next purchase. Recurring shortfalls mean the
estimate in `shared/pricing/credits.ts` is too low for that operation — the
backend logs a warning naming it.

**A purchase is verified but consumption fails.** Credits are already granted;
the token stays in the user's Play inventory until the next verification
consumes it. The user has what they paid for, and the worst case is that they
cannot re-buy that pack until it clears. This ordering is deliberate: consuming
first, as the draft did, meant any failure after consumption took the money and
destroyed the token with nothing left to retry.

**A live session cannot connect.** Reconnects back off exponentially and stop
after a bounded number of attempts. Each reconnect mints a token and reserves
credits, so an unbounded retry is a direct cost.

**Firestore is unavailable for the rate limiter.** It fails open. A throttle
that takes the service down when its own storage hiccups is worse than one that
briefly lets traffic through, and everything behind it still has to reserve
credits before it can spend anything.
