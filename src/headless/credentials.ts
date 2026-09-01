// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

const REFRESH_MARGIN_MS = 5 * 60_000;

export interface HeadlessCredentialOptions {
  firebaseApiKey?: string;
  firebaseAppId?: string;
  firebaseEmail?: string;
  firebasePassword?: string;
  firebaseIdToken?: string;
  appCheckToken?: string;
  appCheckDebugToken?: string;
  fetchImpl?: typeof fetch;
  now?: () => number;
}

interface CachedToken {
  token: string;
  expiresAt: number;
}

interface FirebaseAuthResponse {
  idToken: string;
  refreshToken?: string;
  expiresIn: string;
  localId: string;
}

interface AppCheckResponse {
  token: string;
  ttl: string;
}

export interface HeadlessCredentialProvider {
  getManagedHeaders(required: boolean): Promise<Record<string, string>>;
  getFirebaseIdToken(): Promise<string | null>;
  getAppCheckToken(): Promise<string | null>;
  getUserId(): Promise<string | null>;
  signIn(): Promise<string>;
  signOut(): void;
  describe(): {
    firebase: 'static-token' | 'password' | 'missing';
    appCheck: 'static-token' | 'debug-token' | 'missing';
    signedOut: boolean;
    userId: string | null;
  };
}

const trim = (value: string | undefined): string => value?.trim() || '';

const parseDurationMs = (value: string | number | undefined, fallbackMs: number): number => {
  if (typeof value === 'number' && Number.isFinite(value)) return Math.max(0, value * 1_000);
  const raw = String(value || '').trim();
  const match = /^(\d+(?:\.\d+)?)s?$/.exec(raw);
  return match ? Number(match[1]) * 1_000 : fallbackMs;
};

