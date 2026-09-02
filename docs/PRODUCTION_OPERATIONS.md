# Production operations for managed Maestro

This is the beginner-safe maintainer runbook for the live managed service. It
records what exists, which account permissions are needed, how to release it,
how to rotate credentials, what to verify, and how to roll back. Architecture
and data-model reasoning remain in [MANAGED_BACKEND.md](./MANAGED_BACKEND.md).

Never paste a login token, Firebase CLI JSON response, Stripe secret, webhook
signing secret, service-account private key, keystore password, or Gemini key
into an issue, commit, build log, or chat. Client-side Firebase configuration
and App Check site keys are public identifiers, but they should still be copied
from the provider console or the maintainer's ignored `.env`, not duplicated in
this document.

## 1. Production inventory

Verified on 2026-09-01:

| Item | Production value |
|---|---|
| Public site | `https://chatwithmaestro.com` |
| Source branch at setup time | `feat/managed-backend` |
| Firebase/GCP project | `chatwithmaestro` |
| GCP project number | `47084692464` |
| Functions region | `europe-west1` |
| Managed API | `https://europe-west1-chatwithmaestro.cloudfunctions.net/api` |
| Firebase web app | `1:47084692464:web:7e83d9b9c36b6c59ac7335` |
| Firebase Android app | `1:47084692464:android:1f3e885d2a423a63ac7335` |
| Android package | `com.ronitervo.maestrotutor` |
| Play developer ID | `7337511454933294600` |
| Play app ID | `4972859959018040172` |
| Managed credit pack | `pack_1000` — 1,000 credits, Stripe Checkout price EUR 2.99 |
| Stripe account | `Maestro Chat And Learn` (`acct_1Rui2P1dnAf99VGW`) |
| Stripe webhook destination | `maestro-managed-billing` (`we_1UAmNP1dnAf99VGWLBHM6tdC`) |
| Stripe webhook URL | `https://europe-west1-chatwithmaestro.cloudfunctions.net/api/billing/stripe/webhook` |

The API is a Node.js 22, second-generation Cloud Function. Two scheduled
functions also exist in `europe-west1`: expired reservations run every ten
minutes and managed file cleanup retries hourly. Firestore rules, indexes and
TTL policies are deployed. Artifact Registry deletes old function images after
30 days.

Managed access has no dark-launch flag. The client exposes it only when the
Firebase client and backend URL are completely configured; a clean checkout
with blank template values remains BYOK-only. BYOK keeps precedence when a user
has both access methods, preventing an existing personal key from unexpectedly
spending managed credits.

## 2. Provider state

### Firebase App Check

- Web uses reCAPTCHA Enterprise with a domain-restricted key for
  `chatwithmaestro.com` and `chatwithmaestro.web.app`.
- Android uses Play Integrity.
- Server enforcement is on: production `functions/.env` has
  `REQUIRE_APPCHECK=true`.
- The live web origin has minted a token and passed it through browser CORS to
  the backend. A request without App Check is rejected before Firebase Auth.

Registered Android signing fingerprints:

| Build | SHA-1 | SHA-256 |
|---|---|---|
| Release | `E0:BF:75:8C:4F:98:25:6D:FC:36:37:AE:02:6A:65:AD:FD:25:80:48` | `B1:DD:E2:0B:85:5C:1E:EE:62:63:7A:A7:C2:0C:63:DE:2C:A0:20:AA:FB:2B:BC:01:44:5C:00:E5:48:11:41:02` |
| Debug | `78:22:03:9E:20:50:86:68:21:40:24:31:B4:B9:F6:C1:07:94:1E:56` | `06:72:30:B4:E7:1F:3F:DE:79:54:26:45:D9:4E:35:91:20:9A:02:34:2B:9F:F7:3E:1B:8B:C9:E2:DB:D3:2E:8F` |

