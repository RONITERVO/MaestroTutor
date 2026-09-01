# Maestro Tutor — New Maintainer and Play Console Guide

This is the handoff guide for a maintainer who has repository, Firebase, Google
Cloud, Google Play Console, and Stripe access but has not worked on Maestro
before. Follow it in order. Never paste credentials, purchase tokens, service
account keys, or signing passwords into an issue, commit, chat, or test record.

For routine commands see [DEV_CHEATSHEET.md](./DEV_CHEATSHEET.md). For every
release use [RELEASE_CHECKLIST.md](./RELEASE_CHECKLIST.md). Production provider
configuration and incident procedures live in
[PRODUCTION_OPERATIONS.md](./PRODUCTION_OPERATIONS.md).

## 1. Architecture you must preserve

Maestro supports two access routes:

- **BYOK:** the user supplies a Gemini API key, which stays in device storage.
- **Managed access:** Firebase signs the user in; Maestro's backend meters
  requests and owns the credit balance and billing ledger.

Payments are account-managed products, not client entitlements:

```text
Web      -> backend creates Stripe Checkout -> signed webhook -> ledger grant
Android  -> native Play sheet -> raw token -> backend verifies with Play
         -> idempotent ledger grant -> backend consumes token
```

The browser and Android bridge never grant credits. Android never acknowledges
or consumes a managed purchase locally. A crash or network failure can therefore
retry the same token without either losing the paid benefit or granting twice.

Color themes are deliberately outside this system. **Every current and future
color theme is free, local, and usable offline.** The Theme Gallery keeps the
old card-and-swatch presentation, but it has no price, ownership, account,
storefront, purchase, or restore state. Do not create Play or Stripe products
for color themes.

## 2. Repository map

| Concern | Source of truth |
| --- | --- |
| Web app | `src/` |
| Managed client/network flows | `src/core-sdk/`, `src/services/` |
| Firebase API | `functions/src/` |
| Shared billing rules | `shared/` |
| Android wrapper | `android/` |
| Capacitor configuration | `capacitor.config.ts` |
| App package/version | `android/app/build.gradle` |
| Free theme metadata | `src/features/theme/config/themeCatalogue.ts` |
| Free theme preset mapping | `src/features/theme/config/themePresets.ts` |
| Theme palettes | `src/features/theme/config/themeColors.ts` |
| Theme Gallery UI | `src/features/theme/components/ThemeGalleryPanel.tsx` |
| Generic Play transport | `android/app/src/main/java/com/ronitervo/maestrotutor/ManagedBillingPlugin.java` |
| Server Play verification | `functions/src/playBilling.ts` |
| Server product catalogue | `MANAGED_CREDIT_PACKS` in `functions/.env` |

The Android package is `com.ronitervo.maestrotutor`. Changing it means creating
a different Play app and breaks the current App Check, signing, and billing
configuration. Do not change it as ordinary maintenance.

## 3. Access checklist

Before taking a release task, confirm you can access:

- the Git repository and its pull-request checks;
- the production and staging Firebase/Google Cloud projects;
- the Play Console app, testing tracks, orders, and users/permissions;
- the Stripe account and its test/live Workbench webhook destinations;
- the release keystore and its passwords through the team's approved secret
  storage, without moving them into the repository;
- one physical Android device and a Google account registered as a Play licence
  tester.

Read-only access is enough for diagnosis. A release, provider change, refund,
secret rotation, or permission change requires the matching administrative
permission. If access is missing, stop at the last read-only check and record
exactly which permission is needed.

## 4. Local setup

Install:

1. Current Node.js LTS.
2. Android Studio with the SDK, platform tools, and build tools used by the
   checked-in Gradle project.
3. JDK 21 to run the current Android Gradle plugin. Java source compatibility
   remains 17 in `android/app/build.gradle`.
4. Optional but useful: Firebase CLI, Google Cloud CLI, GitHub CLI, and `adb`.

Then:

```powershell
git clone <repository-url>
cd MaestroTutor
npm ci
Push-Location functions
npm ci
Pop-Location
npm run dev
```

Open the local URL printed by Vite. To sync the web build into Android:

