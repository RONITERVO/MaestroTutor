# New maintainer setup and Play Console guide

This runbook is written for a beginner maintainer who has the same repository,
Firebase/GCP, Stripe, GitHub and Play Console permissions as the owner but no
project history. Follow it in order. Never paste secret values into an issue,
commit, terminal transcript or chat.

Billing is Stripe-only. Google Play is used for Android distribution and Play
Integrity App Check, not for credit purchases. Read
[`STRIPE_ONLY_BILLING.md`](./STRIPE_ONLY_BILLING.md) before changing any payment or
Android checkout setting.

## 1. Install local prerequisites

- Git and GitHub authentication.
- Node.js 22 and npm.
- JDK 21 for Firestore Emulator and Android validation. Android source remains
  Java 17.
- Android Studio plus the SDK version declared by the Gradle project.
- Firebase CLI from the Functions package (`npm --prefix functions ci` installs
  the pinned `firebase-tools`).
- Chrome or Edge for provider-hosted test journeys.
- `gcloud` only when rotating Secret Manager values or inspecting deployed state.

```powershell
git clone <repository URL>
cd MaestroTutor
npm ci
npm --prefix functions ci
npm run verify:release-config
npm test
npm --prefix functions test
```

Do not globally install a different Firebase CLI and assume CI uses it. The
Functions package pins the supported version.

## 2. Know the environments

| Purpose | Firebase project | Hosted app |
| --- | --- | --- |
| Production | `chatwithmaestro` | `https://chatwithmaestro.web.app` and the production custom domain |
| Staging | `chatwithmaestro-staging` | `https://chatwithmaestro-staging.web.app` |

The checked-in `.firebaserc` aliases are `default` (production
`chatwithmaestro`) and `staging` (`chatwithmaestro-staging`). Always include
`--project staging` for staging or `--project chatwithmaestro` / `--project
default` for production. There is no `production` alias. Never infer the target
from the currently selected Firebase CLI project.

Tracked dotenv files contain public client configuration and examples. Ignored
local dotenv/secret files may contain environment-specific values. Confirm with
`git status` before every commit; secrets must remain ignored.

## 3. Access checklist

Ask the owner to verify, without sharing credentials:

- repository write and Actions settings access;
- Firebase/GCP access to both named projects;
- permission to view/deploy Cloud Functions, Hosting, Firestore rules/indexes,
  Authentication and App Check;
- permission to add Secret Manager versions;
- Stripe Developer access to **Maestro Chat And Learn** in the needed mode;
- Play Console access to the Maestro app, testing tracks, App Integrity, users and
  permissions;
- access to the release signing process without receiving a keystore password in
  chat or source control.

Use personal accounts and MFA. Do not share an owner's browser session or API key.

## 4. Firebase public client configuration

The web app values belong in `.env` / `.env.staging`; they are identifiers, not
server credentials:

- Firebase API key, auth domain, project id, storage bucket, sender id and app id;
- Functions region and backend URL;
- reCAPTCHA Enterprise App Check site key;
- Google web/server client ids;
- `VITE_MANAGED_CREDIT_PACK_IDS`;
- `VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED`.

Production normally sets the Android external checkout flag to `false`. Staging
may set it to `true` for internal adapter testing. Do not change production based
only on a successful staging browser test; complete the policy gate in the billing
runbook.

## 5. Firebase Authentication

The product UI signs in with Google. Configure the Google provider and exact
authorized domains in each Firebase project. The headless staging workflow uses one
dedicated email/password user because Google rejects automated browser sign-in;
email/password must not be exposed as a product sign-in option.

For a new staging CI identity:

1. create a disposable Firebase Authentication user with a generated password;
2. store email/password as GitHub Actions secrets and staging Secret Manager values;
3. run the headless auth/account step;
4. rotate the password if it appeared in any log;
5. never use a production end-user identity for automation.

The periodic Google popup proof is manual:

```powershell
npm run maestro -- auth.google.verifyHosted --profile google-release --params '{"headless":false,"timeoutMs":240000}'
```

Complete Google login/MFA in the normal browser window, then close it. The harness
only verifies the resulting app session.

## 6. Web App Check

