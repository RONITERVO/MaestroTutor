# Maestro Tutor - New Developer Setup and Play Console Guide

This guide is for a developer who is new to this repository and needs to get the Android app, Google Play setup, and Google Play Billing setup working end to end.

It focuses on the parts that cannot be solved only by editing code:

- local machine setup
- Android signing
- Google Play Console setup
- Google Play Billing catalog setup
- review-time setup for the Gemini API key gate
- release and maintenance work that must keep matching the codebase

If you only want the quick local commands, also see [DEV_CHEATSHEET.md](./DEV_CHEATSHEET.md). If you only want the short release checklist, also see [RELEASE_CHECKLIST.md](./RELEASE_CHECKLIST.md).

## 1. What lives where

Before touching anything, understand the split between code and external systems.

### In this repository

- Web app and UI logic: `src/`
- Android wrapper app: `android/`
- Capacitor config: [`capacitor.config.ts`](../capacitor.config.ts)
- Android package ID and release version: [`android/app/build.gradle`](../android/app/build.gradle)
- TypeScript billing product IDs: [`src/features/theme/config/themeProducts.ts`](../src/features/theme/config/themeProducts.ts)
- Java billing product IDs: [`android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java`](../android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java)
- Purchased theme color mappings: [`src/features/theme/config/purchasableThemePresets.ts`](../src/features/theme/config/purchasableThemePresets.ts)
- Billing runtime code: [`android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java`](../android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java)
- Store UI: [`src/features/theme/components/ThemeStorePanel.tsx`](../src/features/theme/components/ThemeStorePanel.tsx)
- Privacy policy page that can be hosted: [`public/privacy.html`](../public/privacy.html)

### Outside the repository

- Google AI Studio account and Gemini API key
- Google Play Console developer account
- Google Play app entry for package `com.ronitervo.maestrotutor`
- Google Play Billing product catalog
- Google Play testers and license testers
- Android signing keystore and passwords
- Hosted privacy policy URL

If one of those external pieces is missing, the code can compile and still not work correctly in production.

## 2. Access you need before you start

Ask for these before doing any serious Android or release work:

- Git access to this repository
- Google Play Console access for the app
- Access to the Google account that owns the Play Console app, or at least release permissions
- Access to the privacy policy hosting location, if policy text needs updating
- A Gemini API key for local testing
- A physical Android device or a working emulator

If you cannot access Play Console, you cannot finish billing setup, internal testing, or production releases.

## 3. Local machine setup

### Install required tools

1. Install Node.js 18 or newer.
2. Install Android Studio.
3. In Android Studio, install the Android SDK, platform tools, and build tools.
4. Make sure Java 17 is available. This project compiles Android with Java 17, as defined in [`android/app/build.gradle`](../android/app/build.gradle).
5. Optional but useful: `adb` on your PATH for device install and debugging.

### Clone and install

```bash
git clone <repo-url>
cd maestrotutor
npm install
```

### Start the web app once

```bash
npm run dev
```

Open `http://localhost:5173`.

On first launch, the app asks for a Gemini API key unless you use a local `.env` file.

### Optional: use a local `.env` during development only

Create a root `.env` file like this:

```env
VITE_API_KEY=your_api_key_here
```

Use this only for local development. Do not ship a production build with a real API key embedded.

### Start the Android app locally

```bash
npm run cap:android
npx cap open android
```

Then run the app from Android Studio on a device or emulator.

## 4. Local files and secrets that are not committed

These files are expected locally and must not be committed.

### Development-only secret

- `.env`

### Release signing secrets

- `android/keystore.properties`
- your `.jks` or `.keystore` file

The example properties file is:

- [`android/keystore.properties.example`](../android/keystore.properties.example)

It expects something like:

```properties
storeFile=../keystore/release.jks
storePassword=YOUR_STORE_PASSWORD
keyAlias=YOUR_KEY_ALIAS
keyPassword=YOUR_KEY_PASSWORD
```

### One-time release keystore setup

1. Create a keystore:

```bash
keytool -genkeypair -v -keystore release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias maestro
```