```powershell
npm run cap:android
npm run cap:open:android
```

Use a root `.env` only for local client configuration. Use `functions/.env` for
non-secret backend configuration and the gitignored `functions/.secret.local`
only for emulator secrets. Production secrets belong in Google Secret Manager.

Never commit:

- `.env` or real `functions/.env` values;
- `functions/.secret.local`;
- `android/keystore.properties`;
- `*.jks`, `*.keystore`, service-account JSON, API keys, webhook secrets, or
  purchase tokens.

## 5. Android signing

The local release configuration reads `android/keystore.properties`. Create it
from `android/keystore.properties.example`; keep the keystore in the team's
backed-up secret storage. A one-time local keystore can be created with:

```powershell
keytool -genkeypair -v -keystore release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias maestro
```

The Play app already uses Play App Signing. Do not replace either the upload key
or app-signing key as a routine fix. If an upload key is lost or compromised,
use Play Console's documented reset process and preserve the existing app-signing
identity.

After any signing change, verify the SHA-256 certificates against:

- the Firebase Android app;
- Firebase Android App Check / Play Integrity registration;
- any Google sign-in configuration that lists certificate fingerprints.

Both release and intentionally supported debug fingerprints must remain present.

## 6. Firebase and App Check

The checked-in `android/app/google-services.json` must belong to the registered
Firebase Android app and contain the package above. The web configuration must
use the domain-restricted reCAPTCHA Enterprise key. Android App Check uses Play
Integrity.

Before a release:

1. Verify App Check enforcement and metrics in Firebase.
2. Confirm the production web domains remain allowed on the Enterprise key.
3. Confirm the Android app and its release/debug SHA-256 fingerprints remain
   registered for Play Integrity.
4. Run one signed Android request and one production-domain web request through
   the authenticated backend.
5. Confirm an invalid or missing App Check token is rejected.

Do not disable App Check as a normal troubleshooting step. The time-bounded
incident rollback and re-enable procedure is in `PRODUCTION_OPERATIONS.md`.

## 7. Managed products and configuration

The current Android consumable is configured in three places:

```text
Play Console: maestro_credits_1000
Client env:   VITE_MANAGED_BILLING_PRODUCT_IDS=maestro_credits_1000
Functions:    MANAGED_CREDIT_PACKS=pack_1000:1000:299:maestro_credits_1000
```

`MANAGED_CREDIT_PACKS` entries are:

```text
backendPackId:credits:priceInCents[:googlePlayProductId]
```

The Functions catalogue is authoritative for price, credits, and acceptable
provider products. The client product-ID list only tells Play which catalogue
records to display. The native bridge accepts IDs dynamically; there is no Java
product list to update.

### Add or change a paid product

This is a coordinated provider/backend release:

1. Decide a new immutable backend pack ID, Play product ID, credit amount, and
   price. Never reuse an existing ID for different value.
2. Create/activate the one-time consumable in Play Console. Use the console's
   current **one-time products** workflow; labels change over time.
3. Add the backend entry to `MANAGED_CREDIT_PACKS` in staging and production.
4. Add the Play ID to `VITE_MANAGED_BILLING_PRODUCT_IDS` for Android builds.
5. If the web offer changes, keep Stripe Checkout driven by the backend pack
   rather than adding price or entitlement decisions to React.
6. Add tests for product rejection, completed grant, replay/idempotency, pending
   state, wrong-account binding, and consume-after-grant order.
7. Deploy staging Functions and hosting; test both web and Android.
8. Release the provider config, backend, and client in a sequence that never
   advertises an unrecognized product.

For a future product type, first add a backend catalogue and authenticated
verification/grant route. The client may present the product and open the
provider sheet, but it must not create a local entitlement or independently
decide that payment succeeded.

## 8. Google Play Developer API permission

The Functions runtime service account calls the Android Publisher API. Verify:

1. `androidpublisher.googleapis.com` is enabled in the production Google Cloud
   project.
2. The runtime service-account email is an accepted user in Play Console
   **Users and permissions**, scoped to the Maestro app.
3. It has the minimum billing permissions needed to view orders/financial data
   and manage orders/subscriptions.
