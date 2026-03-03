// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface ColorVariable {
  cssVar: string;
  friendlyName: string;
  /** Short description of what this color affects, for non-technical users. */
  description?: string;
}

export interface ColorGroup {
  groupName: string;
  /** Short explanation of this group shown below the group title. */
  groupDescription?: string;
  colors: ColorVariable[];
  /** If true, group starts collapsed in the panel. */
  collapsedByDefault?: boolean;
}

export const COLOR_GROUPS: ColorGroup[] = [
  {
    groupName: 'App Canvas',
    groupDescription: 'The full-screen paper and main writing color',
    colors: [
      { cssVar: 'background', friendlyName: 'Screen Background', description: 'Main app background behind everything' },
      { cssVar: 'foreground', friendlyName: 'Main Text', description: 'Default text color used in most places' },
      { cssVar: 'page-bg', friendlyName: 'Chat Area Background', description: 'Chat content area background; also the high-contrast text color on thick-sketch-border dark buttons (Save All, dismiss)' },
      { cssVar: 'page-hover', friendlyName: 'Paper Hover Shade', description: 'Hover shade on paper surfaces — darker stripe used when hovering suggestion chips and paper-colored elements' },
      { cssVar: 'deep-ink', friendlyName: 'Deep Ink Text', description: 'Strong dark ink color used for emphasis text and ink marks' },
    ],
  },
  {
    groupName: 'Cards and Popups',
    groupDescription: 'Message cards, dialogs, and secondary text',
    colors: [
      { cssVar: 'panel-surface', friendlyName: 'Panel & Card Fill', description: 'Background fill for panels, cards, and control surfaces: color tuner panel, quick theme buttons, API key gate cards and text inputs, chat control buttons, bookmark note field, and the text input box in suggestion mode' },
      { cssVar: 'panel-surface-text', friendlyName: 'Panel & Card Text', description: 'Text color inside panels, cards, and bubbles' },
      { cssVar: 'popup-bg', friendlyName: 'Popup & Dialog Background', description: 'Popup menu and dialog background color' },
      { cssVar: 'popup-text', friendlyName: 'Popup & Dialog Text', description: 'Text inside popups and dialogs' },
      { cssVar: 'hover-fill', friendlyName: 'Hover State Fill', description: 'Hover background fill: selected color swatch in the color tuner, hover state on theme buttons and control buttons, and section fill inside the API key gate (instruction area, option buttons)' },
      { cssVar: 'faded-label', friendlyName: 'Subdued Icons & Labels', description: 'Subdued/secondary text and icons: color tuner labels, profile icon, swap avatar icon, terminal icon, and API key gate descriptions' },
    ],
  },
  {
    groupName: 'Buttons and Actions',
    groupDescription: 'Main button colors for normal and danger actions',
    colors: [
      { cssVar: 'cta-btn', friendlyName: 'Send & Live Session Button', description: 'Main CTA button fill: the send button in suggestion mode and the live session start/stop button' },
      { cssVar: 'cta-btn-text', friendlyName: 'Send & Live Session Button Text', description: 'Text/icon color on the main CTA button: send button text in suggestion mode and live session button text' },
      { cssVar: 'chip-surface', friendlyName: 'Suggestion Chips & Hover Fill', description: 'Fill for suggestion list chips, the outer input wrapper in suggestion mode, chat control button hover states, and camera controls background' },
      { cssVar: 'chip-text', friendlyName: 'Suggestion Chip Text', description: 'Text and icons on suggestion chips and hover-state surfaces' },
      { cssVar: 'action-accent', friendlyName: 'Action Accent Color', description: 'Blue highlight color: API key gate save/confirm buttons, text field focus rings, suggestion item keyboard focus rings, selected-preset ring in the color tuner, links, bookmark action hovers, and audio waveform played bar' },
      { cssVar: 'action-accent-text', friendlyName: 'Action Accent Text', description: 'Text on action-accent–colored buttons and surfaces (Save Key, Open AI Studio, bookmark bar)' },
      { cssVar: 'danger-color', friendlyName: 'Error & Danger Color', description: 'Error and danger color: error message backgrounds (tinted), error text, invalid API key messages, and delete saved theme button' },
      { cssVar: 'danger-color-text', friendlyName: 'Error & Danger Text', description: 'Text on error/danger elements' },
      { cssVar: 'delete-msg-btn', friendlyName: 'Delete Message Button', description: 'Delete message button background in normal (non-suggestion) mode — only this one button' },
    ],
  },
  {
    groupName: 'Borders and Focus',
    groupDescription: 'Outlines and focus glow',
    colors: [
      { cssVar: 'ui-border', friendlyName: 'General Border Line', description: 'Most borders and separator lines throughout the UI' },
      { cssVar: 'form-field-outline', friendlyName: 'Form Field Outline', description: 'Outline color for text input fields and form controls' },
      { cssVar: 'kbd-focus-ring', friendlyName: 'Keyboard Focus Ring', description: 'Glow ring shown when a control receives keyboard focus' },
      { cssVar: 'sketch-ring', friendlyName: 'Thin Sketch Outline & Input Glow', description: 'Color for thin (1.5px) hand-drawn outlines (sketchy-border-thin on suggestions, assistant messages, API key gate sections, language selector) and the glowing ring around the text input box while typing' },
    ],
  },
  {
    groupName: 'Status Flag: Hold',
    groupDescription: 'Top-left flag when you pause maestro',
    colors: [
      { cssVar: 'status-hold-bg', friendlyName: 'Hold Background', description: 'Flag background in hold mode' },
      { cssVar: 'status-hold-border', friendlyName: 'Hold Border', description: 'Flag border in hold mode' },
      { cssVar: 'status-hold-text', friendlyName: 'Hold Text', description: 'Flag icon/text in hold mode' },
    ],
  },
  {
    groupName: 'Status Flag: Speaking and Typing',
    groupDescription: 'Top-left flag while maestro is actively responding',
    colors: [
      { cssVar: 'status-speaking-bg', friendlyName: 'Speaking Background', description: 'Flag background while maestro is speaking' },
      { cssVar: 'status-speaking-border', friendlyName: 'Speaking Border', description: 'Flag border while maestro is speaking' },
      { cssVar: 'status-speaking-text', friendlyName: 'Speaking Text', description: 'Flag icon/text while maestro is speaking' },
      { cssVar: 'status-typing-bg', friendlyName: 'Typing Background', description: 'Flag background while maestro is typing' },
      { cssVar: 'status-typing-border', friendlyName: 'Typing Border', description: 'Flag border while maestro is typing' },
      { cssVar: 'status-typing-text', friendlyName: 'Typing Text', description: 'Flag icon/text while maestro is typing' },
    ],
  },
  {
    groupName: 'Status Flag: Listening, Observing, Idle',
    groupDescription: 'Top-left flag while maestro is listening for your voice, watching, or idle',
    colors: [
      { cssVar: 'status-listening-bg', friendlyName: 'Listening Background', description: 'Flag background while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-listening-border', friendlyName: 'Listening Border', description: 'Flag border while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-listening-text', friendlyName: 'Listening Text', description: 'Flag icon/text while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-observing-bg', friendlyName: 'Observing Background', description: 'Flag background while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-border', friendlyName: 'Observing Border', description: 'Flag border while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-text', friendlyName: 'Observing Text', description: 'Flag icon/text while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-high-bg', friendlyName: 'About To Engage Background', description: 'Flag background when maestro is about to engage' },
      { cssVar: 'status-observing-high-border', friendlyName: 'About To Engage Border', description: 'Flag border when maestro is about to engage' },
      { cssVar: 'status-observing-high-text', friendlyName: 'About To Engage Text', description: 'Flag icon/text when maestro is about to engage' },
      { cssVar: 'status-idle-bg', friendlyName: 'Idle Background', description: 'Flag background when nothing is running' },
      { cssVar: 'status-idle-border', friendlyName: 'Idle Border', description: 'Flag border when nothing is running' },
      { cssVar: 'status-idle-text', friendlyName: 'Idle Text', description: 'Flag icon/text when nothing is running' },
      { cssVar: 'status-busy-bg', friendlyName: 'Busy Background', description: 'Flag background tint while background tasks are active' },
      { cssVar: 'status-busy-border', friendlyName: 'Busy Border', description: 'Flag border tint while background tasks are active' },
      { cssVar: 'status-busy-text', friendlyName: 'Busy Text', description: 'Flag icon/text while background tasks are active' },
    ],
  },
  {
    groupName: 'User Message Bubbles',
    groupDescription: 'Background and text color of messages you send',
    colors: [
      { cssVar: 'user-bubble-bg', friendlyName: 'User Bubble Background', description: 'Background color of your sent messages' },
      { cssVar: 'user-bubble-text', friendlyName: 'User Bubble Text', description: 'Text and icon color inside your sent messages' },
    ],
  },
  {
    groupName: 'Chat Input Area',
    groupDescription: 'The dark text-input box where you type your message',
    colors: [
      { cssVar: 'chat-input-bg', friendlyName: 'Input Area Background', description: 'Background color of the text input box in normal chat mode; also the selected-language highlight in the expanded language selector' },
      { cssVar: 'chat-input-text', friendlyName: 'Input Area Text & Icons', description: 'Text and icon color inside the text input box: globe, target, speaker, mic, paperclip, and typed text (normal mode)' },
    ],
  },
  {
    groupName: 'Session Controls Bar',
    groupDescription: 'The bottom bar with the action pill, avatar, and session buttons',
    colors: [
      { cssVar: 'session-bar-bg', friendlyName: 'Session Bar Background', description: 'Background of the session controls pill, avatar buttons, and cancel/undo buttons in the bottom bar' },
      { cssVar: 'session-bar-text', friendlyName: 'Session Bar Icons & Text', description: 'Icon and text color in the session controls bar' },
    ],
  },
  {
    groupName: 'Traffic Log Panel',
    groupDescription: 'The developer traffic log panel (opened with the terminal icon)',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'debug-panel-bg', friendlyName: 'Traffic Log Background', description: 'Background color of the traffic log panel' },
      { cssVar: 'debug-panel-text', friendlyName: 'Traffic Log Text & Icons', description: 'Text and icon color in the traffic log header and request payloads' },
    ],
  },
  {
    groupName: 'Assistant Message Bubbles',
    groupDescription: 'Background and text color of AI response messages',
    colors: [
      { cssVar: 'assistant-bubble-bg', friendlyName: 'Assistant Bubble Background', description: 'Background color of AI (Maestro) response message bubbles, their file and video attachment placeholders, the line highlight during text playback, and the annotation confirm (✓) button that appears when drawing on an image' },
      { cssVar: 'assistant-bubble-text', friendlyName: 'Assistant Bubble Text', description: 'Text and icon color inside AI response message bubbles and their attachment placeholders' },
    ],
  },
  {
    groupName: 'Status & Thinking Messages',
    groupDescription: 'System messages: the "thinking…" placeholder and session status notes',
    colors: [
      { cssVar: 'status-bubble-bg', friendlyName: 'Status Message Background', description: 'Background of "thinking…" placeholder and system status messages (e.g. session started)' },
      { cssVar: 'status-bubble-text', friendlyName: 'Status Message Text', description: 'Text color of status and thinking messages' },
    ],
  },
  {
    groupName: 'Input Area Outer Surround',
    groupDescription: 'The outer sketchy-shaped wrapper around the text input box (normal chat mode)',
    colors: [
      { cssVar: 'input-surround-bg', friendlyName: 'Input Surround Background', description: 'Color of the sketchy shaped outer wrapper that frames the text input box in normal chat mode. This is an independent token — changing it only affects this wrapper and nothing else. It starts with the same value as Accent Button in the built-in presets, but once you paint it separately it will not follow Accent Button changes' },
      { cssVar: 'input-surround-text', friendlyName: 'Input Surround Text', description: 'Text and icon color on the outer wrapper around the input box in normal chat mode. Independent from all other tokens' },
    ],
  },
  {
    groupName: 'Input Area Attachment Thumbnails',
    groupDescription: 'The colored thumbnail wrapper shown when you attach an image, video, audio or file before sending',
    colors: [
      { cssVar: 'attachment-thumb-bg', friendlyName: 'Attachment Thumbnail Background', description: 'Background color of the attachment preview card in the input area (the small wrapper that appears when you pick an image, video, audio or file to send)' },
      { cssVar: 'attachment-thumb-text', friendlyName: 'Attachment Thumbnail Text & Icons', description: 'Text and icon color inside the attachment preview card (file name, file type icon, and placeholder icons)' },
    ],
  },
  {
    groupName: 'API Key Button',
    groupDescription: 'Top-right button that shows key present/missing',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'api-key-valid-bg', friendlyName: 'Key Present Background', description: 'API key button background when key exists' },
      { cssVar: 'api-key-valid-hover-bg', friendlyName: 'Key Present Hover', description: 'API key button hover color when key exists' },
      { cssVar: 'api-key-valid-text', friendlyName: 'Key Present Text', description: 'API key button text/icon when key exists' },
      { cssVar: 'api-key-missing-bg', friendlyName: 'Key Missing Background', description: 'API key button background when key is missing' },
      { cssVar: 'api-key-missing-hover-bg', friendlyName: 'Key Missing Hover', description: 'API key button hover color when key is missing' },
      { cssVar: 'api-key-missing-text', friendlyName: 'Key Missing Text', description: 'API key button text/icon when key is missing' },
    ],
  },
  {
    groupName: 'Microphone Recording Button',
    groupDescription: 'Hold-to-record and listening mic states',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'recording-mic-armed-bg', friendlyName: 'Mic Recording Background', description: 'Mic button while hold-to-record is active' },
      { cssVar: 'recording-mic-armed-text', friendlyName: 'Mic Recording Icon', description: 'Mic icon while hold-to-record is active' },
      { cssVar: 'recording-mic-armed-ring', friendlyName: 'Mic Recording Ring', description: 'Ring around mic while hold-to-record is active' },
      { cssVar: 'recording-mic-listening-bg', friendlyName: 'Mic Listening Background', description: 'Mic button while speech-to-text is listening' },
      { cssVar: 'recording-mic-listening-text', friendlyName: 'Mic Listening Icon', description: 'Mic icon while speech-to-text is listening' },
      { cssVar: 'recording-mic-pulse-outer', friendlyName: 'Mic Pulse Outer', description: 'Outer pulse ring while hold-to-record is active' },
      { cssVar: 'recording-mic-pulse-inner', friendlyName: 'Mic Pulse Inner', description: 'Inner pulse ring while hold-to-record is active' },
    ],
  },
  {
    groupName: 'Live and Attachment Recording Controls',
    groupDescription: 'Live chip, stop squares, remove buttons, and inline recording errors',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'recording-live-chip-bg', friendlyName: 'Live Chip Background', description: 'The small LIVE badge background in live preview' },
      { cssVar: 'recording-live-chip-text', friendlyName: 'Live Chip Text', description: 'Text color inside the LIVE badge' },
      { cssVar: 'recording-live-chip-dot', friendlyName: 'Live Chip Dot', description: 'Blinking dot inside the LIVE badge' },
      { cssVar: 'recording-live-stop-bg', friendlyName: 'Live Stop Button Background', description: 'Round stop button background while live session is active' },
      { cssVar: 'recording-live-stop-hover-bg', friendlyName: 'Live Stop Button Hover', description: 'Round stop button hover color while live session is active' },
      { cssVar: 'recording-live-stop-text', friendlyName: 'Live Stop Button Foreground', description: 'Foreground color on live-session stop button' },
      { cssVar: 'recording-live-stop-icon', friendlyName: 'Live Stop Square Icon', description: 'Square stop icon for live-session stop button' },
      { cssVar: 'recording-local-stop-bg', friendlyName: 'Local Stop Button Background', description: 'Round stop button background while local video recording is active' },
      { cssVar: 'recording-local-stop-hover-bg', friendlyName: 'Local Stop Button Hover', description: 'Round stop button hover while local video recording is active' },
      { cssVar: 'recording-local-stop-text', friendlyName: 'Local Stop Button Foreground', description: 'Foreground color on local-video stop button' },
      { cssVar: 'recording-local-stop-icon', friendlyName: 'Local Stop Square Icon', description: 'Square stop icon for local-video recording stop button' },
      { cssVar: 'recording-remove-bg', friendlyName: 'Remove Attachment Background', description: 'Round X button background for removing an attachment' },
      { cssVar: 'recording-remove-hover-bg', friendlyName: 'Remove Attachment Hover', description: 'Round X button hover color for removing an attachment' },
      { cssVar: 'recording-remove-text', friendlyName: 'Remove Attachment Icon', description: 'X icon color on remove-attachment button' },
      { cssVar: 'recording-indicator-dot', friendlyName: 'REC Dot', description: 'Tiny REC indicator dot while local recording is active' },
      { cssVar: 'recording-inline-error-bg', friendlyName: 'Inline Recording Error Background', description: 'Inline error background related to recording/live issues' },
      { cssVar: 'recording-inline-error-text', friendlyName: 'Inline Recording Error Text', description: 'Inline error text related to recording/live issues' },
      { cssVar: 'live-session-button-active-bg', friendlyName: 'Live Session Stop Button Background', description: 'Stop button background shown in the camera area while a live session is running' },
      { cssVar: 'live-session-button-active-hover-bg', friendlyName: 'Live Session Stop Button Hover', description: 'Stop button hover color in the camera area while a live session is running' },
      { cssVar: 'live-session-button-active-text', friendlyName: 'Live Session Stop Button Text', description: 'Stop button text color in the camera area while a live session is running' },
      { cssVar: 'live-session-button-error-bg', friendlyName: 'Live Session Retry Button Background', description: 'Retry button background shown in the camera area after a live session connection error' },
      { cssVar: 'live-session-button-error-hover-bg', friendlyName: 'Live Session Retry Button Hover', description: 'Retry button hover color in the camera area after a live session connection error' },
      { cssVar: 'live-session-button-error-text', friendlyName: 'Live Session Retry Button Text', description: 'Retry button text color in the camera area after a live session connection error' },
      { cssVar: 'live-overlay-button-error-bg', friendlyName: 'Overlay Live Button Error Background', description: 'Overlay LIVE button background when retry is needed' },
      { cssVar: 'live-overlay-button-error-hover-bg', friendlyName: 'Overlay Live Button Error Hover', description: 'Overlay LIVE button hover color when retry is needed' },
      { cssVar: 'live-overlay-button-error-text', friendlyName: 'Overlay Live Button Error Text', description: 'Overlay LIVE button text when retry is needed' },
    ],
  },
  {
    groupName: 'Action Confirmation Panels',
    groupDescription: 'Panels for load, delete, export, combine, and trim actions',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'action-load', friendlyName: 'Load Action Color', description: 'Accent color for load/import confirmation (button, input tint, and label)' },
      { cssVar: 'action-load-text', friendlyName: 'Load Action Text', description: 'Text color on the load action confirm button and inside the input field' },
      { cssVar: 'action-danger', friendlyName: 'Delete Action Color', description: 'Accent color for delete/reset confirmation (button, input tint, and label)' },
      { cssVar: 'action-danger-text', friendlyName: 'Delete Action Text', description: 'Text color on the delete action confirm button and inside the input field' },
      { cssVar: 'action-export', friendlyName: 'Export Action Color', description: 'Accent color for export confirmation (button, input tint, and label)' },
      { cssVar: 'action-export-text', friendlyName: 'Export Action Text', description: 'Text color on the export action confirm button and inside the input field' },
      { cssVar: 'action-combine', friendlyName: 'Combine Action Color', description: 'Accent color for combine/merge confirmation (button, input tint, and label)' },
      { cssVar: 'action-combine-text', friendlyName: 'Combine Action Text', description: 'Text color on the combine action confirm button and inside the input field' },
      { cssVar: 'action-trim', friendlyName: 'Trim Action Color', description: 'Accent color for trim confirmation (button, input tint, and label)' },
      { cssVar: 'action-trim-text', friendlyName: 'Trim Action Text', description: 'Text color on the trim action confirm button and inside the input field' },
      { cssVar: 'action-danger-shortcut-hover-bg', friendlyName: 'Delete Shortcut Hover Background', description: 'Hover background of the small delete shortcut button in session controls' },
      { cssVar: 'action-danger-shortcut-hover-text', friendlyName: 'Delete Shortcut Hover Icon', description: 'Hover icon color of the small delete shortcut button in session controls' },
      { cssVar: 'action-trim-shortcut-hover-bg', friendlyName: 'Trim Shortcut Hover Background', description: 'Hover background of the small trim shortcut button in session controls' },
      { cssVar: 'action-trim-shortcut-hover-text', friendlyName: 'Trim Shortcut Hover Icon', description: 'Hover icon color of the small trim shortcut button in session controls' },
    ],
  },
  {
    groupName: 'Voice Identity',
    groupDescription: 'Color ring identity for each voice character',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'voice-zephyr', friendlyName: 'Voice: Zephyr', description: 'Identity color for Zephyr voice' },
      { cssVar: 'voice-puck', friendlyName: 'Voice: Puck', description: 'Identity color for Puck voice' },
      { cssVar: 'voice-charon', friendlyName: 'Voice: Charon', description: 'Identity color for Charon voice' },
      { cssVar: 'voice-kore', friendlyName: 'Voice: Kore', description: 'Identity color for Kore voice' },
      { cssVar: 'voice-fenrir', friendlyName: 'Voice: Fenrir', description: 'Identity color for Fenrir voice' },
    ],
  },
  {
    groupName: 'Notebook Style Details',
    groupDescription: 'Sketch lines, marker highlights, and correction marks',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'sketch-ink', friendlyName: 'Thick Sketch Border Color', description: 'Dark color for thick (2px) hand-drawn sketch borders on the input area, API key gate, and the Save All dark button' },
      { cssVar: 'sketch-pressed', friendlyName: 'Sketch Pressed / Hover Stroke', description: 'Color for emphasized pressed/hover pencil strokes on buttons (e.g. confirm annotation button, dismiss button hover)' },
      { cssVar: 'sketch-shadow', friendlyName: 'Sketch Shadow', description: 'Shadow color for hand-drawn sketch elements' },
      { cssVar: 'teal-accent', friendlyName: 'Teal Interactive Accent', description: 'Teal highlight color: global profile editing section (label, input border/background), selected native language flag ring, suggestion keyboard focus when creating, and debug log type labels' },
      { cssVar: 'word-mark-bg', friendlyName: 'Playback Word Highlight', description: 'Background color of the highlighted word during audio/text playback' },
      { cssVar: 'word-mark-text', friendlyName: 'Playback Word Text', description: 'Text color of the word that is currently highlighted during playback' },
      { cssVar: 'red-correction', friendlyName: 'Correction Red Mark', description: 'Red correction-pen color for error marks and correction overlays' },
    ],
  },
];

export const ALL_COLOR_VARS: ColorVariable[] = COLOR_GROUPS.flatMap(g => g.colors);
