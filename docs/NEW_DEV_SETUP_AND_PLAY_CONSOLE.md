# Maestro Tutor - Managed Android Release Guide

This guide is the release handoff for the current codebase.

The app now supports two access modes:

- `BYOK`: user pastes their own Gemini API key and talks directly to Gemini.
- `Managed`: user signs in with Firebase Google auth, buys Maestro credits on Android through Google Play, and the app routes Gemini requests through the Firebase backend.

From the user perspective, BYOK still works as before. Managed mode sits next to it inside the same API key gate and header access button.

If you only want quick commands, also read [DEV_CHEATSHEET.md](./DEV_CHEATSHEET.md). If you only want the release checklist, also read [RELEASE_CHECKLIST.md](./RELEASE_CHECKLIST.md).

## 1. Current Release Shape

- Package id: `com.ronitervo.maestrotutor`
- Existing paid theme store remains in place and still uses permanent non-consumable unlocks.
- Managed credits use one re-buyable Google Play one-time product for this release:
  - `maestro_credits_1000`
- One purchase of `maestro_credits_1000` grants `1000` Maestro credits.
- Managed billing verifies Play purchases on the backend before credit grant.
- Managed billing reserves credits before Gemini calls and settles from actual `usageMetadata` when available.
- The API key gate now includes a local usage metadata ledger for both BYOK and managed requests:
  - text, translation, reply suggestions, and image generation record per-request token/image usage locally on-device
  - the ledger is an estimate from Gemini response metadata plus current public Gemini list pricing
  - BYOK users can still open Google Cloud billing from inside that ledger
- Failed or abandoned reservations are released automatically:
  - immediately on request failure when possible
  - periodically by a scheduled backend sweep
- Managed remote file deletion is user-scoped. Clearing uploads in the debug panel only clears the signed-in user's managed uploads.
- Managed uploads no longer require routine manual cleanup during normal chatting. The backend keeps at most `20` active managed files per user and evicts the oldest files automatically before new uploads.
- Managed live tokens are capped at `3` minutes and the backend allows at most `2` active managed live sockets per signed-in user at a time.
- Managed account deletion is now implemented both:
  - inside the app from the managed access panel
  - outside the app from the hosted `delete-account.html` page
- Generated AI output now has an in-app report / flag path on assistant messages.

## 2. Repo Areas That Matter

### Frontend and Android

- App shell and access gate: `src/app/`, `src/features/session/`
- Managed access UI: `src/features/session/components/ManagedAccessPanel.tsx`
- Outside-app deletion page: `delete-account.html`, `src/delete-account/`
- Integration config: `src/core/config/integrations.ts`
- Managed session storage: `src/core/security/managedAccessSessionStorage.ts`
- Service hub: `src/services/maestroServices.ts`
- Firebase client bridge: `src/services/firebase/maestroFirebaseService.ts`
- Google auth bridge: `src/services/auth/`
- Backend client: `src/services/backend/maestroBackendService.ts`
- Google Play billing wrapper: `src/services/payments/`
- Native billing manager/plugin:
  - `android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java`
  - `android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingPlugin.java`

### Backend

- Firebase Functions app: `functions/src/index.ts`
- Account deletion and AI-content reporting: `functions/src/account.ts`
- Auth and CORS: `functions/src/auth.ts`
- Env/config parsing: `functions/src/config.ts`
- Gemini proxy and managed files: `functions/src/gemini.ts`
- Managed billing ledger/reservations: `functions/src/managedBilling.ts`
- Google Play verification: `functions/src/playBilling.ts`
- Pricing conversion: `functions/src/pricing.ts`
- Firestore rules and indexes:
  - `firestore.rules`
  - `firestore.indexes.json`
  - `firebase.json`

### Static public assets

- Privacy policy: `public/privacy.html`
- Public model manifest: `public/gemini-models.json`
- Hosted outside-app deletion entrypoint: `delete-account.html`

Those two public files still need to be reachable from the deployed site, normally:

- `https://chatwithmaestro.com/privacy.html`
- `https://chatwithmaestro.com/gemini-models.json`
- `https://chatwithmaestro.com/delete-account.html`

