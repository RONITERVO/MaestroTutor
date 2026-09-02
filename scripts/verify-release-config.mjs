// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { spawnSync } from 'node:child_process';
import { mkdir, readFile } from 'node:fs/promises';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const read = path => readFile(new URL(`../${path}`, import.meta.url), 'utf8');
const failures = [];
const requireText = (condition, message) => {
  if (!condition) failures.push(message);
};
const envValue = (text, name) => (
  text.match(new RegExp(`^${name}=(.*)$`, 'm'))?.[1]?.trim() || ''
);

const isWindows = process.platform === 'win32';
const repositoryRoot = new URL('../', import.meta.url);
const capacitorCli = fileURLToPath(new URL('../node_modules/@capacitor/cli/bin/capacitor', import.meta.url));
// `cap update` resolves native plugins without copying web assets, but its
// generated plugin JSON still needs the ignored assets directory to exist.
await mkdir(new URL('../android/app/src/main/assets/public/', import.meta.url), { recursive: true });
const capacitorUpdate = spawnSync(
  process.execPath,
  [capacitorCli, 'update', 'android'],
  {
    cwd: repositoryRoot,
    encoding: 'utf8',
  },
);
requireText(
  capacitorUpdate.status === 0,
  `Capacitor Android plugins could not be updated: ${(
    capacitorUpdate.stderr || capacitorUpdate.stdout || capacitorUpdate.error?.message || 'unknown Capacitor error'
  ).trim()}`,
);
const capacitorConfigRead = spawnSync(
  process.execPath,
  [capacitorCli, 'config', '--json'],
  {
    cwd: repositoryRoot,
    encoding: 'utf8',
  },
);
requireText(
  capacitorConfigRead.status === 0,
  `Capacitor config could not be resolved: ${(
    capacitorConfigRead.stderr || capacitorConfigRead.stdout || capacitorConfigRead.error?.message || 'unknown Capacitor error'
  ).trim()}`,
);
const gradleExecutable = isWindows ? (process.env.ComSpec || 'cmd.exe') : './gradlew';
const gradleArgs = isWindows
  ? ['/d', '/s', '/c', 'gradlew.bat :app:processReleaseManifest --no-daemon']
  : [':app:processReleaseManifest', '--no-daemon'];
const mergedManifestBuild = capacitorUpdate.status === 0
  ? spawnSync(
    gradleExecutable,
    gradleArgs,
    {
      cwd: new URL('../android/', import.meta.url),
      encoding: 'utf8',
    },
  )
  : { status: null, stderr: '', stdout: '', error: null };
requireText(
  mergedManifestBuild.status === 0,
  `Android merged release manifest could not be generated: ${(
    mergedManifestBuild.stderr || mergedManifestBuild.stdout || mergedManifestBuild.error?.message || 'unknown Gradle error'
  ).trim()}`,
);
const androidManifest = mergedManifestBuild.status === 0
  ? await read('android/app/build/intermediates/merged_manifests/release/processReleaseManifest/AndroidManifest.xml')
  : '';

const [
  appPackage,
  functionsPackage,
  functionsIndex,
  functionsGemini,
  androidBuild,
  mainActivity,
  stagingEnv,
  functionsExample,
  headlessClient,
  managedGeminiClient,
  headlessAccess,
  firstLessonJourney,
  tutorConversation,
  liveSystemInstruction,
  liveStt,
  attachmentUploadPlans,
  headlessAttachmentJourney,
  firstLessonCoverage,
  stagingWorkflow,
  headlessCoverageDoc,
  officeTextExtraction,
  headlessAttachmentAdapters,
  replySuggestions,
  googleServices,
  androidVariables,
] = await Promise.all([
  read('package.json'),
  read('functions/package.json'),
  read('functions/src/index.ts'),
  read('functions/src/gemini.ts'),
  read('android/app/build.gradle'),
  read('android/app/src/main/java/com/ronitervo/maestrotutor/MainActivity.java'),
  read('.env.staging'),
  read('functions/.env.example'),
  read('src/headless/client.ts'),
  read('src/core-sdk/managedGeminiClient.ts'),
  read('src/headless/access.ts'),
  read('src/headless/firstLessonJourney.ts'),
  read('src/features/chat/hooks/useTutorConversation.ts'),
  read('src/features/live/utils/liveSystemInstruction.ts'),
  read('src/features/speech/hooks/useGeminiLiveStt.ts'),
  read('src/core-sdk/chat/attachmentUploadPlans.ts'),
  read('src/headless/attachmentJourney.ts'),
  read('src/headless/firstLessonCoverage.ts'),
  read('.github/workflows/headless-staging.yml'),
  read('docs/HEADLESS_COVERAGE.md'),
  read('src/core-sdk/chat/officeTextExtraction.ts'),
  read('src/headless/attachmentUploadAdapters.ts'),
  read('src/core-sdk/chat/suggestions.ts'),
  read('android/app/google-services.json'),
  read('android/variables.gradle'),
]);