const decodeJwtPayload = (token: string): Record<string, unknown> | null => {
  const payload = token.split('.')[1];
  if (!payload) return null;
  try {
    return JSON.parse(Buffer.from(payload, 'base64url').toString('utf8')) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const jwtExpiry = (token: string): number => {
  const exp = decodeJwtPayload(token)?.exp;
  return typeof exp === 'number' && Number.isFinite(exp) ? exp * 1_000 : Number.POSITIVE_INFINITY;
};

const jwtSubject = (token: string): string | null => {
  const subject = decodeJwtPayload(token)?.sub;
  return typeof subject === 'string' && subject.trim() ? subject.trim() : null;
};

const readJson = async <T>(response: Response, provider: string): Promise<T> => {
  const payload = await response.json().catch(() => null) as {
    error?: { message?: string; status?: string };
  } | null;
  if (!response.ok) {
    const providerCode = payload?.error?.message || payload?.error?.status || `HTTP_${response.status}`;
    throw new Error(`${provider} rejected the credential request (${providerCode}).`);
  }
  return payload as T;
};

export const createHeadlessCredentialProvider = (
  options: HeadlessCredentialOptions = {},
): HeadlessCredentialProvider => {
  const fetchImpl = options.fetchImpl || globalThis.fetch;
  const now = options.now || Date.now;
  const apiKey = trim(options.firebaseApiKey || process.env.MAESTRO_FIREBASE_API_KEY);
  const appId = trim(options.firebaseAppId || process.env.MAESTRO_FIREBASE_APP_ID);
  const email = trim(options.firebaseEmail || process.env.MAESTRO_FIREBASE_EMAIL);
  const password = trim(options.firebasePassword || process.env.MAESTRO_FIREBASE_PASSWORD);
  const staticFirebaseToken = trim(options.firebaseIdToken || process.env.MAESTRO_FIREBASE_ID_TOKEN);
  const staticAppCheckToken = trim(options.appCheckToken || process.env.MAESTRO_FIREBASE_APP_CHECK_TOKEN);
  const debugToken = trim(options.appCheckDebugToken || process.env.MAESTRO_APPCHECK_DEBUG_TOKEN);
  const projectNumber = appId.split(':')[1] || '';
  let firebaseToken: CachedToken | null = staticFirebaseToken
    ? { token: staticFirebaseToken, expiresAt: jwtExpiry(staticFirebaseToken) }
    : null;
  let appCheckToken: CachedToken | null = staticAppCheckToken
    ? { token: staticAppCheckToken, expiresAt: jwtExpiry(staticAppCheckToken) }
    : null;
  let refreshToken = '';
  let userId = staticFirebaseToken ? jwtSubject(staticFirebaseToken) : null;
  let signedOut = false;
  let firebaseRefresh: Promise<string> | null = null;
  let appCheckRefresh: Promise<string> | null = null;

  const tokenFresh = (token: CachedToken | null): boolean => (
    Boolean(token?.token) && token!.expiresAt - now() > REFRESH_MARGIN_MS
  );

  const signInWithPassword = async (): Promise<string> => {
    if (!apiKey || !email || !password) {
      throw new Error(
        'Managed headless commands require MAESTRO_FIREBASE_ID_TOKEN or '
        + 'MAESTRO_FIREBASE_API_KEY, MAESTRO_FIREBASE_EMAIL and MAESTRO_FIREBASE_PASSWORD.',
      );
    }
    const response = await fetchImpl(
      `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=${encodeURIComponent(apiKey)}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, returnSecureToken: true }),
      },
    );
    const result = await readJson<FirebaseAuthResponse>(response, 'Firebase Authentication');
    firebaseToken = {
      token: result.idToken,
      expiresAt: now() + parseDurationMs(result.expiresIn, 60 * 60_000),
    };
    refreshToken = trim(result.refreshToken);
    userId = trim(result.localId) || jwtSubject(result.idToken);
    signedOut = false;
    return result.idToken;
  };

  const refreshFirebaseToken = async (): Promise<string> => {
    if (!apiKey || !refreshToken) return signInWithPassword();
    const response = await fetchImpl(
      `https://securetoken.googleapis.com/v1/token?key=${encodeURIComponent(apiKey)}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ grant_type: 'refresh_token', refresh_token: refreshToken }),
      },
    );
    const result = await readJson<{
      id_token: string;
      refresh_token?: string;
      expires_in: string;
      user_id?: string;
    }>(response, 'Firebase Secure Token');
    firebaseToken = {
      token: result.id_token,
      expiresAt: now() + parseDurationMs(result.expires_in, 60 * 60_000),
    };
    refreshToken = trim(result.refresh_token) || refreshToken;
    userId = trim(result.user_id) || jwtSubject(result.id_token) || userId;
    return result.id_token;
  };

  const getFirebaseIdToken = async (): Promise<string | null> => {
    if (signedOut) return null;
    if (firebaseToken && tokenFresh(firebaseToken)) return firebaseToken.token;
    if (staticFirebaseToken && !email) {
      if (firebaseToken && firebaseToken.expiresAt > now()) return firebaseToken.token;
      throw new Error('MAESTRO_FIREBASE_ID_TOKEN expired; provide renewable staging credentials.');
    }
    if (!apiKey || !email || !password) return firebaseToken?.token || null;
    firebaseRefresh ||= refreshFirebaseToken().finally(() => { firebaseRefresh = null; });
    return firebaseRefresh;
  };

  const exchangeDebugToken = async (): Promise<string> => {
    if (!apiKey || !appId || !projectNumber || !debugToken) {
      throw new Error(
        'App Check requires MAESTRO_FIREBASE_APP_CHECK_TOKEN or '
        + 'MAESTRO_FIREBASE_API_KEY, MAESTRO_FIREBASE_APP_ID and MAESTRO_APPCHECK_DEBUG_TOKEN.',
      );
    }
    const response = await fetchImpl(
      `https://firebaseappcheck.googleapis.com/v1/projects/${encodeURIComponent(projectNumber)}`
        + `/apps/${encodeURIComponent(appId)}:exchangeDebugToken?key=${encodeURIComponent(apiKey)}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ debugToken, limitedUse: false }),
      },
    );
    const result = await readJson<AppCheckResponse>(response, 'Firebase App Check');
    appCheckToken = {
      token: result.token,
      expiresAt: now() + parseDurationMs(result.ttl, 30 * 60_000),
    };
    return result.token;
  };

  const getAppCheckToken = async (): Promise<string | null> => {
    if (signedOut) return null;
    if (appCheckToken && tokenFresh(appCheckToken)) return appCheckToken.token;
    if (staticAppCheckToken && !debugToken) {
      if (appCheckToken && appCheckToken.expiresAt > now()) return appCheckToken.token;
      throw new Error('MAESTRO_FIREBASE_APP_CHECK_TOKEN expired; provide a registered debug token.');
    }
    if (!apiKey || !appId || !projectNumber || !debugToken) return appCheckToken?.token || null;
    appCheckRefresh ||= exchangeDebugToken().finally(() => { appCheckRefresh = null; });
    return appCheckRefresh;
  };

  return {
    async getManagedHeaders(required) {
      if (signedOut && required) throw new Error('The headless client is signed out.');
      const [idToken, attestation] = await Promise.all([
        getFirebaseIdToken(),
        getAppCheckToken(),
      ]);
      if (required && !idToken) {
        throw new Error('Firebase authentication is not configured for managed headless commands.');
      }
      const headers: Record<string, string> = {};
      if (idToken) headers.Authorization = `Bearer ${idToken}`;
      if (attestation) headers['X-Firebase-AppCheck'] = attestation;
      return headers;
    },
    getFirebaseIdToken,
    getAppCheckToken,
    async getUserId() {
      await getFirebaseIdToken();
      return userId;
    },
    async signIn() {
      signedOut = false;
      const token = await getFirebaseIdToken();
      if (!token) throw new Error('Firebase authentication is not configured for managed headless commands.');
      return token;
    },
    signOut() {
      signedOut = true;
      firebaseToken = null;
      appCheckToken = null;
      refreshToken = '';
      userId = null;
    },
    describe: () => ({
      firebase: staticFirebaseToken ? 'static-token' : (apiKey && email && password ? 'password' : 'missing'),
      appCheck: staticAppCheckToken ? 'static-token' : (appId && debugToken ? 'debug-token' : 'missing'),
      signedOut,
      userId,
    }),
  };
};