## 3. Access You Need Before Doing Release Work

- Git access to this repository
- Firebase project admin access
- Google Cloud Console access for the same Firebase project
- Google Play Console access for the app
- Permission to manage Play API access / service accounts
- Access to the Android signing keystore and passwords
- A real Android device for Play billing validation

If you do not have Play Console access and Firebase project access, you cannot finish managed mode release work.

## 4. Local Toolchain

Required:

- Node.js 20.x
- npm
- Java 17
- Android Studio with current SDK / platform tools
- Firebase CLI
- Git

Verify:

```bash
node --version
npm --version
java -version
firebase --version
git --version
```

Important:

- Root web app currently builds with the repo's normal frontend toolchain.
- `functions/package.json` explicitly targets Node `20`.
- Android release builds use Java 17.

## 5. Clone and Install

```bash
git clone <repo-url>
cd maestrotutor
npm install
cd functions
npm install
cd ..
```

Useful first-pass checks:

```bash
npm run build
cd functions
npm run build
cd ..
```

## 6. Local Secret and Config Files

These files are expected locally and must not be committed:

- `.env`
- `functions/.env`
- `android/app/google-services.json`
- `android/keystore.properties`
- your upload keystore file

### Root `.env`

Create it from the example:

```bash
copy .env.example .env
```

Fill these values:

```env
VITE_FIREBASE_API_KEY=
VITE_FIREBASE_AUTH_DOMAIN=
VITE_FIREBASE_PROJECT_ID=
VITE_FIREBASE_STORAGE_BUCKET=
VITE_FIREBASE_MESSAGING_SENDER_ID=
VITE_FIREBASE_APP_ID=
VITE_FIREBASE_MEASUREMENT_ID=
VITE_FIREBASE_FUNCTIONS_REGION=europe-west1
VITE_BACKEND_BASE_URL=https://europe-west1-your-project-id.cloudfunctions.net/api/
VITE_GOOGLE_WEB_CLIENT_ID=
VITE_GOOGLE_SERVER_CLIENT_ID=
VITE_GOOGLE_PLAY_PACKAGE_NAME=com.ronitervo.maestrotutor
VITE_MANAGED_BILLING_PRODUCT_IDS=maestro_credits_1000
VITE_FIREBASE_APPCHECK_SITE_KEY=
VITE_FIREBASE_APPCHECK_DEBUG_TOKEN=
```

Notes:

- `VITE_BACKEND_BASE_URL` must include the trailing `/api/`.
- Leave App Check values empty unless web App Check is actually being enabled.

### `functions/.env`

Create it from the example:

```bash
copy functions\.env.example functions\.env
```

Fill these values:

```env
FUNCTION_REGION=europe-west1
FIREBASE_PROJECT_ID=
GEMINI_API_KEY=
GOOGLE_PLAY_PACKAGE_NAME=com.ronitervo.maestrotutor
ALLOWED_ORIGINS=https://chatwithmaestro.com,https://chatwithmaestro.web.app,https://localhost,http://localhost,capacitor://localhost
MANAGED_CREDIT_PRODUCTS=maestro_credits_1000:1000
MANAGED_CREDITS_PER_USD=1000
REQUIRE_APPCHECK=false
GEMINI_LIVE_TOKEN_USES=1
MANAGED_MAX_ACTIVE_FILES_PER_USER=20
MANAGED_UPLOAD_CREDITS_PER_MB=10
MANAGED_MAX_UPLOAD_BYTES=52428800
RESERVATION_TTL_MINUTES=30
MANAGED_LIVE_TOKEN_LIFETIME_SECONDS=180
MANAGED_MAX_ACTIVE_LIVE_SOCKETS=2
MANAGED_MUSIC_SESSION_CREDITS=120
```

Notes:

- `GEMINI_API_KEY` is the backend-managed Gemini key from Google AI Studio.
- Keep the localhost origins. Android WebView requests go through localhost and will fail without them.
- `REQUIRE_APPCHECK=false` is the safe default until web App Check is configured and verified.
- Once `REQUIRE_APPCHECK=true`, every authenticated backend request must send a valid App Check token.
- The managed upload guardrails are controlled by `MANAGED_MAX_ACTIVE_FILES_PER_USER`, `MANAGED_UPLOAD_CREDITS_PER_MB`, and `MANAGED_MAX_UPLOAD_BYTES`.
- The managed live guardrails are controlled by `MANAGED_LIVE_TOKEN_LIFETIME_SECONDS` and `MANAGED_MAX_ACTIVE_LIVE_SOCKETS`.
- The current backend clamps managed live token lifetime to `180` seconds max and active managed live sockets to `2` max even if someone sets larger env values.

## 7. Architecture Rules You Must Preserve

### BYOK and managed must stay parallel

- If the user has a BYOK key, Gemini calls go direct from the client.
- If the user has managed credits and no BYOK key, Gemini calls go through the backend.
- The gate should not force one mode to overwrite the other.

### Managed backend is a Gemini request proxy, not a second app-specific API

The frontend now builds Gemini-shaped requests and sends the same `model`, `contents`, and `config` structures to the backend. Do not reintroduce a custom parallel request contract for managed mode.

### Theme purchases and managed credits are different product classes

- Themes:
  - permanent unlocks
  - acknowledged
  - restored from Play
  - never consumed
- Managed credits:
  - one-time product
  - verified server-side
  - consumed after successful verification so the same SKU can be bought again

Do not collapse these two behaviors into one purchase flow.

### Managed storage is shared infrastructure

The managed backend uses one backend Gemini key for all users. Because of that:

- file ownership must stay user-scoped
- purchase verification must stay server-side
- reservations must stay recoverable

The current implementation already enforces those guardrails. Do not remove them.

## 8. Firebase Setup

Create or reuse the Firebase project that will host auth, Firestore, and functions.

### Firebase console setup

1. Enable `Authentication -> Google`.
2. Create Firestore.
3. Upgrade the project to Blaze.
4. Add allowed auth domains at least for:
   - `chatwithmaestro.com`
   - `chatwithmaestro.web.app`
   - `localhost`
5. Add a web app and copy the Firebase web config into root `.env`.
6. Add an Android app with package id `com.ronitervo.maestrotutor`.
7. Download `google-services.json` and place it at:

```text
android/app/google-services.json
```

### Android SHA fingerprints

Firebase Google sign-in requires both debug and release SHA fingerprints on the Android Firebase app.

Get the debug SHA-1:

```bash
keytool -list -v -keystore "%USERPROFILE%\.android\debug.keystore" -alias androiddebugkey -storepass android
```

Get the release/upload SHA-1 from your upload keystore:

```bash
keytool -list -v -keystore <your-upload-keystore>.jks -alias <your-alias>
```

After adding new SHA fingerprints in Firebase Console, re-download `android/app/google-services.json`.

## 9. Firebase App Check

Current intended production default:

- backend env: `REQUIRE_APPCHECK=false`
- frontend env: App Check values empty

If you later enable App Check:

1. Configure App Check in Firebase Console for the web app and Android app.
2. For Android, enable the Play Integrity provider in Firebase App Check.
3. Set `VITE_FIREBASE_APPCHECK_SITE_KEY` for web.
4. Optionally set `VITE_FIREBASE_APPCHECK_DEBUG_TOKEN` for development.
5. Set `REQUIRE_APPCHECK=true` only after both web and Android managed traffic are confirmed working.

What is already implemented:

- web frontend will attach `X-Firebase-AppCheck` automatically when configured
- Android native builds initialize `@capacitor-firebase/app-check` and request native App Check tokens
- backend verifies the token when enforcement is enabled

What is not implemented:

- Firebase Console setup for your production Android App Check provider
- release validation that both web and Android managed traffic still pass once enforcement is on

So do not enable backend enforcement until that Firebase-side setup has been completed and tested.

## 10. Backend Deployment

Login and select the right project:

```bash
firebase login
firebase use --add
```

Deploy the backend:

