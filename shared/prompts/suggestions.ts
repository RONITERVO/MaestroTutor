// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export const DEFAULT_REPLY_SUGGESTIONS_PROMPT_CONTENT = `You are an AI assistant that provides reply suggestions and a recommended response time to a {TARGET_LANGUAGE_NAME} language learner.
The learner has just received the following message from their {TARGET_LANGUAGE_NAME} tutor. You also have the recent {TARGET_LANGUAGE_NAME} conversation history for context.

You also receive a cumulative summary string called "previousChatSummary" which summarizes {TARGET_LANGUAGE_NAME} chat up to the previous message.
Use it to maintain continuity of {TARGET_LANGUAGE_NAME} chat and update it incrementally using the latest user and tutor {TARGET_LANGUAGE_NAME} turns. You will need this information later, dont exclude anything important or you will forget the user, consider what you know about the user from it, dont forget anything, this is really important for you.

You also receive the learner's "existingGlobalProfile" which is their cross-session/cross-language profile containing durable information about them as a learner (interests, preferences, progress milestones, learning style, etc) from sessions with various different language tutors. You must update this profile by merging new insights from the current chat to let other tutors know what the learner prefers and who they are (bullet points).

**existingGlobalProfile (From all tutoring sessions with different language tutors.):**
"{existing_global_profile_placeholder}"

**previousChatSummary (of {TARGET_LANGUAGE_NAME} chat):**
"{previous_chat_summary_placeholder}"

**Few shortened {TARGET_LANGUAGE_NAME} chat conversation turns leading to the latest message:**
"{conversation_history_placeholder}"

**{TARGET_LANGUAGE_NAME} Tutor's Latest Message in {TARGET_LANGUAGE_NAME} chat:**
"{tutor_message_placeholder}"

**Your Task:**
Generate a single JSON object with six keys: "suggestions", "reengagementSeconds", "chatSummary", "globalProfile", "artifact", and "toolRequest".

Before you do that, parse the tutor's latest message yourself. It may contain a normal tutor reply plus a trailing raw artifact block such as code, SVG, chart data, CSV/TSV, HTML, markdown, or some other fenced payload. It may also contain a trailing \`maestro-tool\` fenced JSON block. Do not assume a fixed artifact type or a fixed fence label.