Web uses a reCAPTCHA Enterprise key restricted to the exact environment domains.

1. Open reCAPTCHA Enterprise in the matching GCP project.
2. Verify the key is a website key and list only the intended production or staging
   domains.
3. Register that key with the matching Firebase web App Check app.
4. Deploy the client key as `VITE_FIREBASE_APPCHECK_SITE_KEY`.
5. Keep `REQUIRE_APPCHECK=true` on Functions.
6. For CI only, create a named staging debug token and store it as
   `HEADLESS_APPCHECK_DEBUG_TOKEN`; revoke old tokens after rotation.

A debug token is a secret. A reCAPTCHA site key and Firebase web API key are public
configuration. Do not confuse the two categories.

## 7. Android App Check and Play Integrity

Android uses Play Integrity for App Check.

1. In Firebase App Check, select the exact Android app/package.
2. Register the Play Integrity provider.
3. In Play Console, open **App signing** and copy both the **App signing key
   certificate** and **Upload key certificate** SHA-1 and SHA-256 fingerprints.
   These are separate identities.
4. From the repository, run `cd android` followed by
   `.\gradlew.bat signingReport` on Windows (`./gradlew signingReport` on macOS
   or Linux), then record the debug variant's SHA-1 and SHA-256 fingerprints.
5. In Firebase project settings, register the Play app-signing, upload and debug
   SHA-1 fingerprints for native Google sign-in, and all three SHA-256
   fingerprints for App Check.
6. Download the refreshed `google-services.json` after adding a SHA-1 fingerprint
   and replace `android/app/google-services.json` before building.
7. In Play Console / Google Cloud, accept the Play Integrity/API terms if prompted.
8. Upload/install a correctly signed internal-track build and verify protected API
   calls succeed.
9. Confirm a build signed with an unknown certificate fails closed.

Play Integrity attests the app. It does not enable Stripe checkout or satisfy Google
Play payment-program rules.

## 8. Stripe

Follow [`STRIPE_ONLY_BILLING.md`](./STRIPE_ONLY_BILLING.md). The required durable
provider configuration is:

- production/live and staging/test restricted API keys in their matching GCP
  projects;
- a deployed webhook endpoint subscribed to exactly
  `checkout.session.completed` and
  `checkout.session.async_payment_succeeded`;
- the revealed signing secret stored as `STRIPE_WEBHOOK_SECRET`;
- `MANAGED_CREDIT_PACKS=id:credits:cents` and identical client pack ids;
- exactly-once proof through `billing.checkout.completeTest` in staging.

Never use the Checkout redirect as payment proof. Never add credits manually to
make a smoke test green.

## 9. GitHub Actions configuration

Public repository variables:

- `MAESTRO_BACKEND_BASE_URL`
- `MAESTRO_FIREBASE_API_KEY`
- `MAESTRO_FIREBASE_APP_ID`
- `MAESTRO_TEST_PACK_ID`
- `MAESTRO_TEST_PACK_CREDITS`

Repository secrets:

- `HEADLESS_FIREBASE_EMAIL`
- `HEADLESS_FIREBASE_PASSWORD`
- `HEADLESS_APPCHECK_DEBUG_TOKEN`

For a local managed staging run, create the ignored
`.env.headless.staging.local` yourself and obtain the three secret values through
the approved secret manager. Do not commit or paste the file:

```dotenv
MAESTRO_BACKEND_BASE_URL=https://europe-west1-chatwithmaestro-staging.cloudfunctions.net/api
MAESTRO_FIREBASE_API_KEY=<public staging web API key>
MAESTRO_FIREBASE_APP_ID=<public staging web app id>
MAESTRO_FIREBASE_EMAIL=<dedicated staging automation user>
MAESTRO_FIREBASE_PASSWORD=<secret>
MAESTRO_APPCHECK_DEBUG_TOKEN=<secret>
```

Load it only through the CLI's supported flag:

```powershell
npm run maestro -- auth.signIn --env-file .env.headless.staging.local --profile release-smoke
npm run maestro -- account.summary --env-file .env.headless.staging.local --profile release-smoke
```

