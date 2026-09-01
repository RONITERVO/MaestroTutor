# Stripe-only managed billing

## Status and historical note

This is the authoritative billing document for Maestro. As of the long-term
release branch, Stripe Checkout is the only active provider for managed-credit
purchases. The pack id, checkout controller, webhook fulfilment, Firestore claim
and credit ledger are shared 1:1 by the hosted UI, Core SDK, CLI/JSON-RPC harness
and the optional Android external-browser adapter.

The earlier Google Play managed-credit implementation (“v1”) existed only in the
repository. It was never configured as a live product, never connected to a Play
Console consumable, and never used to grant a deployed purchase. It is removed,
not migrated. The deletion path still recognizes the old draft collection names
and `google-play` platform value defensively so that a developer/emulator document
cannot leak a raw token or break account deletion. That compatibility code is not
an active billing route.

Do not restore any of these without a new reviewed architecture decision:

- `ManagedBillingPlugin` / `ManagedBillingManager`;
- the Android Billing Client dependency;
- `/billing/google-play/verify`;
- `functions/src/playBilling.ts` or `googleapis`;
- Play product aliases in `MANAGED_CREDIT_PACKS`;
- a client-side purchase-token cache or grant request.

`npm run verify:release-config` fails if the most dangerous parts of that second
path reappear.

## The one grant flow

```text
Buy credits
  -> createManagedAccountController.startStripeCheckout(packId)
  -> POST /billing/stripe/checkout
  -> immutable checkoutGrants/<cs_...> snapshot
  -> Stripe-hosted Checkout
  -> Stripe signed webhook
       checkout.session.completed
       or checkout.session.async_payment_succeeded
  -> resolveCheckoutGrant requires payment_status=paid
  -> purchaseClaims/stripe_<sha256(session id)> transaction
  -> account summary + entitlement + one billing-ledger entry
  -> normal account refresh observes the new balance
```

The redirect never grants credits. It is only a signal to poll the account. The
webhook must verify Stripe's signature from the exact raw request bytes. The
Checkout session id, not the event id, is the idempotency key; a completed event
and later async-payment success for the same session are one purchase.

Never add a second “temporary” grant path, an admin credit increment, or a client
callback that trusts a success URL. If a payment is stuck, repair webhook delivery
or the immutable checkout snapshot and redeliver the signed event.

## Configuration contract

Client build:

```dotenv
VITE_MANAGED_CREDIT_PACK_IDS=pack_1000
VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED=false
```

Functions runtime:

```dotenv
MANAGED_CREDIT_PACKS=pack_1000:1000:299
APP_URL=https://chatwithmaestro.com
BILLING_CURRENCY=eur
```

`MANAGED_CREDIT_PACKS` is comma-separated `id:credits:priceInSmallestCurrencyUnit`.
There is no provider alias field. Every id in `VITE_MANAGED_CREDIT_PACK_IDS` must
exist exactly once in the backend catalogue. The release verifier checks the
tracked staging/example files; a maintainer must also check the deployed Functions
environment before release.

Secret Manager contains:

- `STRIPE_SECRET`: a restricted key for the correct Stripe mode/account;
- `STRIPE_WEBHOOK_SECRET`: the revealed `whsec_...` value for this exact endpoint;
- `GEMINI_API_KEY`: unrelated to checkout, but required for a ready backend.

Public Firebase web configuration and pack ids are not secrets. Stripe keys,
webhook secrets, Firebase/password credentials and App Check debug tokens are.
Never print them, place them in command arguments, or commit them.

## Stripe Console: rebuild or rotate

Production account: **Maestro Chat And Learn**. Use live mode for production and
test mode for staging. Do not copy a test key or signing secret into production.

1. Deploy Functions first so the endpoint exists:
   `https://europe-west1-<project>.cloudfunctions.net/api/billing/stripe/webhook`.