```bash
firebase deploy --only functions,firestore:rules,firestore:indexes
```

Health check:

```text
GET https://<region>-<project-id>.cloudfunctions.net/api/health
```

Expected backend surface:

- `GET /health`
- `GET /auth/session`
- `GET /account/summary`
- `GET /account/usage-ledger`
- `GET /account/billing-ledger`
- `POST /billing/google-play/verify`
- `POST /gemini/generate-content`
- `POST /gemini/generate-content-stream`
- `POST /gemini/upload-media`
- `POST /gemini/file-statuses`
- `POST /gemini/delete-file`
- `POST /gemini/clear-files`
- `POST /gemini/live-token`
- `POST /gemini/live-token/release`

Important Firestore collections created by the backend:

- `users/{uid}/account/summary`
- `users/{uid}/billingLedger/{entryId}`
- `users/{uid}/usageLedger/{entryId}`
- `users/{uid}/entitlements/{entitlementId}`
- `googlePlayPurchases/{purchaseToken}`
- `managedReservations/{reservationId}`
- `managedFiles/{fileId}`

## 11. Google Play Purchase Verification Setup

The backend uses Application Default Credentials on Firebase Functions. It does not load a local JSON key file.

That means purchase verification will only work if the service account used by the deployed backend has Play Developer API access.

### What to do

1. Deploy the backend once.
2. In Google Cloud Console, identify the service account used by the deployed `api` function.
3. In Play Console `API access` / `Users and permissions`, link that service account.
4. Grant it app access for `com.ronitervo.maestrotutor`.
5. Give it the permissions needed to read one-time product purchases.

If the wrong service account is linked, `/billing/google-play/verify` will fail even though the app build itself is fine.

## 12. Google Play Console Setup

### App entry

Use or create the Play app for:

```text
com.ronitervo.maestrotutor
```

### Billing catalog

This release has two billing families:

#### Existing themes

- Keep the existing theme SKUs exactly as defined in:
  - `src/features/theme/config/themeProducts.ts`
  - `android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java`
- These remain permanent non-consumable unlocks.

#### Managed credits

Create exactly one Play one-time product for this release:

| Product ID | Purpose | Credit grant |
| --- | --- | --- |
| `maestro_credits_1000` | Managed Maestro credits | `1000` |

Important:

- Do not rename the SKU after release.
- Do not create subscriptions for this flow.
- Do not convert theme SKUs into consumables.
- The app consumes the managed credit purchase after backend verification, so users can rebuy the same SKU.

## 13. Android Signing

Release builds require `android/keystore.properties`.

Create the upload keystore if it does not already exist:

```bash
cd android
keytool -genkeypair -v -keystore release-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload
cd ..
```

Create:

```bash
copy android\keystore.properties.example android\keystore.properties
```

Fill it with the real keystore path and passwords. Never commit the keystore or `android/keystore.properties`.

## 14. Local Android Build Flow

Build and sync Android:

```bash
npm run build:android
```

Open Android Studio if needed:

```bash
npm run cap:open:android
```

Build the release bundle:

```bash
npm run build:aab
```

Output:

```text
android/app/build/outputs/bundle/release/app-release.aab
```

For local release smoke testing:

```bash
cd android
.\gradlew assembleRelease
cd ..
```

APK output:

```text
android/app/build/outputs/apk/release/app-release.apk
```

## 15. Managed Smoke Test Matrix

Use a real Android device and an internal testing install from Google Play.

### BYOK regression

1. Fresh install.
2. Enter a Gemini API key.
3. Verify ordinary text chat still works.
4. Verify image generation still works.
5. Verify the API key gate usage chip opens the in-app usage ledger instead of jumping straight out of the app.
6. Verify the ledger shows local rows for text / translation / image requests and still offers the Google Cloud billing link for BYOK.
7. Verify live mode still works.
8. Keep a managed live session open for more than 3 minutes and verify the app silently reconnects and continues without a manual retry.
9. Verify existing theme store still works.

### Managed access