const app = JSON.parse(appPackage);
const functions = JSON.parse(functionsPackage);
const nativeConfig = capacitorConfigRead.status === 0
  ? JSON.parse(capacitorConfigRead.stdout).app.extConfig
  : {};
const androidOAuthCertificateHashes = JSON.parse(googleServices).client
  ?.filter(client => (
    client.client_info?.android_client_info?.package_name === nativeConfig.appId
  ))
  .flatMap(client => client.oauth_client || [])
  .map(client => client.android_info?.certificate_hash?.toLowerCase())
  .filter(Boolean) || [];
requireText(app.scripts?.['maestro:rpc'], 'package.json must expose the JSON-RPC harness.');
requireText(
  nativeConfig.plugins?.FirebaseAuthentication?.providers?.includes('google.com'),
  'Packaged Android config must enable the native Google authentication provider.',
);
requireText(
  /rgcfaIncludeGoogle\s*=\s*true/.test(androidVariables),
  'Android Gradle variables must package the native Google authentication SDKs.',
);
requireText(
  androidOAuthCertificateHashes.includes('5a8dcea2d9069adcb8f521e9be28b9611ae53b01'),
  'google-services.json must include the Google Play app-signing SHA-1 OAuth client.',
);
requireText(!functions.dependencies?.googleapis, 'Functions must not restore the retired Google Play verifier dependency.');
requireText(!functionsIndex.includes('/billing/google-play/verify'), 'Functions must not expose a second purchase grant route.');
requireText(
  /app\.post\(\s*['"]\/gemini\/generate-music['"]\s*,\s*asyncRoute\(\s*['"]required['"]/.test(functionsIndex),
  'Functions must expose managed music through an authenticated required-auth route.',
);
requireText(functionsGemini.includes("apiVersion: 'v1alpha'"), 'The Lyria backend adapter must use its supported v1alpha WebSocket endpoint.');
requireText(
  /music:\s*\{\s*connect:\s*async\s*\(\)\s*=>\s*\{\s*throw new Error\(/s.test(managedGeminiClient),
  'Managed music connect must throw instead of minting an unsupported ephemeral Lyria token.',
);
requireText(functionsGemini.includes('trimMusicPcmChunk'), 'Managed music must trim PCM to the requested duration.');
requireText(functionsGemini.includes('isCompleteMusicSampleCount'), 'Managed music must reject partial provider closes.');
requireText(functionsGemini.includes('getManagedMusicLeaseDurationMs'), 'Managed music leases must cover the full provider timeout.');
requireText(!androidBuild.includes('com.android.billingclient'), 'Android must not ship a second purchase SDK.');
requireText(!androidManifest.includes('com.android.vending.BILLING'), 'Android merged release manifest must not contain the retired Play Billing permission.');
requireText(!mainActivity.includes('ManagedBillingPlugin'), 'Android must not register the retired billing plugin.');

const clientPacks = envValue(stagingEnv, 'VITE_MANAGED_CREDIT_PACK_IDS')
  .split(',')
  .map(entry => entry.trim())
  .filter(Boolean)
  .sort();
const backendPacks = envValue(functionsExample, 'MANAGED_CREDIT_PACKS')
  .split(',')
  .map(entry => entry.trim())
  .filter(Boolean)
  .map(entry => entry.split(':')[0].trim())
  .sort();
requireText(clientPacks.length > 0, 'Staging must advertise at least one managed credit pack.');
requireText(JSON.stringify(clientPacks) === JSON.stringify(backendPacks), 'Client and backend example pack ids must match exactly.');
for (const method of [
  'billing.checkout.completeTest',
  'chat.attachment.turn',
  'media.audioNote.generate',
  'media.music.generate',
  'speech.synthetic.live',
  'speech.transcribe',
  'speech.tts.generate',
  'live.conversation.turn',
  'live.observer.turn',
  'translation.create',
  'chat.reengage',
  'suggestions.process',
  'journey.firstLesson',
]) {
  requireText(headlessClient.includes(`'${method}'`), `Headless contract is missing ${method}.`);
}
requireText(headlessAccess.includes('MAESTRO_GEMINI_API_KEY'), 'BYOK headless mode must read its Gemini key from the environment.');
requireText(headlessAccess.includes('createDirectHeadlessFilePort'), 'BYOK headless mode must use the direct Files API adapter.');
for (const attachmentKind of ['text', 'image', 'audio', 'pdf', 'svg', 'video', 'office']) {
  requireText(firstLessonCoverage.includes(`'${attachmentKind}'`), `The first-lesson journey must retain ${attachmentKind} attachment coverage.`);
}
for (const toolKind of ['image', 'audio-note', 'music']) {
  requireText(firstLessonCoverage.includes(`'${toolKind}'`), `The first-lesson journey must retain the ${toolKind} suggestion tool afterstep.`);
}
requireText(firstLessonJourney.includes("mode: 'observer'"), 'The first-lesson journey must retain silent-observer coverage.');
requireText(firstLessonJourney.includes('useGoogleSearch: false'), 'Non-Search first-lesson turns must not inherit the earlier Search toggle.');
requireText(firstLessonCoverage.includes('cleanupFailureCount'), 'The first-lesson attachment gate must require confirmed cleanup.');
requireText(firstLessonCoverage.includes('visiblyStreamed'), 'The first-lesson reengagement gate must require visible streaming.');
requireText(tutorConversation.includes('executeSuggestionToolRequest'), 'The visual UI must use the shared suggestion afterstep dispatcher.');
requireText(tutorConversation.includes('normalizeCoreSuggestionCreatorArtifact'), 'The visual UI must use shared artifact normalization.');
requireText(tutorConversation.includes('buildCoreAttachmentUploadPlans'), 'The visual UI must use the shared attachment upload planner.');
requireText(headlessAttachmentJourney.includes('buildHeadlessAttachmentUploadPlans'), 'The headless client must use the shared attachment upload planner through its runtime adapters.');
requireText(attachmentUploadPlans.includes('resolveAttachmentStrategy'), 'Attachment upload orchestration must retain the shared MIME strategy.');
requireText(tutorConversation.includes('extractOfficeTextForUpload'), 'The visual UI must use shared Office upload extraction.');
requireText(headlessAttachmentAdapters.includes('extractOfficeTextForUpload'), 'The headless client must use shared Office upload extraction.');
requireText(officeTextExtraction.includes('JSZip.loadAsync'), 'Shared Office extraction must inspect the real OpenXML/ODF package.');
requireText(replySuggestions.includes('responseJsonSchema: REPLY_SUGGESTIONS_RESPONSE_SCHEMA'), 'Suggestion creation must enforce provider-side JSON structure for artifact-bearing replies.');
requireText(liveSystemInstruction.includes('buildCoreLiveSystemInstruction'), 'The visual UI must use shared Live context serialization.');
requireText(liveStt.includes('buildLiveSttSystemInstruction'), 'The visual UI must use the shared STT instruction contract.');
for (const coverageFlag of [
  'chatStreaming', 'stt', 'liveAudio', 'liveVisual', 'observerAudio',
  'observerVisual', 'suggestionAftersteps', 'translation', 'ttsTrigger',
  'audioCapture', 'reengagement',
]) {
  requireText(firstLessonCoverage.includes(`${coverageFlag}:`), `The first-lesson gate must assert ${coverageFlag}.`);
}
requireText(stagingWorkflow.includes('HEADLESS_GEMINI_API_KEY'), 'The staging workflow must accept an isolated BYOK provider credential.');
requireText(stagingWorkflow.includes('require_byok'), 'A release dispatch must be able to require rather than skip BYOK proof.');
requireText(stagingWorkflow.match(/journey\.firstLesson/g)?.length >= 2, 'Managed and BYOK jobs must both run the same first-lesson command.');
requireText(headlessCoverageDoc.includes('A skipped BYOK job is not evidence.'), 'Maintainer docs must explain that skipped BYOK validation is not release proof.');

if (failures.length) {
  for (const failure of failures) process.stderr.write(`release-config: ${failure}\n`);
  process.exitCode = 1;
} else {
  process.stdout.write(JSON.stringify({
    ok: true,
    activePurchaseProvider: 'stripe',
    creditPackIds: clientPacks,
    headlessCoverage: [
      'billing', 'chat-stream', 'google-search', 'attachments', 'stt', 'tts-trigger',
      'image', 'audio-note', 'music', 'suggestion-aftersteps', 'translation',
      'reengagement', 'live-audio', 'live-video', 'observer-audio', 'observer-video',
      'first-lesson', 'managed', 'byok',
    ],
  }, null, 2) + '\n');
}
