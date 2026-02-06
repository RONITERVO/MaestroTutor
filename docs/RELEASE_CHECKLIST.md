# Maestro Tutor – Release Checklist (Play Store)

## 1) Pre‑Flight
- [X] App ID is correct: `com.ronitervo.maestrotutor`
- [X] API key gate works (blocks first launch)
- [X] No `.env` key in production build
- [X] Privacy policy is hosted (URL to provide Play Console)
- [X] App description mentions BYOK + open source

- The "Default Icon" Flag
If you upload an app with the default Capacitor/React/Vue logo as the app icon or splash screen, Google (and users) immediately perceive it as "low quality" or "spam."

Action: Ensure you have run npx capacitor-assets generate.

Check: Verify android/app/src/main/res/mipmap-* contains your logo, not the default Capacitor triangle.

[X] Icons Swapped: Default Capacitor icons replaced via capacitor-assets.


## 2) Versioning (Required for every upload)
Edit `android/app/build.gradle`:
- [3] Increment `versionCode`
- ["1.1.0"] Update `versionName`

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
  - The app requires a Gemini API key
  - Provide a temporary test key for reviewers
The "Reviewer Trap" (Crucial for Approval)
Since your app has a "Hard Gate," the Google Play Review team cannot test your app unless they have a key. If they launch it, see a lock screen, and can't get past it, they may reject it for "Limited Functionality" or "App Not Working."

Action: Generate a specific, temporary API Key from your own Google AI Studio account.

Where to put it: In the Google Play Console, go to Policy and programs > App content > App access.

Select: "All or some functionality is restricted" -> Add instructions.

Write: "This app requires a user-generated Google Gemini API Key. For testing purposes, please use this valid key: [PASTE_YOUR_KEY_HERE]. You can revoke this key after testing."

- [X] Add privacy policy URL: `https://ronitervo.github.io/MaestroTutor/public/privacy.html`

## 7) Data Safety Form
Recommended answers:
- [x] Data collected/shared: **Yes**
- [x] Data encrypted in transit: **Yes**
- [x] User data deletion: **Users can delete by uninstalling app**
- [x] Data type: “Messages / User content”
- [x] Purpose: “App functionality”

## 8) Store Listing (Short copy to add)
```
This app is a secure local client for your own Gemini API key.
No backend. Your key stays on-device.
```

## 9) After Approval
- [ ] Revoke the temporary review key
- [ ] Tag the release in Git
- [x] Archive the AAB + keystore info

## Common Gotchas
- Forgot to bump `versionCode` → upload fails
- Forgot `npm run build` before sync → old UI ships
- Privacy policy missing → review rejection
- “No data collected” set in Data Safety → rejection