Official references: [web Enterprise App Check](https://firebase.google.com/docs/app-check/web/recaptcha-enterprise-provider),
[Android Play Integrity App Check](https://firebase.google.com/docs/app-check/android/play-integrity-provider),
and [enforcement](https://firebase.google.com/docs/app-check/enable-enforcement).

### Stripe

The live webhook listens to exactly:

- `checkout.session.completed`
- `checkout.session.async_payment_succeeded`

Both events pass paid Checkout sessions to the same idempotent fulfilment path.
An initially unpaid delayed-payment session receives credits only after the
async success event. The webhook signing secret is in Secret Manager as
`STRIPE_WEBHOOK_SECRET`; the live restricted Stripe API key is
`STRIPE_SECRET`. Do not replace either with a test-mode value in production.

Older webhook destinations named `playful-brilliance` and
`paymentevents-maestro` predate this setup. Before enabling or rebuilding any
destination, inspect both legacy destinations, record their URLs, subscribed
events, owners and consumers, and disable the two Checkout fulfilment events or
quarantine any unidentified endpoint. Do not leave an unidentified consumer
receiving credit-fulfilment events. Deletion remains a separately reviewed
provider change after ownership and retention evidence is recorded.

Official reference: [Stripe webhooks](https://docs.stripe.com/webhooks).

### Android distribution and Play Integrity

Google Play is used for distribution and Play Integrity App Check, not for
managed-credit purchases. The release and debug SHA-256 fingerprints above are
registered and the Play Integrity/API terms are accepted. Revalidate attestation
with a signed internal-track build after changing package, certificate or Firebase
app configuration.

The old Android Publisher billing-verifier service accounts and API permissions
are not required by the current application. They are legacy cloud-access cleanup,
not a release dependency; remove them only through an independently reviewed IAM
change after ownership/audit evidence is recorded.

### Gemini model policy

The public `gemini-models.json` registry chooses the client models and contains
their pricing rules. The Functions environment independently allowlists models
with `MANAGED_ALLOWED_GEMINI_MODELS`, `MANAGED_ALLOWED_LIVE_MODELS` and
`MANAGED_ALLOWED_MUSIC_MODELS`. This is intentional: editing public JSON must
not turn the service API key into access to an arbitrary or unpriced model.

Treat a model update as one release: verify the provider name and pricing,
update the checked-in registry/defaults and pricing tests, update the matching
server allowlist, deploy Functions, publish the client, and run one managed
request for every affected surface. Live tokens are constrained to the exact
allowlisted model requested by the client.

Managed text traffic is pinned to the stable `gemini-3.7-flash` and
`gemini-3.5-flash-lite` IDs. Do not put provider-owned `*-latest` aliases in the
Functions allowlist: Google can hot-swap an alias to a model with different
behavior or pricing. The backend maps the two old text aliases to these stable
IDs so installed clients remain compatible. Settlement uses the provider's
returned model version and records both requested and resolved IDs in metadata.

The checked-in Gemini 3.7 and 3.6 Flash Standard rates were verified on
2026-09-01. Their promotional input/output/cache rates expire after 2026-12-31
according to the provider pricing page. Re-verify and deploy the registry before
2027-01-01; leaving an expired rate in production is a billing incident.
Grounded Gemini 3 requests are settled per reported Search query at list price,
with ten queries reserved before the call by default.

Provider release gate passed on 2026-09-01: the production secret successfully
ran `countTokens` and a paid `gemini-3.7-flash` generation, returned visible text,
and reported prompt, output and thinking usage. Re-run `npm run smoke:gemini`
before every backend release; a configured secret or successful `countTokens`
alone is not evidence that provider billing is usable.

The Gemini Developer API `countTokens` endpoint does not accept the full generation
config (`systemInstruction`, tools or generation config), even though the SDK type
surface permits it. The backend counts contents and the instruction separately and
adds a deterministic serialized-config estimate, then settles from final provider
usage. Do not “simplify” this by passing the full generation request to
`countTokens`. A provider stream error releases its reservation with
`provider-stream-failed`, including after partial/thought chunks; charging the full
reservation for an incomplete answer is a billing incident. Client disconnects are
consumed to provider completion so actual usage can still be settled.

## 3. Access a maintainer needs

A maintainer can complete the entire runbook with:

- Write access to the GitHub repository and permission to publish `gh-pages`.
- Firebase/GCP access to project `chatwithmaestro`, including Functions,
  Firestore, App Check, Secret Manager, Cloud Scheduler and billing visibility.
- Play Console permission to manage the Maestro app's integrity settings, test
  tracks, users and releases.
- Stripe access to the `Maestro Chat And Learn` live account, including
  Developers/Webhooks and restricted keys.
- Access to the Android release keystore and its passwords through the team's
  approved password manager. Never send these in chat.

Use the smallest provider permission set that permits the task. Do not create
downloadable service-account JSON keys for the backend: deployed functions use
their runtime identity.

## 4. Prepare a workstation

Install Node.js 22, npm, Git, JDK 21, Android Studio/SDK, Google Cloud CLI and
GitHub CLI. The repository's local Firebase CLI is the supported deployment
CLI; do not depend on an unrelated global version.

```powershell
git clone <repository-url>
cd MaestroTutor
npm ci
cd functions
npm ci
cd ..
```

Authenticate interactively when needed:

```powershell
gh auth login
gcloud auth login
gcloud config set project chatwithmaestro
cd functions
npm exec firebase -- login
```

Use `npm exec firebase -- login:list` if you only need to see the signed-in
email. Never use `firebase login:list --json`: its machine-readable output can
contain reusable OAuth credentials and must not be copied into logs.

Copy the templates to ignored local files and fill them from Firebase Console,
Secret Manager metadata and the provider dashboards:

```powershell
Copy-Item .env.example .env
Copy-Item functions\.env.example functions\.env
```

Rules:

- Root `.env` contains only client configuration embedded in the app build.
- `functions/.env` contains non-secret server settings.
- Production secrets live only in Secret Manager.
- `functions/.secret.local` is only for emulator secrets and is gitignored.
- Never commit any of those ignored files.
- Always pass the intended Firebase project explicitly. In the checked-in
  `.firebaserc`, `default` is production and `staging` is staging; do not rely on
  whichever alias a previous maintainer selected locally.

For production, confirm the fail-closed server setting without printing the
rest of the file:

```powershell
rg "^REQUIRE_APPCHECK=" functions\.env
```

It must be `true`. Also verify that the root `.env` has the complete Firebase
and backend values; there is no separate client switch.

## 5. Validate before any release

From the repository root:

```powershell
npm test
npm run lint
npm run build
npm audit --omit=dev --audit-level=critical
git diff --check
```

From `functions/`:

```powershell
npm test
npm run test:emulator
npm audit --omit=dev --audit-level=high
```

Then run the paid-provider smoke test. It makes one bounded, very small Gemini
generation and therefore incurs a real provider charge:

```powershell
$env:GEMINI_API_KEY = (gcloud secrets versions access latest --secret GEMINI_API_KEY --project chatwithmaestro).Trim()
npm run smoke:gemini
Remove-Item Env:GEMINI_API_KEY
```

The command must return JSON with `ok: true`. Never echo the environment
variable or paste it into the command itself. A successful `countTokens` call
without a successful generation is not a pass. The smoke also requires visible
text because an output ceiling consumed entirely by thinking tokens is not a
usable generation.

Pull requests and pushes to the release branches run the same release gate in
`.github/workflows/release-gate.yml` on Node 22 and JDK 21. Do not merge a red
gate or weaken it to get a release through.

The emulator test requires Java; use JDK 21 and make sure `JAVA_HOME` points at
that JDK when more than one Java version is installed. A bundle-size warning for
the main app and Whisper worker is currently known. Treat new build errors as
blocking. Never run an audit force fix blindly on the release branch.

Audit exceptions reviewed on 2026-09-01:

- The root runtime tree reports `adm-zip` and `sharp` through Transformers.js.
  They belong to its Node-only ONNX/image path; the production browser bundle
  uses `onnxruntime-web` and contains neither package. `@capacitor/assets` also
  carries older `sharp`/`tar` versions, but it is a development-only tool that
  processes repository-owned assets. Recheck these when either upstream ships
  an update; do not force incompatible transitive majors into the lockfile.
- The Functions runtime tree reports a moderate `uuid` advisory through
  Firebase Admin's Google Cloud Storage client. The affected buffer overloads
  of UUID v3/v5/v6 are not called by Maestro. Keep the high-severity Functions
  audit gate and remove this exception when Firebase Admin updates the chain.

The CI thresholds intentionally fail on a new critical shipped-app advisory or
a new high-severity Functions advisory. A lower-severity result still requires
human review during dependency maintenance; the threshold is not a statement
that every lower-severity advisory is harmless.

## 6. Publish the web app

1. Confirm root `.env` has the live Firebase values, Enterprise site key,
   backend URL, Google client IDs and `pack_1000`.
2. Confirm `public/CNAME` contains exactly `chatwithmaestro.com`.
3. Run:

```powershell
npm run deploy
```

This builds and publishes `dist/` to `gh-pages`. Vite's
`avoid-generated-secret-false-positives` plugin rewrites two public
Transformers.js strings in the generated Whisper worker that otherwise resemble
provider keys. Do not bypass GitHub push protection for those strings, and do
not remove the plugin without confirming a production build can push safely.
If a rejected `gh-pages` commit remains in the tool cache after fixing output,
run `npm exec gh-pages-clean`, rebuild, and publish again.

Verify after GitHub Pages propagates:

```powershell
Invoke-WebRequest https://chatwithmaestro.com/ -UseBasicParsing
Invoke-WebRequest https://chatwithmaestro.com/delete-account.html -UseBasicParsing
```

Open the site in a clean browser profile, confirm the managed sign-in option is
visible, sign in, refresh the account, and sign out. Do not turn App Check off
merely because Pages propagation is still returning an old asset; wait and
verify the asset actually changed.

## 7. Deploy the backend

Run from `functions/` so the pinned Firebase CLI and package lock are used:

```powershell
$env:FUNCTIONS_DISCOVERY_TIMEOUT='60'
npm exec firebase -- deploy --config ../firebase.json --only functions --project chatwithmaestro
```

Rules and indexes can be deployed separately:

```powershell
npm exec firebase -- deploy --config ../firebase.json --only firestore:rules,firestore:indexes --project chatwithmaestro
```

Do not accept deletion of an index you do not recognize. The live project has
an older `userAppSessions` index outside managed mode; preserve it or first add
its intended definition to the repository.

Quick checks:

```powershell
Invoke-RestMethod https://europe-west1-chatwithmaestro.cloudfunctions.net/api/health
gcloud scheduler jobs list --location europe-west1 --project chatwithmaestro
gcloud functions describe api --gen2 --region europe-west1 --project chatwithmaestro
```

Expected health facts are `ok: true`, region `europe-west1`, Firestore ready,
Gemini and Stripe configured, App Check required, managed product `pack_1000`,
and model lists matching the deployed registry. The response deliberately says
`geminiConfigured`, not `geminiReady`: the paid smoke test above is what proves
the provider balance and generation quota are usable.

## 8. App Check changes and rollback

When registering or rotating the web key:

1. Create a reCAPTCHA Enterprise website key restricted to the production
   domains.
2. Register that key against the Firebase web app.
3. Put its public site key in root `.env`, build and publish the web app.
4. Verify the live site can obtain an App Check token.
5. Only then keep `REQUIRE_APPCHECK=true` and deploy `api`.

For Android, register Play Integrity against the Firebase Android app and keep
both release and debug SHA-256 fingerprints current. If the signing certificate
changes, register the new fingerprint before publishing that build.

Enforcement check without a token:

```powershell
curl.exe -sS "https://europe-west1-chatwithmaestro.cloudfunctions.net/api/auth/session" `
  -H "Origin: https://chatwithmaestro.com"
```

It must return `Missing Firebase App Check token.` A real app token should pass
App Check and then either authenticate normally or, without an ID token, return
`Missing Authorization bearer token.` Never print an App Check token.

Emergency rollback: set `REQUIRE_APPCHECK=false` in the ignored
`functions/.env`, deploy only `functions:api`, confirm service recovery, and
open an incident. Restore enforcement after fixing the web/Android provider;
do not leave the rollback undocumented.

## 9. Stripe maintenance

The live destination must remain enabled and subscribe to both Checkout events
listed in section 2. When rotating its signing secret:

```powershell
cd functions
npm exec firebase -- functions:secrets:set STRIPE_WEBHOOK_SECRET --project chatwithmaestro
$env:FUNCTIONS_DISCOVERY_TIMEOUT='60'
npm exec firebase -- deploy --config ../firebase.json --only functions:api --project chatwithmaestro
```

Enter the value only at the interactive prompt. Send a Stripe test event from
the live destination's provider UI, confirm a 2xx delivery, then disable the old
Secret Manager version. The same procedure applies to `STRIPE_SECRET`, using a
live restricted key for the correct account. Never use a broad secret key if a
restricted key can provide the required Checkout/customer permissions.

Stripe's dashboard masks the signing secret until its reveal control is activated.
Never copy a visible `whsec_····` placeholder: it is not a secret and every delivery
will fail. Cloud Functions can expose parsed JSON in `req.body` while preserving
the signed bytes in `req.rawBody`; the webhook handler accepts only a Buffer from
one of those locations. Keep the raw-body regression tests when changing Express,
Firebase Functions or webhook middleware.

For an end-to-end release test, make one low-value real Checkout purchase with
a dedicated test user. Confirm exactly one billing ledger grant, the expected
credit balance, and a successful webhook delivery. Also test a delayed-payment
method when available; `checkout.session.completed` may be unpaid and must not
grant until `checkout.session.async_payment_succeeded` arrives. Refund or
otherwise reconcile the test purchase according to the team's accounting
process.

### Customer-specific 100% promotion

Use a 100% promotion when an authorized maintainer needs to prove the live
Checkout/webhook/ledger journey without taking money. Do not add credits by hand:
that bypasses the route being tested. The user must first start and cancel one
Checkout so the backend creates and stores the correct mode-specific Stripe
customer. Then use Application Default Credentials with Firebase Auth/Firestore
access and load the matching restricted Stripe key without printing it:

```powershell
$env:STRIPE_SECRET = gcloud secrets versions access latest --secret=STRIPE_SECRET --project=chatwithmaestro
try {
  npm --prefix functions run promotion:create-user -- --project chatwithmaestro --mode live --email maintainer@example.com --expires-hours 24
} finally {
  Remove-Item Env:STRIPE_SECRET
}
```

The command verifies the Firebase user, canonical managed-account customer id,
Stripe mode and the customer's Firebase UID metadata. It creates a random code
restricted to that one customer, with one redemption and a short expiry. Share
only the generated code through the approved maintainer channel; never share the
restricted key. In Checkout, expand the promotion-code field, enter the code and
complete the zero-total order. A correct event has an exact full discount, zero
total and no PaymentIntent. The signed webhook still grants from the immutable
Checkout snapshot and uses the Checkout session id for idempotency. Confirm one
ledger entry and the exact pack credit delta, then confirm the promotion shows one
redemption. Replaying the delivery must not change the balance.

If the command reports no customer, start Checkout and cancel it before payment,
then retry. If credentials, mode, customer metadata, subtotal, currency or full
discount do not match, stop; do not weaken the verifier or create an unrestricted
code as a workaround.

## 10. Stripe-only catalogue and Android checkout policy

Color themes are local and permanently free. Managed credits use the single
Stripe catalogue:

```text
Client:     VITE_MANAGED_CREDIT_PACK_IDS=pack_1000
Functions:  MANAGED_CREDIT_PACKS=pack_1000:1000:299
```

When changing price or adding a pack, update both values in one release, run
`npm run verify:release-config`, and complete a controlled Stripe test purchase.
Do not reuse a pack id for a different credit quantity after Checkout sessions
exist.

The Android app has no Play Billing SDK or verification route. Its optional
Stripe Custom Tab uses the same Core SDK checkout path, but production must keep
`VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED=false` until Play programme
eligibility, enrollment, required information/choice UI, transaction-token
handling and reporting are all recorded. See `docs/STRIPE_ONLY_BILLING.md` and
verify Google's current rules at release time.

Do not declare `com.android.vending.BILLING` in the manifest. Even without a
Billing Client dependency, Play interprets that legacy permission as an AIDL
billing integration and blocks the release for using an obsolete library. The
release-config verifier checks both the Gradle dependency text and manifest.

Closed/internal-track tester-count and duration requirements remain Play
distribution gates. Read the live Console because Google can change them; they are
separate from payment-provider eligibility.

## 11. Build and test Android

`android/app/google-services.json` belongs to the registered Firebase Android
app and is tracked in this repository. Verify its package is
`com.ronitervo.maestrotutor` after any Firebase app change.

```powershell
npm run cap:android
npm run cap:open:android
```

Build a signed Android App Bundle in Android Studio or with the repository's
Gradle release configuration. At setup time the app used version code 67 and
version name 2.3.1; every Play upload needs a higher version code. Do not upload
or promote a release merely to test a local build. Use an internal or closed
track, test Google sign-in, App Check, shared credit balance, Gemini journeys and
account deletion. If external Stripe checkout is policy-approved and enabled,
also test Custom Tab return and exactly-once webhook reconciliation; otherwise
verify the buy action is fail-closed. Then promote through Play Console.

## 12. Monitoring and incident checks

Useful read-only commands:

```powershell
gcloud run services describe api --region europe-west1 --project chatwithmaestro
gcloud logging read 'resource.type="cloud_run_revision" AND resource.labels.service_name="api"' --project chatwithmaestro --limit 50
gcloud scheduler jobs list --location europe-west1 --project chatwithmaestro
gcloud secrets versions list STRIPE_WEBHOOK_SECRET --project chatwithmaestro
```

Also inspect:

- Firebase Functions errors, latency, instance count and App Check metrics.
- Firestore usage, rejected rules requests, TTL backlog and index health.
- Stripe webhook delivery failures and retries.
- Play Integrity/App Check failures and internal-track release health.
- Cloud Billing spend against a maintainer-approved budget.

The deployed API caps scaling at 10 instances with concurrency 20. Credits and
rate limits reduce abuse but do not replace a Cloud Billing budget. No spending
threshold should be invented by a maintainer: the owner must choose the monthly
budget and alert recipients, then record them in this document.

## 13. Recovery rules

- Bad web release: revert the source commit, rebuild, and run `npm run deploy`.
  Do not force-push `gh-pages` unless repository owners explicitly approve it.
- Bad API revision: revert the source/config change and redeploy `functions:api`.
- App Check outage: use the temporary rollback in section 8.
- Stripe failures: leave webhook signature verification fail-closed. Restore the
  correct secret/key and replay failed events from Stripe; idempotency prevents a
  second grant.
- Paid balance missing: inspect the signed Stripe delivery, `checkoutGrants`,
  and the hashed `purchaseClaims` record, then replay the same provider event.
  Do not grant manually; the idempotent claim must remain the only grant path.
- Firestore migration: stop. v2 is the first deployed schema, but any future
  schema change must inventory live documents and include a tested rollback.
- Account deletion failure: preserve the signed-in account so deletion can be
  retried; do not manually delete only Firebase Auth and strand provider data.

## 14. What still requires live human testing

Infrastructure and configuration are real and active, but these checks cannot
be truthfully replaced with setup screens or unit tests:

- A real Stripe Checkout payment, including a delayed-payment success path.
- An Android internal-track run proving managed balance spend and Play Integrity.
  If external Stripe checkout is approved and enabled, test the Custom Tab return
  and exact grant; otherwise prove that the Android buy action is fail-closed.
- Full account deletion across Firebase, Stripe and remote Gemini files.
- Representative production load and cost observation.
- A signed Android build containing the new Firebase config, followed by a
  closed-track release; production access remains gated by Play testers.

Record the date, test user, provider event/order identifier, result and cleanup
for each live test without recording tokens or secrets.

## 15. Historical schema statement

Managed-mode v1 existed only in source code. It was never configured or
deployed, and it created no users, balances, purchases, reservations, files,
ledgers or other production data. Managed v2 is therefore the first live
schema. There is no v1 migration or backfill. Legacy deletion names remain only
as defensive cleanup code, not evidence of a deployed v1 system.

## 16. Maintainer handoff checklist

Before handing ownership to another maintainer:

- Confirm they can access GitHub, Firebase/GCP, Play and the correct Stripe
  account using their own identity.
- Confirm secrets remain in Secret Manager and keystore credentials remain in
  the password manager.
- Run the validation suite and health checks.
- Review live App Check metrics, webhook health, scheduler status and the Play
  tester gate.
- Record any active incident, temporary rollback or pending provider permission.
- Remove access for maintainers who no longer need it; do not share accounts.
