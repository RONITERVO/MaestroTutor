# Maestro Tutor - Release Checklist

Use this before every Android upload.

## 1. Versioning

- [ ] `android/app/build.gradle` `versionCode` incremented
- [ ] `android/app/build.gradle` `versionName` updated

## 2. Local Files Present

- [ ] `.env`
- [ ] `functions/.env`
- [ ] `android/app/google-services.json`
- [ ] `android/keystore.properties`
- [ ] upload keystore file

## 3. Firebase / Backend

- [ ] Firebase Authentication Google provider enabled
- [ ] Firestore exists
- [ ] Blaze plan enabled
- [ ] Backend deployed with:

```bash
firebase deploy --only functions,firestore:rules,firestore:indexes
```

- [ ] `GET /health` succeeds
- [ ] Backend service account linked in Play Console for purchase verification
- [ ] `MANAGED_CREDIT_PRODUCTS` matches the live Play SKU list
- [ ] `ALLOWED_ORIGINS` still contains localhost and capacitor localhost values
- [ ] Managed upload envs reviewed: `MANAGED_MAX_ACTIVE_FILES_PER_USER`, `MANAGED_UPLOAD_CREDITS_PER_MB`, `MANAGED_MAX_UPLOAD_BYTES`
- [ ] If `REQUIRE_APPCHECK=true`, both web and Android managed traffic have been verified against the live backend

## 4. Static Public Assets

- [ ] `https://chatwithmaestro.com/privacy.html` is live
- [ ] `https://chatwithmaestro.com/gemini-models.json` is live
- [ ] If either public file changed, run:

```bash
npm run deploy
```

## 5. Play Console

- [ ] App id is still `com.ronitervo.maestrotutor`
- [ ] Existing theme SKUs remain active
- [ ] Managed credit SKU `maestro_credits_1000` is active
- [ ] Internal testing track ready
- [ ] License testers configured
- [ ] App access instructions updated
- [ ] Temporary BYOK review key prepared if needed
- [ ] Privacy policy URL updated in listing
- [ ] Data Safety answers still match current app behavior

## 6. Build

- [ ] Web build passes

```bash
npm run build
```

- [ ] Functions build passes

```bash
cd functions
npm run build
cd ..
```

- [ ] Android sync passes

```bash
npm run build:android
```

- [ ] Release AAB built

```bash
npm run build:aab
```

## 7. Smoke Tests

### BYOK

- [ ] Fresh install with no account still shows access gate
- [ ] Entering a Gemini API key works
- [ ] Text chat works
- [ ] Image generation works
- [ ] Live mode works

### Managed

- [ ] Google sign-in works
- [ ] Managed panel loads product details
- [ ] `maestro_credits_1000` purchase succeeds
- [ ] Credits are granted after backend verification
- [ ] Purchase is consumed so the same SKU can be bought again
- [ ] Managed text request works
- [ ] Managed streaming request works
- [ ] Managed translation works
- [ ] Managed image generation works
- [ ] Managed uploads succeed and enforce per-user quota correctly
- [ ] Credits deduct correctly
- [ ] Restore purchases does not double-grant

### Theme regression

- [ ] Existing theme purchase still unlocks permanently
- [ ] Theme restore still works
- [ ] Theme purchases are not consumed

## 8. Upload

- [ ] Upload `android/app/build/outputs/bundle/release/app-release.aab`
- [ ] Add release notes
- [ ] Roll out to internal testing first
- [ ] Install from Play on a real device before any wider rollout

## 9. After Internal Validation

- [ ] Review logs / Firestore ledgers for a real managed purchase and a real managed charge
- [ ] Confirm no credits are stuck in `reservedCredits`
- [ ] Promote only after both BYOK and managed flows are validated

## 10. After Approval

- [ ] Revoke any temporary review BYOK key
- [ ] Archive the shipped AAB
- [ ] Tag the release in Git if desired
