# Maestro Tutor – Dev Cheat Sheet

Keyboard UX (The WebView curse)
In chat apps specifically, a common Capacitor bug is that when the on-screen keyboard opens, 
it covers the input field instead of pushing the view up.

[X] Keyboard Check: Input field stays visible when typing on Android.

Action: Open android/app/src/main/AndroidManifest.xml.

Check: Look for the <activity> tag. Ensure android:windowSoftInputMode is set to "adjustResize" (recommended for chat) rather than "adjustPan".

Why: adjustResize shrinks the webview so your CSS "flex-end" logic keeps the input bar visible on top of the keyboard.

Summary Checklist Addition

## Quick Commands
```bash
# Web dev server
npm run dev

# Production build
npm run build

# Build + sync to Android
npm run cap:android

# Open Android Studio
npm run cap:open:android

# Sync only (after npm install or plugin changes)
npx cap sync android
```

## Device Testing (USB)
1) Enable Developer Options + USB Debugging on phone  
2) Plug in, accept debugging prompt  
3) Run:
```bash
npm run cap:android
npm run cap:open:android
```

4) Click **Run** in Android Studio


2. The "Minification" Crash Test
   Configured release.jks. However, Android release builds often default to minifyEnabled true (using R8/Proguard) to shrink code. This frequently breaks Capacitor plugins or the Gemini SDK because it renames the variables that the JSON parser looks for.

Action: Test the Release Build locally. Do not assume that if "Debug" works, "Release" works.

How to test:

Run cd android && ./gradlew assembleRelease (builds an APK, not AAB).

Locate the APK in android/app/build/outputs/apk/release/.

Install it on your phone: adb install app-release.apk.

If it crashes on launch or when you save the key: You need to add Proguard rules or set minifyEnabled false in build.gradle (simplest fix for now).


## API Key (BYOK) Flow
- First launch shows the API key gate (blocking).
- Top-right “API Key” button opens the key manager later.
- Storage:
  - Native: Capacitor Preferences
  - Web: localStorage fallback
- Key files:
  - `src/core/security/apiKeyStorage.ts`
  - `src/features/session/components/ApiKeyGate.tsx`

## When Things Break
**Symptoms → Quick Fix**
- App shows blank WebView after changes  
  → Run `npm run build` then `npx cap sync android`

- API key errors in logs  
  → Tap “API Key” in app and re‑paste key

- Changes not reflected on device  
  → Run `npm run cap:android` again and rebuild app

- Android Studio doesn’t see device  
  → Verify USB Debugging and accept device prompt

[x] Release Build Verified: Installed app-release.apk locally to ensure Minification/R8 didn't break the Gemini SDK.

## Safe Defaults
- Do **not** ship `.env` keys in production  
- Always run `npm run build` before syncing to Android  
- Keep `android/` committed (required for Play builds)

## File Map (Common)
- Capacitor config: `capacitor.config.ts`
- Android project: `android/`
- Privacy policy: `public/privacy.html`
- App settings: `src/store/slices/settingsSlice.ts`

## Git / Repo Hygiene
- Never commit:
  - `.env*`
  - `*.jks` / `*.keystore`
  - `android/keystore.properties`
- `.gitignore` already covers these.

## Debugging Tips
- Use the top-right terminal icon for request logs.
- Use Chrome DevTools (chrome://inspect) for WebView debugging.
