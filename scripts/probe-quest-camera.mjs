// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real Chrome canvas/video lifecycle, with a recorded native JPEG. No user media or provider is accessed.
import sharp from 'sharp';
import { createHash } from 'node:crypto';
import { chromium } from 'playwright-core';
import { createServer } from 'vite';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';
const output = resolve(process.argv[2] || '.quest-evidence/virtual-camera'); await mkdir(output, { recursive: true });
const native = JSON.parse(await readFile('test-fixtures/browser/roomCapture.json', 'utf8'));
// Synthetic square sensor-shaped input tests transport only; it is never sent to a provider.
const sensorJpeg = await sharp(Buffer.from('<svg width="512" height="512" xmlns="http://www.w3.org/2000/svg"><rect width="512" height="512" fill="#268c49"/><path d="M100 200 L256 60 L412 200 H310 V430 H202 V200Z" fill="white"/><text x="20" y="490" font-size="22" fill="white">SYNTHETIC CAMERA TEST</text></svg>')).jpeg().toBuffer();
const sensorFixture = { data: sensorJpeg.toString('base64'), capture: { mimeType: 'image/jpeg', width: 512, height: 512, sha256: createHash('sha256').update(sensorJpeg).digest('hex') } };
let server, browser; const errors = [];
try {
 server = await createServer({ cacheDir: resolve(output, 'vite-cache'), optimizeDeps: { entries: ['test-fixtures/browser/quest-camera.html'] }, server: { host: '127.0.0.1', port: 0, strictPort: true, watch: null }, logLevel: 'warn', clearScreen: false }); await server.listen();
 const address = server.httpServer.address(); const url = `http://127.0.0.1:${address.port}/test-fixtures/browser/quest-camera.html`;
 browser = await chromium.launch({ channel: 'chrome', headless: true }); const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
 page.on('pageerror', error => errors.push(error.message));
 await page.addInitScript(() => { window.physicalMediaRequests = 0; navigator.mediaDevices.getUserMedia = async () => { window.physicalMediaRequests++; throw new Error('Physical media must not be requested in this virtual camera probe'); }; });
 await page.route('**/*', route => new URL(route.request().url()).hostname === '127.0.0.1' ? route.continue() : route.abort());
 await page.goto(url); await page.waitForFunction(() => Boolean(window.maestroBook && window.cameraFixture));
 await page.evaluate(({ native, sensorFixture }) => {
   const cameraSession = window.maestroBook.snapshot().camera.session; let revision = 0, frame = 0;
   window.nativeCameraPaused = false; window.fixtureCameraPermission = false; window.nativeFrameCount = 0;
   window.nativeCameraTimer = setInterval(() => {
     if (window.nativeCameraPaused) return;
     const request = window.maestroBook.snapshot().camera;
     const payload = { version: 1, session: cameraSession, host: 'a'.repeat(32), revision: ++revision, sources: ['maestro-camera:virtual-scene', 'maestro-camera:headset-camera'], sourceId: request.sourceId, status: request.requestId ? 'streaming' : 'ready', requestId: request.requestId };
     const physical = request.sourceId === 'maestro-camera:headset-camera';
     if (request.requestId && physical && !window.fixtureCameraPermission) { payload.status = 'failed'; payload.error = 'permission-required'; }
     else if (request.requestId && revision % 5 === 0) {
       const selected = physical ? sensorFixture : native;
       payload.frame = { sourceId: request.sourceId, capture: { ...selected.capture, captureId: (++frame).toString(16).padStart(32, '0'), capturedAt: new Date().toISOString() }, data: selected.data }; window.nativeFrameCount++;
     }
     window.maestroBook.cameraState(payload);
   }, 200);
 }, { native, sensorFixture });
 await page.locator('[data-source="maestro-camera:virtual-scene"]').waitFor();
 assert.equal(await page.evaluate(() => window.nativeFrameCount), 0);
 await page.locator('[data-source="maestro-camera:virtual-scene"]').click();
 await page.waitForFunction(() => window.cameraFixture.liveVideoStream?.active && document.querySelector('video').videoWidth === 512);
 const snapshot = await page.evaluate(() => window.cameraFixture.captureSnapshot(false)); assert.equal(snapshot.imageOrigin, 'virtual-scene');
 await writeFile(resolve(output, 'camera-preview.jpg'), Buffer.from(snapshot.base64.split(',')[1], 'base64'));
 await page.evaluate(() => window.startFixtureLive()); await page.waitForFunction(() => window.liveInputs.length > 0);
 const first = await page.evaluate(() => window.liveInputs[0]); assert.deepEqual(Object.keys(first), ['video']);
 await writeFile(resolve(output, 'live-frame.jpg'), Buffer.from(first.video.data, 'base64'));
 const handoff = await page.evaluate(() => window.stopFixtureLive()); assert.equal(handoff.frames[0].origin, 'virtual-scene'); assert.equal(handoff.frames[0].data, first.video.data);
 await page.evaluate(() => { window.nativeCameraPaused = true; });
 await page.waitForFunction(() => !window.cameraFixture.liveVideoStream);
 const stopped = await page.evaluate(async () => ({ request: window.maestroBook.snapshot().camera.requestId, snapshot: await window.cameraFixture.captureSnapshot(false) })); assert.equal(stopped.request, ''); assert.equal(stopped.snapshot, null);
 await page.locator('#off').click(); await page.evaluate(() => { window.nativeCameraPaused = false; });
 await page.locator('[data-source="maestro-camera:virtual-scene"]').waitFor(); await page.locator('[data-source="maestro-camera:virtual-scene"]').click();
 await page.waitForFunction(() => window.cameraFixture.liveVideoStream?.active);
 // Switching must end the virtual lease; permission acceptance alone starts nothing.
 await page.locator('[data-source="maestro-camera:headset-camera"]').click();
 await page.waitForFunction(() => window.cameraFixture.visualContextCameraError?.includes('Allow headset camera'));
 const countBeforeGrant = await page.evaluate(() => { window.fixtureCameraPermission = true; return window.nativeFrameCount; });
 await page.waitForTimeout(1200);
 assert.equal(await page.evaluate(() => window.nativeFrameCount), countBeforeGrant);
 assert.equal(await page.evaluate(() => window.maestroBook.snapshot().camera.requestId), '');
 await page.locator('#off').click(); await page.locator('[data-source="maestro-camera:headset-camera"]').click();
 await page.waitForFunction(() => window.cameraFixture.liveVideoStream?.active && document.querySelector('video').videoHeight === 512);
 const physicalSnapshot = await page.evaluate(() => window.cameraFixture.captureSnapshot(false)); assert.equal(physicalSnapshot.imageOrigin, 'headset-camera');
 await writeFile(resolve(output, 'synthetic-headset-preview.jpg'), Buffer.from(physicalSnapshot.base64.split(',')[1], 'base64'));
 await page.evaluate(() => window.startFixtureLive()); await page.waitForFunction(() => window.liveInputs.length > 0);
 const physicalLive = await page.evaluate(() => window.liveInputs[0]);
 const physicalHandoff = await page.evaluate(() => window.stopFixtureLive());
 assert.equal(physicalHandoff.frames[0].origin, 'headset-camera'); assert.equal(physicalHandoff.frames[0].data, physicalLive.video.data);
 await writeFile(resolve(output, 'synthetic-headset-live.jpg'), Buffer.from(physicalLive.video.data, 'base64'));
 const size = await sharp(Buffer.from(physicalLive.video.data, 'base64')).metadata(); assert.equal(size.width, 512); assert.equal(size.height, 536);
 await page.evaluate(() => window.maestroBook.lifecycle(true)); await page.waitForFunction(() => !window.cameraFixture.liveVideoStream);
 assert.equal(await page.evaluate(() => window.maestroBook.snapshot().camera.requestId), '');
 await page.screenshot({ path: resolve(output, 'camera-after-suspend.png') });
 await page.evaluate(() => clearInterval(window.nativeCameraTimer));
 assert.equal(await page.evaluate(() => window.physicalMediaRequests), 0);
 assert.deepEqual(errors, []); await writeFile(resolve(output, 'chrome.json'), JSON.stringify({ passed: true, source: 'recorded native JPEG through real Chrome canvas/video', liveHandoffOrigin: handoff.frames[0].origin, noPhysicalCameraRequest: true, interruption: true, firstFrameWidth: 512, physicalTransport: { input: "Synthetic square fixture; no physical sensor or provider exercised", aspectPreserved: true, permissionRequiresReselection: true, liveHandoffOrigin: physicalHandoff.frames[0].origin }, errors }, null, 2));
 console.log('Chrome camera passed: native selection, real video preview, snapshot origin, labeled Live frame, exact handoff bytes, stale-frame stop and suspend.');
} finally { await browser?.close(); await server?.close(); }
