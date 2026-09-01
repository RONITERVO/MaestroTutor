# Maestro Tutor – Release Checklist (Play Store)

## 1) Pre‑Flight
- [X] App ID is correct: `com.ronitervo.maestrotutor`
- [X] Access gate works: BYOK accepts a user-supplied Gemini key; managed
      access signs in with Google and uses purchased credits
- [X] No `.env` key in production build
- [X] Privacy policy is hosted (URL to provide Play Console)
- [ ] Store listing describes both BYOK and managed access
- [ ] Every color theme applies locally without sign-in, a network request,
      price, purchase, ownership, or restore UI
- [ ] `npm run verify:release-config` reports Stripe as the sole active purchase
      provider and the intended pack ids.
- [ ] Release gate and the Stripe-first `Headless staging journey` are green for
      the candidate commit.
- [ ] Web reCAPTCHA Enterprise and Android Play Integrity App Check remain
      enforced for the exact production apps/domains/fingerprints.
- [ ] `VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED` matches recorded Play
      programme eligibility. Keep it `false` without that proof.

- The "Default Icon" Flag
If you upload an app with the default Capacitor/React/Vue logo as the app icon or splash screen, Google (and users) immediately perceive it as "low quality" or "spam."

Action: Ensure you have run npx capacitor-assets generate.

Check: Verify android/app/src/main/res/mipmap-* contains your logo, not the default Capacitor triangle.

[X] Icons Swapped: Default Capacitor icons replaced via capacitor-assets.


## 1b) Memory & Quality Thresholds (Play requirement)

Play sets thresholds on dynamic memory usage, bitmap usage and code
optimization. These are measured on real devices, so a dev-machine pass proves
nothing. Run this on the weakest device you have, or an emulator configured with
2 GB RAM and 4 cores.

- [ ] Open a chat with a long mixed history: several mini-games, a multi-page
      PDF, images, audio. Scroll top to bottom and back.
- [ ] In `chrome://inspect` → Console, run `__EMBED_DEBUG__()` at rest and while
      scrolling. `live.length` must never exceed `budgets.maxLiveEmbeds`, and
      `posters` must never exceed `budgets.posterBudget`. (Dev builds only.)
- [ ] Android Studio → Profiler → Memory: total should stay flat while scrolling
      past embeds. A staircase that never comes down means something is
      retaining rasterized pages or documents.
- [ ] Confirm the tier the device resolved to. `navigator.deviceMemory` is
      missing on some WebViews, which resolves to `mid`; if the device is
      genuinely weak, check that it was not over-classified.
- [ ] Scroll the transcript up through history and confirm nothing jumps: the
      reserved boxes must hold their space with no embed running.
- [ ] Rotate the device with an embed on screen. The box changes height but the
      content must stay correctly proportioned and the page must not jump.

## 2) Versioning (Required for every upload)
Edit `android/app/build.gradle`:
- [X] Increment `versionCode`
- [X] Update `versionName`

## 3) Build Web Assets
```bash
npm run build
npx cap sync android
```

## 4) Release Signing (One‑Time Setup)
```bash
keytool -genkeypair -v -keystore release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias maestro
```
Then:
- [X] Move `release.jks` to `android/keystore/`
- [X] Create `android/keystore.properties` from `android/keystore.properties.example`
- [X] Keep keystore + passwords in a safe place (you’ll need them forever)

## 5) Build Release AAB
```bash
cd android
./gradlew bundleRelease
```
Output:
```
android/app/build/outputs/bundle/release/app-release.aab
```

## 6) Play Console – Internal Testing
- [X] Upload the `.aab`
- [ ] Add testers
- [ ] Provide “App Access” instructions:
  - Mark **All or some functionality is restricted**.
  - Prefer a dedicated Google reviewer account that can use managed access and
    has enough test credits for every reviewed flow. Provide its credentials
    and exact sign-in steps in the private App Access field, never in this
    repository or the public store listing.
  - If a managed reviewer account cannot be used, provide a temporary,
    quota-limited Gemini API key and instructions for the BYOK path.
  - Exercise the supplied access method on a clean device before submitting.
  - Remove the reviewer account's access or revoke the temporary key after the
    review is complete.

The access gate is a rejection risk if the reviewer cannot get past it. Keep
these instructions current whenever authentication or the first-run flow
changes.

- [X] Add privacy policy URL: `https://chatwithmaestro.com/privacy.html`

## 7) Data Safety Form
Do not copy old blanket answers. Re-evaluate the form against the release and
Google Play's current definitions each time. The current implementation facts
maintainers must account for are:

- [ ] Data is encrypted in transit.
- [ ] BYOK credentials and chat history stay in local app storage; BYOK Gemini
      requests go directly from the device to Google.
- [ ] Managed access sends account identifiers and generation content through
      Firebase/Google Cloud and Google Gemini for app functionality.
- [ ] Managed access stores account/billing summaries, entitlements, usage and
      billing ledger metadata, and operational anti-abuse/cleanup state.
- [ ] Stripe Checkout is the sole managed-credit payment provider. Maestro
      stores Checkout/customer references, immutable grant snapshots and hashed
      idempotency claims, not raw payment-card details. Android in-app checkout
      is fail-closed unless the documented Play external-checkout programme gate
      is satisfied; signed-in users can spend a balance purchased on the web.
- [ ] Color themes are free local presentation choices and are not products or
      entitlements in either provider.
- [ ] A voluntarily submitted AI-content report can include reported content,
      a reason, optional notes, and request metadata.
- [ ] Uninstalling clears only data stored locally on that device. It does not
      delete a managed account or provider-held records.
- [ ] Managed users can request account deletion in the app or at
      `https://chatwithmaestro.com/delete-account.html`.
- [ ] The published privacy policy and account-deletion URL match the release.

Record the final Play Console answers and the date they were revalidated in the
release ticket. If the product or Google's form wording changes, obtain an
appropriate policy/legal review rather than inferring an answer from this
engineering checklist.

## 8) Store Listing (Short copy to add)
```
Use your own Gemini API key locally, or sign in for Maestro managed access with
purchased credits. Chat history stays on your device. Managed requests are
processed securely by Maestro's cloud service and Google Gemini.
```

## 9) After Approval
- [ ] Remove dedicated reviewer access or revoke the temporary review key
- [ ] Tag the release in Git
- [x] Archive the AAB + keystore info

## Common Gotchas
- Forgot to bump `versionCode` → upload fails
- Forgot `npm run build` before sync → old UI ships
- Privacy policy missing → review rejection
- Data Safety or App Access answers copied from an older architecture → review
  rejection or an inaccurate disclosure
