// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real Chrome canvas/video lifecycle, with a recorded native JPEG. No user media or provider is accessed.
import { chromium } from 'playwright-core';
import { createServer } from 'vite';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';
const output = resolve(process.argv[2] || '.quest-evidence/virtual-camera'); await mkdir(output, { recursive: true });
const native = JSON.parse(await readFile('test-fixtures/browser/roomCapture.json', 'utf8'));
let server, browser; const errors = [];
try {
 server = await createServer({ cacheDir: resolve(output, 'vite-cache'), optimizeDeps: { entries: ['test-fixtures/browser/quest-camera.html'] }, server: { host: '127.0.0.1', port: 0, strictPort: true, watch: null }, logLevel: 'warn', clearScreen: false }); await server.listen();
 const address = server.httpServer.address(); const url = `http://127.0.0.1:${address.port}/test-fixtures/browser/quest-camera.html`;
 browser = await chromium.launch({ channel: 'chrome', headless: true }); const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
 page.on('pageerror', error => errors.push(error.message));
 await page.addInitScript(() => { window.physicalMediaRequests = 0; navigator.mediaDevices.getUserMedia = async () => { window.physicalMediaRequests++; throw new Error('Physical media must not be requested in this virtual camera probe'); }; });
 await page.route('**/*', route => new URL(route.request().url()).hostname === '127.0.0.1' ? route.continue() : route.abort());
 await page.goto(url); await page.waitForFunction(() => Boolean(window.maestroBook && window.cameraFixture));
 await page.evaluate(native => {
   const cameraSession = window.maestroBook.snapshot().camera.session; let revision = 0, frame = 0;
   window.nativeCameraPaused = false; window.nativeFrameCount = 0;
   window.nativeCameraTimer = setInterval(() => {
     if (window.nativeCameraPaused) return;
     const request = window.maestroBook.snapshot().camera;
     const payload = { version: 1, session: cameraSession, host: 'a'.repeat(32), revision: ++revision, available: true, status: request.requestId ? 'streaming' : 'ready', requestId: request.requestId };
     if (request.requestId && revision % 5 === 0) {
       payload.frame = { capture: { ...native.capture, captureId: (++frame).toString(16).padStart(32, '0'), capturedAt: new Date().toISOString() }, data: native.data }; window.nativeFrameCount++;
     }
     window.maestroBook.cameraState(payload);
   }, 200);
 }, native);
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
 await page.evaluate(() => window.maestroBook.lifecycle(true)); await page.waitForFunction(() => !window.cameraFixture.liveVideoStream);
 assert.equal(await page.evaluate(() => window.maestroBook.snapshot().camera.requestId), '');
 await page.screenshot({ path: resolve(output, 'camera-after-suspend.png') });
 await page.evaluate(() => clearInterval(window.nativeCameraTimer));
 assert.equal(await page.evaluate(() => window.physicalMediaRequests), 0);
 assert.deepEqual(errors, []); await writeFile(resolve(output, 'chrome.json'), JSON.stringify({ passed: true, source: 'recorded native JPEG through real Chrome canvas/video', liveHandoffOrigin: handoff.frames[0].origin, noPhysicalCameraRequest: true, interruption: true, firstFrameWidth: 512, errors }, null, 2));
 console.log('Chrome camera passed: native selection, real video preview, snapshot origin, labeled Live frame, exact handoff bytes, stale-frame stop and suspend.');
} finally { await browser?.close(); await server?.close(); }