2. Move it to a safe local folder, for example `android/keystore/`.
3. Create `android/keystore.properties` from the example file.
4. Put the correct passwords and alias in that file.
5. Back up the keystore and passwords outside your laptop.

Important rules:

- Never commit the keystore.
- Never commit `android/keystore.properties`.
- Never lose the keystore or its passwords.

Losing signing credentials is not a code problem. It becomes a release-management problem.

## 5. Verify project constants before touching Play Console

These values must stay aligned between code and Play Console.

### Package ID

This app is configured as:

- `com.ronitervo.maestrotutor`

Verify in:

- [`capacitor.config.ts`](../capacitor.config.ts)
- [`android/app/build.gradle`](../android/app/build.gradle)

Do not casually change the package ID after Play Console setup. In practice, that means a new app listing and a broken billing/test setup.

### Billing product IDs

The current paid theme IDs are:

- `theme_ocean_blue`
- `theme_sunset_gold`
- `theme_dark_neon`

They must match in both:

- [`src/features/theme/config/themeProducts.ts`](../src/features/theme/config/themeProducts.ts)
- [`android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java`](../android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java)

Never rename an already released product ID. Create a new product instead.

## 6. Create or verify the Play Console app

This is one-time per app, not per developer machine.

### Create the app entry

1. Sign in to Google Play Console.
2. Create an app if one does not already exist.
3. Make sure the app is for the same package ID used by this repo: `com.ronitervo.maestrotutor`.
4. Accept Play App Signing during app creation or first release setup.
5. Fill in the base store listing details.

The Play Console UI changes over time. If the left-nav labels move, use the Play Console search bar instead of guessing the menu path.

### Upload a first build to establish the app

Build an app bundle:

```bash
cd android
./gradlew bundleRelease
```

Output:

```text
android/app/build/outputs/bundle/release/app-release.aab
```

Upload that `.aab` to an internal testing track first, not straight to production.

### Store listing and policy basics

Before review, make sure the following are filled in:

- app title
- short description
- full description
- screenshots
- app icon
- support email
- privacy policy URL

This repo already contains a privacy policy page at [`public/privacy.html`](../public/privacy.html), but Play Console needs a hosted public URL, not just the file in the repo. 

Run "npm run deploy" to host on github pages and make sure the custom domain stays set at https://chatwithmaestro.com on github pages so that /privacy.html can be accessed alongside the /gemini-models.json both of those must exist in the custom deployment https://chatwithmaestro.com so that a. User in the app can open the privacy policy and b. The app can fetch the latest models from the json.

## 7. App review setup for the Gemini API key gate

This app is BYOK. Reviewers cannot use the app if they do not have a Gemini API key.

That means app review setup is mandatory outside code.

### What to do

1. Generate a temporary Gemini API key for Google Play reviewers.
2. In Play Console, open `Policy and programs > App content > App access`.
3. Explain that the app requires a user-provided Gemini API key.
4. Provide the temporary review key and exact steps to enter it.
5. After approval, revoke that temporary key if you do not want to keep it active.

Suggested wording:

```text
This app requires a Google Gemini API key before the main functionality is usable.
For review, use this temporary test key: <paste key here>.
Open the app, paste the key into the first screen, and continue.
```

If this is skipped, review can fail even when the app itself is technically fine.

## 8. Data Safety and policy maintenance

You must keep Play Console policy answers aligned with the current app behavior.

At minimum, verify:

- privacy policy URL is live
- Data Safety answers still match the app
- app access instructions still work
- screenshots still match the app
- store listing does not claim features that no longer exist

Treat this as maintenance work, not one-time paperwork.

## 9. Set up Google Play Billing for premium themes

This repo already contains the in-app billing code. What code cannot do for you is create the Play-side catalog.

### Understand the current billing model

The premium themes are designed as:

- Android-only Google Play purchases
- one-time purchases
- permanent unlocks
- non-consumable entitlements

This is visible from the code:

- Java side queries `BillingClient.ProductType.INAPP`
- purchases are acknowledged
- purchases are restored on app launch
- there is no consume flow

Relevant files:

- [`android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java`](../android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java)
- [`src/features/theme/components/ThemeStorePanel.tsx`](../src/features/theme/components/ThemeStorePanel.tsx)

### Create the products in Play Console

For each product ID below, create a one-time product in Play Console:

| Product ID | Display name in app |
| --- | --- |
| `theme_ocean_blue` | Ocean Blue |
| `theme_sunset_gold` | Sunset Gold |
| `theme_dark_neon` | Dark Neon |
| `theme_scholar` | Scholar |
| `theme_pure_light` | Pure Light |
| `theme_obsidian` | Obsidian |
| `theme_forest` | Forest |
| `theme_lavender` | Lavender |
| `theme_spectrum` | Spectrum |
| `theme_graphite` | Graphite |

### Recommended setup for each product

1. Create a one-time product with the exact product ID.
2. Set a clear title and description.
3. Configure a normal buy purchase option.
4. Set pricing.
5. Activate the product so it is available to testers.

Keep the catalog simple unless the code is updated:

- use permanent theme unlocks
- do not make these consumables
- do not rely on multiple offers or complex purchase-option logic
- do not introduce rent-style behavior

Reason: the current app code expects one permanent entitlement per product and a straightforward product-details response.

### Important product-ID rule

Product IDs in Play Console must exactly match the IDs in code.

If Play Console has `theme-ocean-blue` but the app requests `theme_ocean_blue`, the store will not return that product.

## 10. Map purchased products to real theme presets

Billing alone does not define the colors that users receive. The app also needs a preset mapping.

Current mapping file:

- [`src/features/theme/config/purchasableThemePresets.ts`](../src/features/theme/config/purchasableThemePresets.ts)

Current behavior:

- the user sees one `Theme Store` entry in Quick Themes
- bought themes appear in Quick Themes after purchase
- users do not see every locked paid theme all the time

If you add a new paid theme and forget this mapping file, the purchase may succeed but the theme will not appear as a usable preset.

## 11. Set up testing for billing

This is where most first-time billing integrations fail.

### Use internal testing and license testers

You should set up both:

- an internal testing track
- license testers

Why both:

- internal testing lets testers install the app from Google Play
- license testers can use Play test payment methods and can even test sideloaded builds

### Internal testing setup

1. In Play Console, create or open the internal testing track.
2. Upload a new `.aab`.
3. Add tester email addresses.
4. Share the opt-in URL with those testers.
5. Wait for Play propagation. It can take time.

Important:

- purchases on test tracks can be real charges unless the tester is also a license tester
- tester accounts must actually opt in

### License tester setup

In Play Console, find the global license testing settings and add the Gmail addresses used for billing tests.

If the menu path has moved, use the Play Console search bar and search for `License testing`.

Why license testers matter:

- they can test sideloaded builds
- they see Play test payment methods
- they avoid real charges for standard billing tests

### Optional but useful: Play Billing Lab

Install Google Play Billing Lab on the test device if you need to simulate more billing scenarios, regions, or response codes.

This is optional for basic theme-purchase testing, but useful for serious QA.

## 12. How to test the premium theme purchase flow

Do not assume that building the app means billing is working.

### Recommended test path

1. Make sure the tester Gmail is:
   - added to the internal test track
   - added as a license tester
2. Install the app either:
   - from the Play internal testing link, or
   - as a sideloaded APK while using a license tester account
3. On the device, sign in to the Play Store with the tester account.
4. Open the app.
5. Enter a Gemini API key so the app is usable.
6. Open `Paint Colors`.
7. Tap `Theme Store`.
8. Verify product titles and prices appear.
9. Buy one product using the Play test payment method.
10. Verify the bought theme appears in Quick Themes.
11. Apply it and confirm the UI changes.
12. Reopen the app and confirm the theme is still owned.
13. Use `Restore Purchases` in the store panel and confirm ownership still resolves correctly.

### Extra cases you should test

- user cancels purchase
- payment declines
- pending payment finishes later
- reinstall app and restore purchases
- product already owned

The current code acknowledges purchases and restores them on app launch, so those behaviors should always be validated after billing changes.

## 13. Build and release workflow for Android