4. No downloadable service-account key is embedded in Functions. Workload
   identity/default runtime credentials are preferred.

Permission changes can take time to propagate. Do not broaden the account to
administrator merely to make a transient verification failure disappear.

Official reference: [Google Play Developer API setup](https://developers.google.com/android-publisher/getting_started).

## 9. Stripe setup

Web managed-credit purchases are created by the backend. Production must have a
live webhook destination pointing at the deployed `/billing/stripe/webhook`
endpoint and subscribing to both:

- `checkout.session.completed`
- `checkout.session.async_payment_succeeded`

The signing secret belongs in Secret Manager as `STRIPE_WEBHOOK_SECRET`; the
restricted live API key belongs in `STRIPE_SECRET`. Staging/test mode uses its
own keys, endpoint, and secret. Never share a signing secret across destinations
or copy a masked dashboard placeholder.

An unpaid `checkout.session.completed` event must grant nothing. A later
`checkout.session.async_payment_succeeded` for that session grants exactly once.
Both immediate and delayed events pass through the same fulfillment function.

## 10. Google Play test setup

Use both a Play testing track and licence testing:

1. Upload a signed AAB with a new `versionCode` to the internal or closed track.
2. Add the tester account to the track and have it accept the opt-in link.
3. Add the same account under Play Console licence testing.
4. Install the app from Google Play with that account. On a multi-account device,
   expand the purchase sheet and verify the intended buyer account.
5. Wait for Play propagation rather than repeatedly changing working config.

Licence testers receive test payment instruments. Test-track users who are not
licence testers can incur real charges. Play Billing Lab is optional and must use
the same licence-tester account.

Official references:

- [Test a Play Billing integration](https://developer.android.com/google/play/billing/test)
- [Integrate Play Billing](https://developer.android.com/google/play/billing/integrate)
- [One-time products overview](https://support.google.com/googleplay/android-developer/answer/16430488)

## 11. Required Android purchase tests

Use a dedicated managed test account and record only non-secret provider/order
references in the release ticket.

### Approved purchase

1. Note the starting managed-credit balance.
2. Buy the configured credit pack with the licence tester's always-approved
   instrument.
3. Confirm the Play sheet shows the correct app, product, price, and account.
4. Confirm exactly one backend ledger entry and the expected credit increase.
5. Confirm Play no longer reports the consumable as owned after the backend
   consumes it.
6. Buy it again to prove the product is reusable.

### Retry and replay

1. Interrupt network access after Play returns the purchase if practical.
2. Restart and sign into the same managed account.
3. Use **Restore purchases** or allow startup reconciliation to resend the raw
   token.
4. Confirm the benefit arrives once.
5. Replay/reconcile the same token again and confirm no second ledger grant.

### Pending purchase

1. Use the slow test instrument that eventually approves.
2. Confirm no credits while Play reports `PENDING`.
3. Restart once while pending and confirm it remains ungranted.
4. After Play changes it to `PURCHASED`, confirm one grant and server consume.
5. Repeat with the slow instrument that declines and confirm no grant.

### Account binding

Purchases carry SHA-256 of the Firebase UID as Play's 64-character
`obfuscatedAccountId`. Confirm a valid purchase succeeds for that account and a
missing or mismatched binding is rejected before any grant.

The server sequence must remain:

```text
verify with Play -> verify account binding -> idempotent grant -> consume
```

Google recommends server consumption for consumables when a secure backend is
available. Consumption fulfills Play's acknowledgement requirement. Never move
consumption before the durable grant.

## 12. Free theme maintenance

Adding a color theme is intentionally a code-only change:

1. Add a stable local ID and display metadata to `themeCatalogue.ts`.
2. Add its color map to `themeColors.ts`.
3. Map the ID to a preset in `themePresets.ts`.
4. Add/update translations if the UI gains new theme-specific copy.
5. Run the catalogue and gallery tests, web build, and Android sync.
6. Visually apply the theme from Quick Themes and Theme Gallery on narrow and
   wide layouts.

That is all. Do **not** add a Play product, Stripe product, native product ID,
ownership flag, receipt restore, price lookup, sign-in requirement, or backend
entitlement. The typed preset map and tests fail if catalogue metadata is added
without a usable local preset.

## 13. Build and validation

From the repository root:

```powershell
npm ci
npm test
npm run lint
npm run build
npm run build:staging

Push-Location functions
npm ci
npm test
npm run build
Pop-Location

npm run cap:android
Push-Location android
./gradlew testDebugUnitTest assembleDebug lintDebug --no-daemon
./gradlew bundleRelease --no-daemon
Pop-Location
```

`bundleRelease` needs the local signing files. Its output is
`android/app/build/outputs/bundle/release/app-release.aab`.

Before any Play upload:

- increment `versionCode`; update `versionName` when appropriate;
- inspect the generated app icon and splash assets;
- install a release APK on a physical device;
- test BYOK, managed sign-in, App Check, chat, speech, free theme application,
  managed purchase/reconciliation, and account deletion;
- confirm the privacy policy, Data Safety answers, and reviewer App Access
  instructions match the actual release.

## 14. Play Console review and policy maintenance

The app gate restricts functionality, so provide accurate private App Access
instructions and a dedicated reviewer route. Never put reviewer credentials or
temporary keys in source control or public listing text.

Re-evaluate Data Safety on every material architecture change. Relevant facts
include local BYOK data, Firebase account identifiers, managed generation
content, usage/billing ledger records, voluntary AI-content reports, Stripe and
Play processing, encryption in transit, and the hosted managed-account deletion
route. Use `RELEASE_CHECKLIST.md`; do not blindly copy an older submission.

Google changes Console labels, policy wording, target/API requirements, tester
eligibility, and Billing deadlines. Read the live Console warnings and official
documentation during every release. Record the date and final answers in the
release ticket rather than encoding temporary eligibility numbers here.

## 15. Troubleshooting by symptom

### Play product details are empty

- Confirm the build was installed through Play or uses a valid licence tester.
- Confirm package ID, app signature, tester account, country availability, and
  active one-time product.
- Compare the exact Play ID with `VITE_MANAGED_BILLING_PRODUCT_IDS` and the
  `MANAGED_CREDIT_PACKS` Play field.
- Inspect the native `billingError` debug message; do not add a hardcoded Java
  catalogue as a workaround.

### Play sheet does not open

- Confirm Google Play is current and the device has a signed-in tester account.
- Confirm product details loaded and a foreground activity exists.
- Confirm the user is signed into managed access; the account hash is required.
- A random sideloaded release is not equivalent to an opted-in Play test build.

### Payment succeeded but credits did not appear

- Preserve the account and token for normal reconciliation; do not consume,
  acknowledge, refund, or manually grant from the client.
- Check Functions logs for Play verification, account binding, ledger grant, and
  consumption in that order.
- Verify the runtime service account and Android Publisher API.
- Retry **Restore purchases** after fixing the server. Token idempotency prevents
  duplicate credits.

### Stripe returned but credits are delayed

- Check both subscribed webhook event types and the delivery response.
- Confirm the Checkout session is actually paid.
- Fix the signing secret/endpoint and replay the failed provider event. Do not
  bypass signature verification or manually edit the ledger.

### A theme seems locked or asks for payment

That behavior is a regression. Current theme UI must load entirely from the
local catalogue. Search for purchase, ownership, price, restore, or provider
logic under `src/features/theme` and remove the coupling; do not configure a
provider product.

### Release signing fails

Verify `android/keystore.properties`, its relative `storeFile`, alias, and local
secret availability. Do not weaken signing or commit the keystore to make CI
green.

## 16. Change record expectations

For provider or billing changes, leave a maintainer record containing:

- date, environment, app version, and commit;
- provider object IDs that are safe to record (never secrets or purchase tokens);
- the exact client/backend catalogue mapping;
- immediate, delayed, replay, pending, and wrong-account test outcomes;
- ledger/credit outcome and cleanup/refund status;
- links to the release, provider delivery, and incident ticket if applicable.

For a theme-only change, record the visual checks and automated validation. No
provider confirmation should be needed, because themes have no commerce state.
