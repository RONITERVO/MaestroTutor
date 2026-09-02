// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { readFile } from 'node:fs/promises';

const read = path => readFile(new URL(`../${path}`, import.meta.url), 'utf8');
const failures = [];
const requireText = (condition, message) => {
  if (!condition) failures.push(message);
};
const envValue = (text, name) => (
  text.match(new RegExp(`^${name}=(.*)$`, 'm'))?.[1]?.trim() || ''
);

const [
  appPackage,
  functionsPackage,
  functionsIndex,
  functionsGemini,
  androidBuild,
  androidManifest,
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
] = await Promise.all([
  read('package.json'),
  read('functions/package.json'),
  read('functions/src/index.ts'),
  read('functions/src/gemini.ts'),
  read('android/app/build.gradle'),
  read('android/app/src/main/AndroidManifest.xml'),
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
]);

const app = JSON.parse(appPackage);
const functions = JSON.parse(functionsPackage);
requireText(app.scripts?.['maestro:rpc'], 'package.json must expose the JSON-RPC harness.');
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
requireText(!androidManifest.includes('com.android.vending.BILLING'), 'Android must not declare the retired Play Billing permission.');
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