1. Fresh install with no BYOK key.
2. Open the gate and sign in with Google.
3. Verify the managed panel shows signed-in user info.
4. Verify the `maestro_credits_1000` product loads with price.
5. Buy the product.
6. Verify backend credit grant happens.
7. Verify the purchase is consumed and the same SKU can be bought again later.
8. Kill and reopen the app.
9. Verify managed session restores.
10. Run a managed text request.
11. Run a managed streaming text request.
12. Run a managed translation request.
13. Run a managed image generation request.
14. Verify credit deductions appear in billing summary and ledgers.
15. Trigger `Restore purchases` and verify nothing double-grants.

### Theme regression after managed changes

1. Buy an existing theme SKU.
2. Verify it stays permanently unlocked.
3. Verify restore still resolves ownership.
4. Verify theme SKUs were not consumed.

### Failure-path checks

1. Cancel a Play purchase.
2. Try a pending purchase flow if available.
3. Verify failed managed requests release or later recover reserved credits.
4. Verify ordinary chatting/upload flows do not require manual "clear uploads" usage because the backend evicts old managed files automatically.
5. Verify debug-panel "clear uploads" only clears the current managed user's uploads when you intentionally use it.

## 16. Play Internal Testing and License Testers

Set up both:

- internal testing track
- license testers

Why:

- internal testing gives the real Play installation path
- license testers give Play test payment methods

Do not trust sideload-only billing tests for the final release decision.

## 17. Play Review and Store Listing

This app still supports BYOK and can still be gated on first launch if the reviewer has no managed credits.

For review, the safest path is still to provide a temporary BYOK Gemini API key in Play Console `App access`.

Suggested review text:

```text
This app supports two modes:
1. Bring-your-own Gemini API key
2. Managed sign-in plus Google Play credit purchases

For Play review, use this temporary Gemini API key to get through the access gate immediately:
<paste temporary key here>

Android managed purchases are also available in the signed-in managed access panel.
```

Keep Play Console metadata aligned with the current app behavior:

- Firebase Google sign-in exists
- server-side Gemini requests exist in managed mode
- Firestore billing and usage ledgers exist
- Google Play purchase verification exists
- BYOK still exists
- the API key gate also shows a local usage metadata ledger for client-visible spend inspection

If privacy or data handling changes, update:

- Play Data Safety
- hosted privacy policy
- app access instructions

### Play Console claims that must match this build

Safe claims for the current codebase:

- BYOK still exists and sends the user's Gemini prompts and attachments directly from the device to Google Gemini.
- Managed mode uses Firebase Google sign-in.
- Managed mode routes prompts, replies, images, audio, video, and file uploads through your Firebase backend and then to Google Gemini.
- Managed mode stores billing summaries, usage ledgers, entitlement records, reservation records, and Google Play purchase verification records in Firebase/Firestore.
- Google Play handles Android managed credit payments.
- The app processes Google Play purchase metadata such as purchase token, product id, order id, purchase time, purchase state, and package name for purchase verification.
- The API key gate includes a local on-device usage metadata ledger for user-visible spend inspection.
- Managed users can delete their account from inside the app.
- A dedicated outside-app account deletion web resource exists at the hosted `delete-account.html` page.
- Assistant-generated output has an in-app report / flag action.
- No in-app ads are included in this release.
- No developer-operated analytics SDK or crash reporting SDK is included in this release.

Do not claim these things unless you actually implement them first:

- ads, analytics, or crash reporting if those SDKs are still absent

### Google Play policy areas that still require release-side verification

The code now includes the required product behaviors, but you still need to verify them on the real shipped build before answering Play Console questions:

- Managed sign-in creates an account path, so verify both deletion paths really work on the build you upload:
  - in-app managed account deletion from the managed access panel
  - outside-app deletion at the hosted `delete-account.html` page
- The app generates AI content, so verify the in-app report / flag action is visible on assistant output and successfully submits a report through the backend.

### What to update in Play Console for this release

