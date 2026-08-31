# Managed mode: architecture and runbook

Managed mode lets someone use Maestro Tutor without supplying a Gemini API key.
They sign in, buy credits — through Google Play in the Android app, or Stripe
Checkout on the web — and the backend proxies Gemini on their behalf, charging
credits against real usage.

It ships **dark**: `VITE_MANAGED_MODE_ENABLED` is `false` by default and the app
behaves exactly as it does today. Nothing below is reachable until that flag is
turned on in a build.

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

Firestore holds the balance, the reservations, the ledgers and the purchase
records. Rules deny everything by default: a signed-in user may **read** their
own account summary, ledgers, and entitlements. Every write goes through the
Admin SDK, which bypasses rules, so there is no path for a client to write its
own balance.

### Where the money logic lives

| Concern | Location | Tested |
|---|---|---|
| Rate card, model matching, usage → USD | `shared/pricing/` | yes |
| Credits, reservation estimates | `shared/pricing/credits.ts` | yes |
| Balance arithmetic and its invariants | `shared/billing/ledger.ts` | yes |
| Reconnect pacing | `shared/reconnect/policy.ts` | yes |
| Firestore persistence of the above | `functions/src/managedBilling.ts` | no — needs the emulator |
| Play verification | `functions/src/playBilling.ts` | no — needs a real purchase |
| Stripe fulfilment rules | `shared/billing/stripeFulfilment.ts` | yes |
| Stripe checkout and webhook | `functions/src/stripeBilling.ts` | no — needs Stripe CLI or a live endpoint |

`shared/` is compiled into the functions bundle (see `functions/tsconfig.json`,
which roots at the repo so `shared/` is emitted alongside `functions/src`). The
app imports the same files directly.

---

## 3. Deploying

### Once per project

1. Create the Firebase project; enable Firestore and Google sign-in.
2. Grant the functions service account access to the Play Developer API, and
   link the Play Console to the project. Purchase verification fails without it.
3. Configure App Check for web and Android. Leave `REQUIRE_APPCHECK=false`
   until both are verified, then turn it on — it is the main defence against
   someone driving the backend outside the app.
4. Define the credit packs once in `MANAGED_CREDIT_PACKS` as
   `id:credits:cents[:playProductId]`. Create the matching consumable products
   in Play for any pack that names a `playProductId`.
5. For web payments, set `STRIPE_SECRET_KEY`, `APP_URL`, and add a webhook
   endpoint in the Stripe dashboard pointing at
   `<api>/billing/stripe/webhook` subscribed to `checkout.session.completed`;
   put its signing secret in `STRIPE_WEBHOOK_SECRET`.

### Every deploy

```bash
cp functions/.env.example functions/.env    # fill in, never commit
firebase deploy --only functions,firestore:rules,firestore:indexes
```

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
draft carried. Those tests run in the normal suite.

**Not yet verified, and needing a live project before release:**

- Firestore transaction behaviour under concurrency. The arithmetic is tested;
  its persistence is not. Worth running the emulator with two concurrent
  requests against one balance before trusting it.
- Play purchase verification end to end. Requires a real or licence-tested
  purchase; the failure modes that matter are a pending purchase, a replayed
  token, and a token belonging to another account.
- App Check enforcement.
- Account deletion actually removing everything, which is a compliance
  obligation and not only a feature.
- Stripe end to end against the real account. The decision logic is tested, but
  signature verification, the customer record and the redirect round trip are
  not exercised by unit tests.

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