2. Create or open the webhook event destination for that exact URL.
3. Subscribe only to:
   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded`
4. Reveal the endpoint signing secret. A masked `whsec_····` value is not usable.
5. Store the revealed value as `STRIPE_WEBHOOK_SECRET` in the matching GCP project.
6. Create/restrict the Stripe API key used by Functions. It needs the operations
   used in `stripeBilling.ts` (customers and Checkout Sessions) and no broader
   dashboard role than necessary. Store it as `STRIPE_SECRET`.
7. Redeploy `api` so Cloud Functions binds the newest secret versions.
8. Send a test delivery and confirm HTTP 200. An invalid signature must return 400.
9. Run `billing.checkout.completeTest` in staging and require exactly one credit
   delta and one new purchase-ledger entry.
10. Redeliver the same event. It must return success and make no balance change.

If the account, mode, endpoint or secret differs, treat it as a different
integration. Stripe customer ids are stored separately for live and test mode so a
mode switch cannot reuse an invalid customer.

## Firebase and App Check prerequisites

Billing routes require Firebase Authentication and App Check in addition to Stripe
signatures. Keep these provider controls distinct:

- Web App Check uses the domain-restricted reCAPTCHA Enterprise key registered to
  the exact Firebase web app. Production domains only belong on the production key;
  staging domains only belong on staging.
- Android App Check uses Play Integrity with the existing release and debug SHA-256
  fingerprints. Accepting the Play Integrity/API terms enables attestation; it does
  not authorize an alternative payment method.
- Headless CI uses one named staging App Check debug token. It is a secret and must
  never be enabled for production.
- `REQUIRE_APPCHECK=true` is the normal deployed state. A temporary emergency
  bypass requires an incident record and immediate rollback plan.

Health must report Firestore ready, Gemini configured, Stripe configured, App Check
required, and the intended `managedCreditPacks` list. Health is configuration
evidence, not payment evidence.

## Android external checkout gate

The native code contains no Google Play purchase SDK. When
`VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED=true`, the same Core SDK checkout
controller opens Stripe in a Capacitor Custom Tab and refreshes the shared account
after the tab closes. Staging/internal builds may use that adapter for testing.

For a Google Play-distributed production build, leave the flag **false** until all
of the following are recorded in the release ticket:

1. the app and target countries are eligible for the applicable Google Play
   external-offers or alternative-billing programme;
2. the Play Console enrollment/terms are accepted for this app and account;
3. the required Google information/choice UI is implemented if the programme
   requires it;
4. any required external transaction token is obtained and passed through the
   purchase journey;
5. external transactions are reported to Google within the programme deadline;
6. a licensed internal-track build completes Stripe Checkout and receives exactly
   one credit grant; and
7. policy/legal review approves the release countries and copy.

Official references change; verify them at release time:

- [Google Play billing choice](https://developer.android.com/google/play/billing/billingchoice)
- [External offers integration](https://developer.android.com/google/play/billing/external/integration)
- [Alternative billing backend integration](https://developer.android.com/google/play/billing/outside-gpb-backend)

If those conditions are not proven, Android users can still sign in and spend the
same balance purchased on the hosted web app; only the in-app buy button is hidden.
Do not enable the flag merely because the code compiles or a staging Custom Tab
works.

## Staging release proof

The `Headless staging journey` workflow is the canonical bounded proof. It begins
with one real Stripe **test-mode** Checkout using Stripe's fixed `4242` test card,
then exercises real managed routes using the granted credits:

- authenticated account/Firestore reads;
- tutor chat and suggestions;
- text, image, WAV and PDF uploads followed by multimodal tutor turns;
- raw Gemini generate and stream;
- image and Gemini Live audio-note generation;
- Lyria music streaming through the shared Core SDK;
- synthetic PCM through the shared Gemini Live stream path;
- usage/billing ledgers, file cleanup and sign-out.

Required GitHub variables:

- `MAESTRO_BACKEND_BASE_URL`
- `MAESTRO_FIREBASE_API_KEY`
- `MAESTRO_FIREBASE_APP_ID`
- `MAESTRO_TEST_PACK_ID` (normally `pack_1000`)
- `MAESTRO_TEST_PACK_CREDITS` (normally `1000`)

Required GitHub secrets:

- `HEADLESS_FIREBASE_EMAIL`
- `HEADLESS_FIREBASE_PASSWORD`
- `HEADLESS_APPCHECK_DEBUG_TOKEN`

The browser adapter refuses live Stripe sessions, non-Stripe initial hosts and an
arbitrary card number. Any failure stops the workflow; paid journeys are not
silently skipped for a zero balance because the controlled checkout is the first
provider step.

Local equivalent:

```powershell
npm ci
npm --prefix functions ci
npm run verify:release-config
npm run maestro -- auth.signIn --profile release-smoke
npm run maestro -- billing.checkout.completeTest --profile release-smoke --params '{"packId":"pack_1000","expectedCredits":1000,"headless":true}'
npm run maestro -- chat.attachment.turn --profile release-smoke --params '{"text":"Identify this PDF.","fixture":"pdf","cleanup":true}'
npm run maestro -- media.music.generate --profile release-smoke --params '{"prompt":"Original scale-practice backing track","durationSeconds":8}'
```

## Incident triage

Checkout cannot open:

- verify the client pack id matches deployed `MANAGED_CREDIT_PACKS`;
- verify Firebase ID token and App Check token reach the backend;
- verify `APP_URL` and allowed origins;
- verify the Stripe restricted key is for the intended mode/account.

Checkout paid but balance unchanged:

- inspect the Stripe destination delivery and HTTP response;
- confirm the event is one of the two subscribed types and the session is paid;
- confirm `checkoutGrants/<session id>` exists and is not marked account-deleted;
- confirm the webhook secret belongs to this endpoint/mode;
- inspect Functions logs and redeliver the same signed event after fixing the cause.

Balance changed twice:

- stop the release immediately;
- preserve the session/event ids and Firestore records;
- verify both event types resolve to the same `stripe_<sha256(session id)>` claim;
- do not delete claims to “retry”; claims are the protection against another grant.

Android buy button missing:

- this is expected when the production external-checkout flag is false;
- check the release ticket for programme proof before changing it;
- do not restore Play Billing as a quick fix.

## Maintainer change checklist

For any billing change:

1. update this document and the environment examples;
2. run `npm run verify:release-config`;
3. run app tests, lint and production build;
4. run Functions unit, CORS and Firestore emulator tests;
5. run the staging Stripe-first workflow;
6. inspect the one-grant invariant and final reserved-credit balance;
7. verify App Check remains enforced;
8. obtain independent review before production; and
9. record provider console changes, secret version numbers (not values), deployment
   revision and workflow URL in the release ticket.
