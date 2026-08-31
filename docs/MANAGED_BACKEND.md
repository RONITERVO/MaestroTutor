# Managed mode: architecture and runbook

Managed mode lets someone use Maestro Tutor without supplying a Gemini API key.
They sign in, buy credits through Google Play, and the backend proxies Gemini
on their behalf, charging credits against real usage.

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
Gemini reports, and every purchase is verified against Google Play rather than
believed.

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
own account summary and ledgers and nothing else. Every write goes through the
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
4. Create the consumable credit products in Play and list them in
   `MANAGED_CREDIT_PRODUCTS` as `productId:credits`.

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
