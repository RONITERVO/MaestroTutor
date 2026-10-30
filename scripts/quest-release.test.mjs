// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import { readFile, mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { validateProfile, releaseEnvironment, stampWeb, fileInventory } from './quest-release.mjs';
const fixture = new URL('../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/release-profile.json', import.meta.url);
const profile = async () => JSON.parse(await readFile(fixture, 'utf8'));
it('accepts the shared native/web public profile fixture without claiming provider acceptance', async () => { expect(validateProfile(await profile()).versionCode).toBe(1); });
it.each([
  ['package','com.maestro.quest.development'], ['package',['com.maestro.quest']], ['versionCode',0], ['versionCode',1.5],
  ['versionName','1.0'], ['metaAppId',''], ['signingCertificateSha256','a'.repeat(64)], ['secret','never-allowed'],
])('rejects invalid release field %s', async (field,value) => { const p = await profile(); p[field] = value; expect(() => validateProfile(p)).toThrow('Quest release configuration'); });
it.each([
  ['questFirebaseAppId','1:123456:web:abcdef'], ['questFirebaseAppId','1:999:web:fedcba'],
  ['questAccountLinkVerificationUrl','https://chatwithmaestro.com/quest-link.html?code=secret'],
  ['backendBaseUrl','http://maestro-fixture.cloudfunctions.net/api'], ['backendBaseUrl','https://localhost/api'],
  ['questAttestationUrl','https://user:password@provider.invalid/proof'], ['firebaseAuthDomain','host/path'],
  ['firebaseAppCheckSiteKey',''], ['firebaseApiKey','unexpected\nline'], ['firebaseAppCheckDebugToken','private'],
])('rejects invalid web field %s', async (field,value) => { const p = await profile(); p.web[field] = value; expect(() => validateProfile(p)).toThrow('Quest release configuration'); });
it('removes inherited public secret/debug/checkout values and pins the profile', async () => {
  const p=await profile();const env=releaseEnvironment(p,{ PATH:'unchanged',VITE_GEMINI_API_KEY:'private',VITE_FIREBASE_APPCHECK_DEBUG_TOKEN:'private',VITE_BACKEND_BASE_URL:'https://wrong.invalid',MAESTRO_WEBVIEW_DEBUG:'1',VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED:'true' });
  expect(env.VITE_GEMINI_API_KEY).toBe('');expect(env.VITE_FIREBASE_APPCHECK_DEBUG_TOKEN).toBe('');
  expect(env.VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED).not.toBe('true');expect(env.MAESTRO_WEBVIEW_DEBUG).toBe('0');
  expect(env.VITE_BACKEND_BASE_URL).toBe(p.web.backendBaseUrl);expect(env.PATH).toBe('unchanged');
});
it('stamps all built files and detects a changed bundle by its content hash', async () => {
  const directory=await mkdtemp(path.join(tmpdir(),'maestro-release-'));
  try {
    await writeFile(path.join(directory,'index.html'),'book');await writeFile(path.join(directory,'quest-link.html'),'approval');
    expect(await stampWeb(fixture,directory)).toBe(2);
    const receipt=JSON.parse(await readFile(path.join(directory,'quest-release.json'),'utf8'));
    expect(receipt.files).toEqual(await fileInventory(directory));expect(receipt.profileSha256).toMatch(/^[a-f0-9]{64}$/);
    await writeFile(path.join(directory,'index.html'),'wrong build');expect(receipt.files).not.toEqual(await fileInventory(directory));
  } finally { await rm(directory,{recursive:true,force:true}); }
});