- App access: keep the BYOK review instructions current and provide a temporary reviewer key if you still want the reviewer to bypass the gate quickly.
- Data Safety: answer based on the actual shipped build, including Firebase Google sign-in, managed backend Gemini processing, Firestore ledger storage, and Google Play purchase verification metadata.
- Data deletion questions: answer based on the shipped deletion flows. This build now supports:
  - in-app managed account deletion
  - outside-app deletion at `https://chatwithmaestro.com/delete-account.html`
- Privacy policy URL: point to the hosted `privacy.html` that matches the current BYOK plus managed behavior.
- AI-generated content reporting: answer based on the in-app report / flag flow now present on assistant output.
- Support contact: make sure the Play listing support contact is real and monitored as a fallback for deletion requests and offensive-content reports.

## 18. Static Site Deployment

If you change either of these files:

- `public/privacy.html`
- `public/gemini-models.json`

redeploy the static site:

```bash
npm run deploy
```

Then verify:

- `https://chatwithmaestro.com/privacy.html`
- `https://chatwithmaestro.com/gemini-models.json`
- `https://chatwithmaestro.com/delete-account.html`

Official references used for this section:

- Data Safety: https://support.google.com/googleplay/android-developer/answer/10787469?hl=en
- Account deletion requirements: https://support.google.com/googleplay/android-developer/answer/13327111?hl=en
- Developer Program Policy, AI-generated content: https://support.google.com/googleplay/android-developer/answer/16944162?hl=en

## 19. Maintenance Rules

- Do not change the package id casually.
- Do not rename released Play product ids.
- Do not remove localhost origins from `functions/.env`.
- Do not replace server-side Play verification with client-side trust.
- Do not remove reservation sweeping.
- Do not make managed file deletion global.
- Do not regress BYOK behavior while working on managed mode.

## 20. Troubleshooting

### Managed sign-in fails on Android

Check:

- Firebase Google provider is enabled
- debug SHA-1 is added
- release SHA-1 is added
- `android/app/google-services.json` is current
- Firebase config values are filled in `.env`

### Product price or product title does not load

Check:

- Play app package id matches `com.ronitervo.maestrotutor`
- SKU is active in Play Console
- tester is installed through Play or is a license tester
- `VITE_MANAGED_BILLING_PRODUCT_IDS` includes `maestro_credits_1000`

### Purchase succeeds but credits do not appear

Check:

- backend was deployed
- backend service account has Play API access
- `GOOGLE_PLAY_PACKAGE_NAME` matches the app
- `MANAGED_CREDIT_PRODUCTS=maestro_credits_1000:1000`
- purchase token is not already linked to another user

### User cannot rebuy the credit pack

Check:

- backend verification succeeded
- the app consumed the purchase after verification
- the managed purchase is not being treated like a theme SKU

### Credits stay stuck in reserved state

Check:

- scheduled function `releaseExpiredReservations` deployed successfully
- `RESERVATION_TTL_MINUTES` is sane
- the backend has permission to write Firestore

### Account deletion or report flow fails

Check:

- the static site deploy includes `delete-account.html`
- Firebase web config in `.env` is correct
- backend is reachable from the hosted site
- Google sign-in works on the hosted deletion page
- the uploaded build still shows the report / flag action on assistant messages

### Android backend calls fail with CORS

Check:

- `functions/.env` keeps:
  - `http://localhost`
  - `https://localhost`
  - `capacitor://localhost`
  in `ALLOWED_ORIGINS`

## 21. Final Pre-Release Checklist

Before promoting any build beyond internal testing, all of these should be true:

- `.env` is filled correctly
- `functions/.env` is filled correctly
- `android/app/google-services.json` exists
- `android/keystore.properties` exists
- upload keystore is available
- Firebase Google sign-in works
- Firestore exists
- Blaze is enabled
- backend deploy succeeded
- backend service account has Play API access
- Play credit SKU is active
- internal purchase verification works
- credit pack can be rebought
- theme purchases still behave as permanent unlocks
- BYOK still behaves as before
- privacy policy URL is live
- `gemini-models.json` is live on the public site
- `delete-account.html` is live on the public site
- managed account deletion works from inside the app
- managed account deletion works from the hosted deletion page
- generated AI output can be reported from inside the app
