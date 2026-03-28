/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_FIREBASE_API_KEY?: string;
  readonly VITE_FIREBASE_AUTH_DOMAIN?: string;
  readonly VITE_FIREBASE_PROJECT_ID?: string;
  readonly VITE_FIREBASE_STORAGE_BUCKET?: string;
  readonly VITE_FIREBASE_MESSAGING_SENDER_ID?: string;
  readonly VITE_FIREBASE_APP_ID?: string;
  readonly VITE_FIREBASE_MEASUREMENT_ID?: string;
  readonly VITE_FIREBASE_FUNCTIONS_REGION?: string;
  readonly VITE_FIREBASE_APPCHECK_SITE_KEY?: string;
  readonly VITE_FIREBASE_APPCHECK_DEBUG_TOKEN?: string;
  readonly VITE_BACKEND_BASE_URL?: string;
  readonly VITE_GOOGLE_WEB_CLIENT_ID?: string;
  readonly VITE_GOOGLE_SERVER_CLIENT_ID?: string;
  readonly VITE_GOOGLE_PLAY_PACKAGE_NAME?: string;
  readonly VITE_MANAGED_BILLING_PRODUCT_IDS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

// Type declarations for Vite's special import suffixes

// Worker imports with URL suffix
declare module '*?worker&url' {
  const url: string;
  export default url;
}

declare module '*?url' {
  const url: string;
  export default url;
}

// Worker imports (instantiated)
declare module '*?worker' {
  const workerConstructor: {
    new (): Worker;
  };
  export default workerConstructor;
}

// Worklet-specific module declarations
declare module '*.worklet.ts?worker&url' {
  const url: string;
  export default url;
}

declare module '*.worklet.js?worker&url' {
  const url: string;
  export default url;
}