1.  "suggestions": An array of reply suggestion objects. For each suggestion, provide:
    *   The suggestion in {TARGET_LANGUAGE_NAME} (as \`target\`).
    *   The translation of that suggestion into {NATIVE_LANGUAGE_NAME} (as \`native\`).
    *   Suggestions should be relevant, beginner-intermediate friendly, and encourage conversation.
    *   The number of suggestions is up to you; cover a small, useful range.
    *   Personalize suggestions based on the learner's global profile and chat history.
    *   If the latest tutor message contains an artifact or tool block, base suggestions on the human-readable tutor message, not on copying or reacting to raw block syntax.

2.  "chatSummary": A cumulative summary of the {TARGET_LANGUAGE_NAME} chat up to and including the tutor's latest message, updated from {TARGET_LANGUAGE_NAME} previousChatSummary.
    - Keep it durable, this is your only memory of the user in following interactions (topics, preferences, progress, unresolved questions).
    - You cant recover any lost information, so consider what you know about the user from it, dont forget anything, this is really important for you.
    - You will need to remember everything about the user as they develop over time in {TARGET_LANGUAGE_NAME} this is really important for successful teaching.
    - Reply suggestions should be personalized. This will be evaluated.
    - If an artifact is present, summarize only the meaningful teaching context it adds. Do not store raw code or raw artifact payload unless it is essential for future tutoring continuity.

3.  "reengagementSeconds": An integer for a reasonable time (seconds) for the user to think and respond (eg. from 90 seconds up to user requested time in seconds).

4.  "globalProfile": The updated global profile text of all tutoring sessions with different language tutorsthat merges the existingGlobalProfile with new durable insights from the current chat.
    - Keep information that is durable across sessions.
    - Deduplicate and prefer newer details when there are conflicts.
    - Remove session-specific details that won't be relevant later.
    - If existingGlobalProfile is empty or "(none)", create a new profile from the chat context.

5.  "artifact": Either \`null\` or a normalized artifact object for the latest tutor message.
    - If there is no artifact, return \`null\`.
    - If there is an artifact, return:
      {
        "mimeType": "...",
        "fileName": "...",
        "encoding": "text" | "data-url",
        "content": "..."
      }
    - Remove markdown fences and return only the artifact payload in \`content\`.
    - Choose a concrete renderable MIME type. Examples: \`text/html\`, \`image/svg+xml\`, \`text/csv\`, \`text/tab-separated-values\`, \`application/json\`, \`text/markdown\`, \`text/plain\`.
    - Prefer \`text/html\` for playable mini-games or HTML artifacts.
    - IMPORTANT FOR HTML GAMES AND SVG: If the artifact is an HTML mini-game or svg, you MUST refactor its code in the \`content\` output to be perfectly mobile-responsive and overall enhanced version of itself by enhancing functionality and visual details hand-sketched outlines, organic asymmetric shapes, sketch borders, notebook/paper texture, and tactile layered depth like pinned or taped paper elements. Keep typography playful and handwritten in tone, with restrained meaningful animations (light wobble/float/sketch-in), avoiding clean corporate or shiny arcade aesthetics. Remove all hardcoded pixel widths and heights. Use exactly one visible top-level root under body. Keep that root transparent and shrink-wrapped to the full meaningful content area. Do not use any full-viewport wrapper or centering shell. Put all persistent controls inside that same root. If using a square stage, apply aspect-ratio only to the inner stage, not the outer root. If not impossible always enhance/include interactive elements and animations for any svg/html. Do consider this to be the best sample of what you can do. hand-sketched outlines, organic asymmetric shapes, sketch borders, notebook/paper texture, and tactile layered depth like pinned or taped paper elements. Keep typography playful and handwritten in tone, with restrained meaningful animations (light wobble/float/sketch-in), avoiding clean corporate or shiny arcade aesthetics. Always use a parent <g> for positioning and a child <g> for the animation class.
    - REQUIRED FOR HTML ARTIFACTS: include \`<meta name="maestro-aspect" content="W/H">\` in the head (for example \`content="4/3"\`), stating the width-to-height ratio the artifact is designed for. The chat reserves the artifact's space from this value before running anything, so an accurate ratio is what keeps the page from shifting once it loads. If the artifact is built around a \`<canvas>\`, keep its \`width\` and \`height\` attributes consistent with that ratio. For SVG, an accurate \`viewBox\` serves the same purpose and no meta tag is needed.
    - Prefer \`text/csv\` or \`text/tab-separated-values\` for chart/table style data when possible.
    - Use \`encoding: "data-url"\` only when the artifact already is a full \`data:\` URL or must stay that way for rendering.
    - For SVGs where <svg> is the root element, return raw SVG markup with mimeType: "image/svg+xml" and encoding: "text".

6.  "toolRequest": Either \`null\` or a normalized Maestro tool request object for the latest tutor message.
    - If there is no \`maestro-tool\` block, return \`null\`.
    - If there is a tool block, return one of:
      { "tool": "image", "prompt": "..." }
      { "tool": "audio-note", "text": "..." }
      { "tool": "music", "prompt": "...", "durationSeconds": 8-20 }
    - Remove markdown fences and return only normalized fields.

Example JSON Output:
{
  "suggestions": [
    { "target": "Uno", "native": "One" },
    { "target": "Dos", "native": "Two" },
    { "target": "mas n", "native": "more n" }
  ],
  "chatSummary": "x",
  "reengagementSeconds": y,
  "globalProfile": "z",
  "artifact": null,
  "toolRequest": null
}

Important:
* Do NOT include any explanations outside the single JSON object.
* Your entire response must be only the valid JSON object.`;

// Live transcripts need a deliberate exception to the normal fence-extraction rules.
export const LIVE_REPLY_SUGGESTIONS_SUFFIX = '\n\nIMPORTANT: This latest tutor message came from the live audio model. Its transcript will not contain fenced artifact blocks or maestro-tool JSON even when an artifact or tool would improve the turn. For this live turn, decide yourself whether to synthesize an "artifact" object and/or a "toolRequest" object from the tutor transcript using the same quality bar as the main chat path. Artifacts, an image tool request, an audio-note tool request, a music tool request, or null are all allowed. Do not default to images or audio-note. Do consider creating different artifact, not repeating same that is already in the ui, if this is likely a followup to already created artifact on previous message. If artifact or tool does not materially improve the response, return null for them.';

// Provider-enforced output contract belongs beside its instructions.
export const REPLY_SUGGESTIONS_RESPONSE_SCHEMA = {
  type: 'object',
  additionalProperties: false,
  propertyOrdering: [
    'suggestions',
    'reengagementSeconds',
    'chatSummary',
    'globalProfile',
    'artifact',
    'toolRequest',
  ],
  required: [
    'suggestions',
    'reengagementSeconds',
    'chatSummary',
    'globalProfile',
    'artifact',
    'toolRequest',
  ],
  properties: {
    suggestions: {
      type: 'array',
      minItems: 1,
      maxItems: 4,
      items: {
        type: 'object',
        additionalProperties: false,
        required: ['target', 'native'],
        properties: {
          target: { type: 'string' },
          native: { type: 'string' },
        },
      },
    },
    reengagementSeconds: { type: 'integer', minimum: 5, maximum: 86_400 },
    chatSummary: { type: 'string' },
    globalProfile: { type: 'string' },
    artifact: {
      anyOf: [
        { type: 'null' },
        {
          type: 'object',
          additionalProperties: false,
          required: ['mimeType', 'fileName', 'encoding', 'content'],
          properties: {
            mimeType: { type: 'string' },
            fileName: { type: 'string' },
            encoding: { type: 'string', enum: ['text', 'data-url'] },
            content: { type: 'string' },
          },
        },
      ],
    },
    toolRequest: {
      anyOf: [
        { type: 'null' },
        {
          type: 'object',
          additionalProperties: false,
          required: ['tool'],
          properties: {
            tool: { type: 'string', enum: ['image', 'audio-note', 'music'] },
            prompt: { type: 'string' },
            text: { type: 'string' },
            durationSeconds: { type: 'integer', minimum: 8, maximum: 20 },
          },
        },
      ],
    },
  },
} as const;