The `Release gate` runs app tests/lint/build, Functions tests, Firestore billing
invariants and the static single-provider verifier. The weekly/manual `Headless
staging journey` performs the controlled Stripe-first real-provider sequence. Both
must be green before a production release.

## 10. Deploy staging

```powershell
npm ci
npm --prefix functions ci
npm test
npm run lint
npm run build:staging
npm --prefix functions test
npm --prefix functions run test:emulator
npm --prefix functions exec firebase -- deploy --config firebase.json --project staging --only functions,firestore:rules,firestore:indexes,hosting
```

After deployment:

```powershell
Invoke-RestMethod https://europe-west1-chatwithmaestro-staging.cloudfunctions.net/api/health
npm run maestro -- auth.signIn --env-file .env.headless.staging.local --profile release-smoke
npm run maestro -- billing.checkout.completeTest --env-file .env.headless.staging.local --profile release-smoke --params '{"packId":"pack_1000","expectedCredits":1000,"headless":true}'
```

Dispatch `Headless staging journey`. Do not substitute a local emulator result for
the Stripe/Gemini/App Check provider proof.

## 11. Build and validate Android

1. Confirm production uses the production `google-services.json` and package;
   staging must use a real staging flavor/app, not a hand-swapped file.
2. Confirm `VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED` matches the release
   ticket's policy decision.
3. Build/sync the web assets and Capacitor project.
4. Compile and run Android tests.
5. Install a signed internal-track artifact and verify sign-in, App Check, shared
   balance and normal Gemini use.
6. If external checkout is approved/enabled, verify Custom Tab open/close,
   Stripe test purchase in an eligible test context, exactly one grant and balance
   refresh. Otherwise verify the buy action is absent/unavailable and web-purchased
   credits still work in the app.

```powershell
npm run cap:android
Push-Location android
.\gradlew.bat :app:testDebugUnitTest :app:assembleDebug :app:lintDebug --no-daemon
Pop-Location
```

There is deliberately no Android Billing Client dependency and no managed purchase
plugin.

## 12. Production release

1. Freeze the candidate commit and record its SHA.
2. Require independent review and green required checks.
3. Confirm production Secret Manager versions by name/version, never by printing
   values.
4. Confirm the live Stripe webhook destination and its two event types.
5. Confirm App Check web/Android providers and enforcement.
6. Confirm the Android external-checkout flag and policy evidence.
7. Deploy Functions/rules/indexes before Hosting so the client never targets an
   older contract.
8. Run health, auth/account and a bounded live-mode validation that does not use a
   test card. Use a real low-value purchase only when the release plan explicitly
   authorizes it, then reconcile/refund through normal Stripe operations.
9. Record deployment revisions, provider changes, workflow links and any rollback
   decision in the release ticket.

## 13. Common failures

Google says the browser/app is unsafe:

- do not automate the Google login page;
- use the normal-browser handoff and complete login/MFA manually;
- verify OAuth client ids, package and SHA fingerprints.

App Check 401/403:

- verify project/app/key alignment and exact domain or signing certificate;
- verify the staging debug token belongs to the staging web app;
- do not disable enforcement as a normal fix.

Stripe checkout works but no credits arrive:

- inspect webhook delivery/signature, session `payment_status` and checkout grant;
- verify the secret is revealed and belongs to this exact endpoint/mode;
- redeliver the same signed event after repair; do not create another grant path.

Android buy button is unavailable:

- check the explicit external-checkout flag and release policy evidence;
- this is expected for a fail-closed production build without enrollment;
- do not restore Play Billing or an unreviewed WebView checkout.

Firestore emulator fails before tests:

- select JDK 21 and use the Functions package's pinned Firebase CLI.

## 14. Handoff record

Before leaving a release or provider change, write down:

- commit SHA and branch;
- Firebase projects and deployed revisions;
- Secret Manager version numbers changed (never values);
- App Check providers/domains/fingerprints reviewed;
- Stripe mode, endpoint id and subscribed event types;
- Android external-checkout policy decision/evidence;
- required workflow URLs and results;
- remaining blockers and the exact owner of each.

That record lets the next maintainer continue without guessing or receiving secret
material from the previous maintainer.
