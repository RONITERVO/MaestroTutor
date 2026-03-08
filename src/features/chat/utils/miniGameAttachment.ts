// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

interface MiniGameAttachmentCandidate {
  sourceCode: string;
  fileName?: string | null;
  mimeType?: string | null;
}

interface MiniGameDocumentOptions extends MiniGameAttachmentCandidate {
  frameId: string;
}

const WEB_RUNTIME_EXTENSIONS = new Set(['html', 'htm', 'js', 'mjs', 'cjs']);
const WEB_RUNTIME_MIME_HINTS = [
  'text/html',
  'application/xhtml+xml',
  'text/javascript',
  'application/javascript',
];

const HTML_MARKUP_HINT = /<(!doctype\s+html|html|body|script|canvas|button|style|main|section|div)\b/i;
const GAMEPLAY_HINT = /data-maestro-mini-game|@maestro-mini-game|<canvas\b|<button\b|onclick\s*=|onpointerdown\s*=|addEventListener\(\s*["'](?:click|pointerdown|touchstart)|requestAnimationFrame|flashcard|vocab(?:ulary)?\s*quiz|word\s*match/i;
const BROWSER_SCRIPT_HINT = /\b(document|window)\.|getContext\(|querySelector\(|createElement\(|addEventListener\(/i;

const escapeForInlineScript = (value: string): string =>
  value.replace(/<\/script/gi, '<\\/script');

const getFileExtension = (fileName?: string | null): string => {
  const normalized = (fileName || '').trim().toLowerCase();
  const dot = normalized.lastIndexOf('.');
  if (dot < 0 || dot >= normalized.length - 1) return '';
  return normalized.slice(dot + 1);
};

const isLikelyModuleScript = (code: string): boolean =>
  /^\s*import\s+/m.test(code) || /^\s*export\s+/m.test(code);

const detectDocumentKind = (candidate: MiniGameAttachmentCandidate): 'html' | 'script' | 'unknown' => {
  const source = (candidate.sourceCode || '').trim();
  if (!source) return 'unknown';

  const ext = getFileExtension(candidate.fileName);
  if (ext === 'js' || ext === 'mjs' || ext === 'cjs') return 'script';
  if (ext === 'html' || ext === 'htm') return 'html';

  const mime = (candidate.mimeType || '').trim().toLowerCase();
  if (mime.includes('javascript')) return 'script';
  if (mime.includes('html') || mime.includes('xhtml')) return 'html';

  if (HTML_MARKUP_HINT.test(source)) return 'html';
  if (BROWSER_SCRIPT_HINT.test(source)) return 'script';
  return 'unknown';
};

const buildRuntimeBridge = (frameId: string): string => {
  const escapedFrameId = JSON.stringify(frameId);
  return `
<script>
(function () {
  var FRAME_ID = ${escapedFrameId};
  var EVENT_TYPE = 'maestro-mini-game-status';
  var sendStatus = function (status, detail) {
    try {
      parent.postMessage({ type: EVENT_TYPE, frameId: FRAME_ID, status: status, detail: detail || '' }, '*');
    } catch (_err) {}
  };

  var lockToClickAndTap = function () {
    window.addEventListener('keydown', function (event) {
      event.preventDefault();
      event.stopPropagation();
    }, true);
    window.addEventListener('contextmenu', function (event) {
      event.preventDefault();
    });
    window.addEventListener('dragstart', function (event) {
      event.preventDefault();
    });
  };

  window.addEventListener('error', function (event) {
    var msg = (event && event.message) ? String(event.message) : 'Runtime error';
    sendStatus('error', msg);
  });
  window.addEventListener('unhandledrejection', function (event) {
    var reason = event && event.reason;
    var msg = reason && reason.message ? String(reason.message) : String(reason || 'Unhandled promise rejection');
    sendStatus('error', msg);
  });

  window.addEventListener('DOMContentLoaded', function () {
    lockToClickAndTap();
    sendStatus('ready', 'ok');
  });
})();
</script>`;
};

const buildRuntimeStyle = (): string => `
<style>
  :root { color-scheme: light dark; }
  html, body {
    margin: 0;
    width: 100%;
    height: 100%;
    overflow: hidden;
    touch-action: manipulation;
    -webkit-user-select: none;
    user-select: none;
    background: #0f172a;
    color: #e2e8f0;
    font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, sans-serif;
  }
  * { box-sizing: border-box; }
  button, [role="button"], a, canvas, svg, input, textarea {
    touch-action: manipulation;
  }
</style>`;

const ensureHtmlDocument = (sourceCode: string): string => {
  const source = (sourceCode || '').trim();
  if (!source) {
    return '<!doctype html><html><head><meta charset="utf-8" /></head><body></body></html>';
  }

  if (/<html[\s>]/i.test(source) || /<!doctype\s+html/i.test(source)) {
    return source;
  }

  return `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
  </head>
  <body>${source}</body>
</html>`;
};

const injectIntoHead = (html: string, payload: string): string => {
  if (/<\/head>/i.test(html)) {
    return html.replace(/<\/head>/i, `${payload}</head>`);
  }

  if (/<html[^>]*>/i.test(html)) {
    return html.replace(/<html[^>]*>/i, (openTag) => `${openTag}<head>${payload}</head>`);
  }

  return `<head>${payload}</head>${html}`;
};

const injectIntoBody = (html: string, payload: string): string => {
  if (/<\/body>/i.test(html)) {
    return html.replace(/<\/body>/i, `${payload}</body>`);
  }
  return `${html}${payload}`;
};

export const isRunnableMiniGameAttachment = (candidate: MiniGameAttachmentCandidate): boolean => {
  const source = (candidate.sourceCode || '').trim();
  if (!source) return false;

  const ext = getFileExtension(candidate.fileName);
  const mime = (candidate.mimeType || '').trim().toLowerCase();
  const kind = detectDocumentKind(candidate);

  const hasRuntimeHint =
    WEB_RUNTIME_EXTENSIONS.has(ext) ||
    WEB_RUNTIME_MIME_HINTS.some((hint) => mime.includes(hint)) ||
    kind !== 'unknown';
  if (!hasRuntimeHint) return false;

  const hasGameplayHint = GAMEPLAY_HINT.test(source);
  if (kind === 'script') {
    return hasGameplayHint && BROWSER_SCRIPT_HINT.test(source);
  }

  return hasGameplayHint && (HTML_MARKUP_HINT.test(source) || /<script\b/i.test(source));
};

export const buildMiniGameSrcDoc = (options: MiniGameDocumentOptions): string => {
  const source = (options.sourceCode || '').trim();
  const runtimeBridge = buildRuntimeBridge(options.frameId);
  const runtimeStyle = buildRuntimeStyle();
  const kind = detectDocumentKind(options);

  if (kind === 'script') {
    const scriptType = isLikelyModuleScript(source) ? 'module' : 'text/javascript';
    return `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
    ${runtimeStyle}
  </head>
  <body>
    <div id="mini-game-root"></div>
    <script type="${scriptType}">
${escapeForInlineScript(source)}
    </script>
    ${runtimeBridge}
  </body>
</html>`;
  }

  let htmlDoc = ensureHtmlDocument(source);
  htmlDoc = injectIntoHead(htmlDoc, runtimeStyle);
  htmlDoc = injectIntoBody(htmlDoc, runtimeBridge);
  return htmlDoc;
};
