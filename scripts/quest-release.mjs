// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHash } from 'node:crypto';
import { readFile, writeFile, readdir, lstat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
const root = fileURLToPath(new URL('../', import.meta.url));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const fail = field => { throw new Error(`Quest release configuration: check ${field}.`); };
const exact = (value, keys, field) => {
  if (!value || typeof value !== 'object' || Array.isArray(value) || Object.keys(value).sort().join(',') !== [...keys].sort().join(',')) fail(field);
};
export const webFields = {
  firebaseApiKey: 'VITE_FIREBASE_API_KEY', firebaseAuthDomain: 'VITE_FIREBASE_AUTH_DOMAIN', firebaseProjectId: 'VITE_FIREBASE_PROJECT_ID',
  firebaseAppId: 'VITE_FIREBASE_APP_ID', questFirebaseAppId: 'VITE_QUEST_FIREBASE_APP_ID', firebaseAppCheckSiteKey: 'VITE_FIREBASE_APPCHECK_SITE_KEY',
  backendBaseUrl: 'VITE_BACKEND_BASE_URL', questAttestationUrl: 'VITE_QUEST_ATTESTATION_URL',
  questAccountLinkUrl: 'VITE_QUEST_ACCOUNT_LINK_URL', questAccountLinkVerificationUrl: 'VITE_QUEST_ACCOUNT_LINK_VERIFY_URL',
};
export function validateProfile(value) {
  exact(value, ['version','package','versionName','versionCode','metaAppId','signingCertificateSha256','web'], 'profile fields (public values only; no secrets)');
  if (value.version !== 1) fail('version');
  if (typeof value.package !== 'string' || !/^[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+$/.test(value.package) || /(?:^|\.)(development|debug|example)(?:\.|$)/.test(value.package) || value.package.length > 150) fail('package');
  if (typeof value.versionName !== 'string' || !/^\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(value.versionName)) fail('versionName');
  if (!Number.isSafeInteger(value.versionCode) || value.versionCode < 1 || value.versionCode > 2100000000) fail('versionCode');
  if (typeof value.metaAppId !== 'string' || !/^[1-9][0-9]{5,24}$/.test(value.metaAppId)) fail('metaAppId');
  if (typeof value.signingCertificateSha256 !== 'string' || !/^[a-fA-F0-9]{64}$/.test(value.signingCertificateSha256) || /^(.)\1+$/.test(value.signingCertificateSha256)) fail('signingCertificateSha256');
  exact(value.web, Object.keys(webFields), 'web fields');
  for (const [key, item] of Object.entries(value.web)) if (typeof item !== 'string' || item !== item.trim() || !item || item.length > 2048 || /[\r\n\0]/.test(item)) fail(key);
  const w = value.web, app = /^1:([0-9]+):(web|android):[a-f0-9]+$/;
  if (!/^1:[0-9]+:web:[a-f0-9]+$/.test(w.firebaseAppId)) fail('firebaseAppId');
  if (!app.test(w.questFirebaseAppId) || w.questFirebaseAppId === w.firebaseAppId || w.questFirebaseAppId.match(app)[1] !== w.firebaseAppId.match(app)[1]) fail('questFirebaseAppId (distinct registration in the same Firebase project)');
  if (!/^[a-z][a-z0-9-]{4,28}[a-z0-9]$/.test(w.firebaseProjectId)) fail('firebaseProjectId');
  if (!/^(?:[a-z0-9-]+\.)+[a-z]{2,}$/i.test(w.firebaseAuthDomain) || w.firebaseAuthDomain.endsWith('.localhost')) fail('firebaseAuthDomain');
  for (const key of ['backendBaseUrl','questAttestationUrl','questAccountLinkUrl','questAccountLinkVerificationUrl']) {
    let u; try { u = new URL(w[key]); } catch { fail(key); }
    if (u.protocol !== 'https:' || u.username || u.password || u.search || u.hash || u.port || !/^(?:[a-z0-9-]+\.)+[a-z]{2,}$/i.test(u.hostname) || u.hostname.endsWith('.localhost')) fail(key);
  }
  if (new Set([w.backendBaseUrl,w.questAttestationUrl,w.questAccountLinkUrl]).size !== 3) fail('separate API/attestation/pairing endpoints');
  if (w.questAccountLinkVerificationUrl !== 'https://chatwithmaestro.com/quest-link.html') fail('questAccountLinkVerificationUrl (native allowlist)');
  return value;
}
export function releaseEnvironment(profile, inherited = process.env) {
  validateProfile(profile);
  // Blank all existing VITE_* values, including keys in dotenv files, then
  // supply only the public release profile. No debug/checkout flag is inherited.
  const env = { ...inherited, NODE_ENV: 'production', MAESTRO_WEBVIEW_DEBUG: '0', VITE_FIREBASE_APPCHECK_DEBUG_TOKEN: '', VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED: 'false' };
  for (const name of Object.keys(env)) if (name.startsWith('VITE_')) env[name] = '';
  for (const [field, name] of Object.entries(webFields)) env[name] = profile.web[field];
  return env;
}
export async function fileInventory(directory) {
  const files = [];
  async function visit(relative = '') {
    for (const item of await readdir(path.join(directory, relative), { withFileTypes: true })) {
      const name = relative ? `${relative}/${item.name}` : item.name;
      if (item.isSymbolicLink()) throw new Error('Quest release inputs cannot contain symbolic links.');
      if (item.isDirectory()) await visit(name);
      else if (item.isFile() && name !== 'quest-release.json') files.push({ path: name, sha256: hash(await readFile(path.join(directory, name))) });
    }
  }
  if ((await lstat(directory)).isSymbolicLink()) throw new Error('Quest release input root cannot be a symbolic link.');
  await visit(); return files.sort((a,b) => a.path.localeCompare(b.path, 'en'));
}
export async function stampWeb(profileFile, directory) {
  const source = await readFile(profileFile); validateProfile(JSON.parse(source.toString('utf8')));
  const files = await fileInventory(directory);
  for (const entry of ['index.html','quest-link.html']) if (!files.some(file => file.path === entry)) throw new Error('Quest release web entry is missing.');
  await writeFile(path.join(directory,'quest-release.json'), JSON.stringify({ version: 1, profileSha256: hash(source), files }, null, 2) + '\n');
  return files.length;
}
async function main() {
  const [operation, input] = process.argv.slice(2);
  if (!['check','build-web'].includes(operation) || !input || process.argv.length !== 4) throw new Error('Usage: node scripts/quest-release.mjs check|build-web <public-profile.json>');
  const filename = path.resolve(input), profile = validateProfile(JSON.parse(await readFile(filename, 'utf8')));
  if (operation === 'build-web') {
    const { loadEnv } = await import('vite');
    const env = releaseEnvironment(profile, { ...loadEnv('production', root, 'VITE_'), ...process.env });
    for (const [binary, args] of [['typescript/bin/tsc', []], ['vite/bin/vite.js', ['build']]]) {
      const result = spawnSync(process.execPath, [path.join(root,'node_modules',binary), ...args], { cwd: root, env, stdio: 'inherit' });
      if (result.status !== 0) throw new Error('Quest release web build failed.');
    }
    await stampWeb(filename, path.join(root, 'dist'));
  }
  console.log(JSON.stringify({ configurationValid: true, package: profile.package, versionCode: profile.versionCode, providerAcceptanceVerified: false, storeAvailabilityVerified: false }));
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error instanceof SyntaxError ? 'Quest release profile is not valid JSON.' : error.message); process.exitCode = 1; });