### Development build

```bash
npm run build
npx cap sync android
cd android
./gradlew assembleDebug
```

### Release APK for device smoke testing

```bash
npm run build
npx cap sync android
cd android
./gradlew assembleRelease
```

APK output:

```text
android/app/build/outputs/apk/release/app-release.apk
```

Use this to smoke-test the release build before uploading an `.aab`.

### Release AAB for Play Console

```bash
npm run build
npx cap sync android
cd android
./gradlew bundleRelease
```

AAB output:

```text
android/app/build/outputs/bundle/release/app-release.aab
```

Upload the `.aab` to the correct Play track.

## 14. What must be updated for every Android release

Before each uploaded release:

1. Bump `versionCode` in [`android/app/build.gradle`](../android/app/build.gradle).
2. Update `versionName` in the same file.
3. Run:

```bash
npm run build
npx cap sync android
cd android
./gradlew assembleRelease
./gradlew bundleRelease
```

4. Test the release APK on a real device.
5. Upload the AAB to Play Console.
6. Update release notes in Play Console if needed.

Never skip the release APK smoke test. Debug working is not enough.

## 15. What must be updated when adding a new paid theme

If you add another premium theme, update all of these together:

1. Add the TypeScript product ID and store metadata in [`src/features/theme/config/themeProducts.ts`](../src/features/theme/config/themeProducts.ts).
2. Add the Java product ID in [`android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java`](../android/app/src/main/java/com/ronitervo/maestrotutor/ThemeProducts.java).
3. Add the actual preset colors in [`src/features/theme/config/purchasableThemePresets.ts`](../src/features/theme/config/purchasableThemePresets.ts).
4. Create the matching one-time product in Play Console.
5. Price and activate it in Play Console.
6. Test it with a license tester account.

If you miss one of those steps, the feature is incomplete.

## 16. What must be reviewed when billing or policy behavior changes

If you change app behavior in ways users or reviewers care about, update the external setup too.

Examples:

- If the onboarding or key gate changes, update app-access instructions in Play Console.
- If privacy or data handling changes, update the privacy policy and Data Safety answers.
- If product names or prices change, update Play Console catalog details.
- If the package ID changes, expect a new Play app setup, not a simple code patch.
- If the keystore changes, treat it as a release-management event and coordinate carefully.

## 17. Troubleshooting

### Store opens but no prices or products appear

Check:

- products exist in Play Console
- product IDs match code exactly
- products are active
- enough time has passed for Play propagation
- tester is using the right Google account
- app package ID matches the Play Console app

### Purchase dialog never opens in a locally installed APK

Check:

- the tester account is a license tester
- the device Play Store account matches the tester account
- the Play Console app uses package `com.ronitervo.maestrotutor`

### Release build fails with `Missing keystore.properties`

Create:

- `android/keystore.properties`

using:

- [`android/keystore.properties.example`](../android/keystore.properties.example)

### Theme was bought but does not appear in Quick Themes

Check:

- purchase succeeded and was acknowledged
- product ID exists in `ThemeProducts.java`
- product ID exists in `themeProducts.ts`
- product is mapped in `purchasableThemePresets.ts`

## 18. Safe defaults for new developers

- Do not commit secrets.
- Do not change published product IDs.
- Do not change the package ID casually.
- Do not release without testing `assembleRelease`.
- Do not assume Play Console metadata stays correct forever.
- Do not assume a sideloaded APK is enough proof that billing works for real users.

## 19. External references

These official docs were useful when writing this guide. They can change, so prefer searching the current official page if a menu label has moved.

- Google Play Console app setup: https://support.google.com/googleplay/android-developer/answer/9859152
- Google Play app review preparation and App access: https://support.google.com/googleplay/android-developer/answer/9859455
- Google Play requirements for app-access credentials: https://support.google.com/googleplay/android-developer/answer/15748846
- Google Play Billing testing: https://developer.android.com/google/play/billing/billing_testing
- Google Play one-time products: https://developer.android.com/google/play/billing/one-time-products
- Google Play billing overview: https://developer.android.com/google/play/billing/
