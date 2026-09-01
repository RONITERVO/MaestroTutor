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
| Play consumable | `maestro_credits_1000` — 1,000 credits, EUR 2.99 |
| Stripe account | `Maestro Chat And Learn` (`acct_1Rui2P1dnAf99VGW`) |
| Stripe webhook destination | `maestro-managed-billing` (`we_1UAmNP1dnAf99VGWLBHM6tdC`) |
| Stripe webhook URL | `https://europe-west1-chatwithmaestro.cloudfunctions.net/api/billing/stripe/webhook` |

The API is a Node.js 24, second-generation Cloud Function. Two scheduled
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
`paymentevents-maestro` predate this setup. They were intentionally preserved.
Do not delete them until their owners and consumers are identified.

Official reference: [Stripe webhooks](https://docs.stripe.com/webhooks).

### Google Play

`androidpublisher.googleapis.com` is enabled. The functions runtime service
account `47084692464-compute@developer.gserviceaccount.com` is enabled in Play
Console, scoped only to Maestro, and has the minimum billing permissions:

- View financial data, which includes Purchases API access.
- Manage orders.

The older `play-billing-verifier@ideatesvg.iam.gserviceaccount.com` account was
preserved. Do not remove it until its ownership and use are confirmed. New Play
permissions can take time to propagate, so do not immediately broaden access if
the first verification call fails.

Google no longer requires linking the Play developer account to a Cloud
project. The required setup is enabling the Android Publisher API and granting
the Play Console user permissions described above. See Google's
[Android Publisher API setup](https://developers.google.com/android-publisher/getting_started).

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

The checked-in Gemini 3.6 Flash Standard rate was verified on 2026-09-01. Its
current promotional input/output/cache rates expire after 2026-12-31 according
to the provider pricing page. Re-verify and deploy the registry before
2027-01-01; leaving an expired rate in production is a billing incident.
Grounded Gemini 3 requests are settled per reported Search query at list price,
with ten queries reserved before the call by default.

Active release blocker recorded on 2026-09-01: the production Gemini key passes
`countTokens`, but a minimal paid generation returns HTTP 429 because the AI
Studio project's prepayment credits are depleted. Do not deploy or advertise
the managed client until the owner restores the provider balance and the smoke
test below passes. Remove this incident note only after recording the successful
retest; a configured secret is not evidence that provider billing is usable.

## 3. Access a maintainer needs

A maintainer can complete the entire runbook with:

- Write access to the GitHub repository and permission to publish `gh-pages`.
- Firebase/GCP access to project `chatwithmaestro`, including Functions,
  Firestore, App Check, Secret Manager, Cloud Scheduler and billing visibility.
- Play Console permission to manage the Maestro app's products, test tracks,
  users and orders.
- Stripe access to the `Maestro Chat And Learn` live account, including
  Developers/Webhooks and restricted keys.
- Access to the Android release keystore and its passwords through the team's
  approved password manager. Never send these in chat.

Use the smallest provider permission set that permits the task. Do not create
downloadable service-account JSON keys for the backend: deployed functions use
their runtime identity.

## 4. Prepare a workstation

Install Node.js 24, npm, Git, JDK 21, Android Studio/SDK, Google Cloud CLI and
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
- Keep `.firebaserc` pointed at `chatwithmaestro` before deploying.

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
without a successful generation is not a pass; depleted prepayment currently
has exactly that failure shape.

Pull requests and pushes to the release branches run the same release gate in
`.github/workflows/release-gate.yml` on Node 24 and JDK 21. Do not merge a red
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
   backend URL, Google client IDs and `maestro_credits_1000`.
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

For an end-to-end release test, make one low-value real Checkout purchase with
a dedicated test user. Confirm exactly one billing ledger grant, the expected
credit balance, and a successful webhook delivery. Also test a delayed-payment
method when available; `checkout.session.completed` may be unpaid and must not
grant until `checkout.session.async_payment_succeeded` arrives. Refund or
otherwise reconcile the test purchase according to the team's accounting
process.

## 10. Google Play billing maintenance

The store product ID, app client configuration and server catalogue must match:

```text
Play:       maestro_credits_1000
Client:     VITE_MANAGED_BILLING_PRODUCT_IDS=maestro_credits_1000
Functions:  MANAGED_CREDIT_PACKS=pack_1000:1000:299:maestro_credits_1000
```

When changing price or adding a product, update the provider and both config
values in one release. Do not reuse a product ID for a different credit amount.

Before testing purchases, confirm the runtime service account still appears as
enabled under Play Console → Users and permissions and is scoped only to
Maestro. A successful test must prove: purchased state grants once, replaying
the token grants nothing, the server consumes only after granting, and a pending
purchase grants nothing. New purchases carry the SHA-256 Firebase UID as Play's
64-character `obfuscatedAccountId`; the server rejects a missing value or a
purchase bound to a different account before granting.

At setup time the app had only four joined closed-test users. Play's production
access page required at least 12 joined testers continuously for 14 days. This
is the current external production-release gate; always read the live Play
Console requirement because Google can change eligibility rules.

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
track, test Google sign-in, App Check, product loading, purchase verification,
restore/retry behavior and account deletion, then promote through Play Console.

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
- Play order/purchase-verification errors.
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
- Play verification failures: do not consume locally or grant manually. Restore
  API/permissions, then retry the same purchase token.
- Firestore migration: stop. v2 is the first deployed schema, but any future
  schema change must inventory live documents and include a tested rollback.
- Account deletion failure: preserve the signed-in account so deletion can be
  retried; do not manually delete only Firebase Auth and strand provider data.

## 14. What still requires live human testing

Infrastructure and configuration are real and active, but these checks cannot
be truthfully replaced with setup screens or unit tests:

- A real Stripe Checkout payment, including a delayed-payment success path.
- A Play licence-test purchase and replay/pending-purchase checks.
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
