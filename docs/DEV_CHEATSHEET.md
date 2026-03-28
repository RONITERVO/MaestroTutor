# Maestro Tutor - Dev Cheat Sheet

## Quick Commands

```bash
# Web dev
npm run dev

# Production web build
npm run build

# Build and sync Android
npm run build:android

# Open Android Studio
npm run cap:open:android

# Build release AAB
npm run build:aab

# Functions build
cd functions
npm run build
cd ..

# Deploy backend
firebase deploy --only functions,firestore:rules,firestore:indexes
```

## Local Files You Need

- `.env`
- `functions/.env`
- `android/app/google-services.json`
- `android/keystore.properties`
- upload keystore file

## Access Modes

### BYOK

- User pastes Gemini API key
- Client talks directly to Gemini
- API key gate shows a local usage metadata ledger for tracked requests
- BYOK ledger still offers the Google Cloud billing shortcut
- This path must stay intact

### Managed

- User signs in with Firebase Google auth
- User buys credits through Google Play on Android
- Backend verifies purchase token with Play Developer API
- Backend grants credits and records ledgers in Firestore
- Gemini requests go through Firebase Functions
- API key gate shows the same local usage metadata ledger for managed requests
- Managed live tokens expire after 3 minutes and the client silently reconnects when needed
- Managed mode allows at most 2 active live sockets per signed-in user

## Play Metadata / Policy Claims

Safe claims for the current build:

- BYOK exists and keeps the user's Gemini key local to the device
- Managed mode uses Firebase Google sign-in
- Managed requests go through Firebase Functions and then Google Gemini
- Firestore stores managed billing / usage / entitlement / purchase verification records
- Google Play handles Android managed credit payments
- No ads
- No developer analytics SDK
- No developer crash reporting SDK

Do not claim these are already solved unless you implement them:

- self-serve in-app managed account deletion
- outside-app deletion request URL
- in-app report / flag controls for offensive AI-generated output

Current production blockers to remember:

- Play account-deletion policy is relevant because managed sign-in creates an account path
- Play AI-generated-content policy expects an in-app report / flag path for generated content
- `public/privacy.html` is updated to describe current behavior, but Play Console declarations must stay equally honest

## Product Rules

### Themes

- Existing theme SKUs remain permanent unlocks
- They are acknowledged
- They are not consumed

### Managed credits

- Current SKU: `maestro_credits_1000`
- One purchase grants `1000` credits
- Purchase is consumed after backend verification so it can be bought again

### Managed uploads

- Backend keeps at most `20` active managed files per user
- Oldest managed files are auto-evicted before new uploads
- Upload billing is size-based through `MANAGED_UPLOAD_CREDITS_PER_MB`

## Important Paths

- Managed access UI: `src/features/session/components/ManagedAccessPanel.tsx`
- App gate: `src/features/session/components/ApiKeyGate.tsx`
- Integration config: `src/core/config/integrations.ts`
- Service hub: `src/services/maestroServices.ts`
- Backend client: `src/services/backend/maestroBackendService.ts`
- Native billing: `android/app/src/main/java/com/ronitervo/maestrotutor/ThemeBillingManager.java`
- Backend entry: `functions/src/index.ts`
- Play verification: `functions/src/playBilling.ts`
- Reservation logic: `functions/src/managedBilling.ts`
- Gemini proxy and managed files: `functions/src/gemini.ts`

## Firestore Collections Written By Backend

- `users/{uid}/account/summary`
- `users/{uid}/billingLedger/{entryId}`
- `users/{uid}/usageLedger/{entryId}`
- `users/{uid}/entitlements/{entitlementId}`
- `googlePlayPurchases/{purchaseToken}`
- `managedReservations/{reservationId}`
- `managedFiles/{fileId}`

## Common Build / Release Flow

```bash
npm run build
cd functions
npm run build
cd ..
npm run build:android
npm run build:aab
```

## Common Failure Checks

### Android sign-in fails

- SHA-1 fingerprints missing in Firebase
- stale `google-services.json`
- Firebase Google provider disabled

### Purchase succeeds but no credits appear

- Backend not deployed
- Wrong service account linked in Play Console
- `MANAGED_CREDIT_PRODUCTS` mismatch
- wrong package id

### Android backend calls fail

- `functions/.env` missing localhost origins
- wrong `VITE_BACKEND_BASE_URL`

### Managed live disconnects too early or too late

- `MANAGED_LIVE_TOKEN_LIFETIME_SECONDS` should stay `180`
- `MANAGED_MAX_ACTIVE_LIVE_SOCKETS` should stay `2`
- verify the app silently reconnects after the 3-minute lease rolls over

### Credits stay reserved

- scheduled sweep not deployed
- function crashed before release path completed
- check `managedReservations`

## Safe Defaults

- Do not commit secrets
- Do not rename released product ids
- Do not remove localhost origins from backend env
- Do not trust Play purchases on the client without backend verification
- Do not replace user-scoped file deletion with global deletion
- Do not regress BYOK while editing managed mode
