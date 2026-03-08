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
<script data-maestro-runtime="bridge">
(function () {
  var FRAME_ID = ${escapedFrameId};
  var EVENT_TYPE = 'maestro-mini-game-status';
  var isFitScheduled = false;
  var fitMutationObserver = null;
  var fitResizeObserver = null;
  var GAME_ROOT_SELECTOR = '[data-mini-game-root], #mini-game-root, .mini-game-root';
  var FIT_TRANSFORM_VAR = '--maestro-fit-transform';
  var FIT_TRANSFORM_REF = 'var(' + FIT_TRANSFORM_VAR + ')';

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

  var shouldIgnoreNode = function (node) {
    if (!node || node.nodeType !== 1) return true;
    var tagName = String(node.tagName || '').toUpperCase();
    return (
      tagName === 'SCRIPT' ||
      tagName === 'STYLE' ||
      tagName === 'LINK' ||
      tagName === 'META' ||
      tagName === 'NOSCRIPT' ||
      tagName === 'TITLE'
    );
  };

  var findDeclaredGameRoot = function () {
    var root = document.querySelector(GAME_ROOT_SELECTOR);
    if (!root || root.nodeType !== 1) return null;
    return root;
  };

  var findObservedGameRoot = function () {
    var declaredRoot = findDeclaredGameRoot();
    if (declaredRoot) return declaredRoot;

    var body = document.body;
    if (!body) return null;

    var children = body.children;
    for (var i = 0; i < children.length; i += 1) {
      var node = children[i];
      if (shouldIgnoreNode(node)) continue;
      return node;
    }

    return null;
  };

  var ensureBodyFitTransformRef = function (body) {
    if (!body) return;
    var current = String(body.style.transform || '').trim();

    if (!current || current === 'none') {
      body.style.transform = FIT_TRANSFORM_REF;
      return;
    }

    if (current.indexOf(FIT_TRANSFORM_REF) >= 0) return;
    body.style.transform = current + ' ' + FIT_TRANSFORM_REF;
  };

  var getContentBounds = function () {
    var fallbackWidth = Math.max(window.innerWidth || 0, 1);
    var fallbackHeight = Math.max(window.innerHeight || 0, 1);
    var fallback = {
      minLeft: 0,
      minTop: 0,
      width: fallbackWidth,
      height: fallbackHeight,
    };
    var body = document.body;
    if (!body) return fallback;

    var declaredRoot = findDeclaredGameRoot();
    if (declaredRoot) {
      var rootRect = declaredRoot.getBoundingClientRect();
      if (
        rootRect &&
        isFinite(rootRect.left) &&
        isFinite(rootRect.top) &&
        isFinite(rootRect.right) &&
        isFinite(rootRect.bottom)
      ) {
        return {
          minLeft: rootRect.left,
          minTop: rootRect.top,
          width: Math.max(rootRect.right - rootRect.left, 1),
          height: Math.max(rootRect.bottom - rootRect.top, 1),
        };
      }
    }

    var nodes = body.querySelectorAll('*');
    var hasBounds = false;
    var minLeft = 0;
    var minTop = 0;
    var maxRight = 0;
    var maxBottom = 0;

    for (var i = 0; i < nodes.length; i += 1) {
      var node = nodes[i];
      if (shouldIgnoreNode(node)) continue;
      var rect = node.getBoundingClientRect();
      if (!rect || (!isFinite(rect.width) && !isFinite(rect.height))) continue;

      if (!hasBounds) {
        minLeft = rect.left;
        minTop = rect.top;
        maxRight = rect.right;
        maxBottom = rect.bottom;
        hasBounds = true;
        continue;
      }

      if (rect.left < minLeft) minLeft = rect.left;
      if (rect.top < minTop) minTop = rect.top;
      if (rect.right > maxRight) maxRight = rect.right;
      if (rect.bottom > maxBottom) maxBottom = rect.bottom;
    }

    if (!hasBounds) return fallback;

    return {
      minLeft: minLeft,
      minTop: minTop,
      width: Math.max(maxRight - minLeft, 1),
      height: Math.max(maxBottom - minTop, 1),
    };
  };

  var fitToViewport = function () {
    var body = document.body;
    var docEl = document.documentElement;
    if (!body || !docEl) return;

    ensureBodyFitTransformRef(body);
    body.style.setProperty(FIT_TRANSFORM_VAR, 'translate(0px, 0px) scale(1)');

    var viewportWidth = Math.max(window.innerWidth || 0, docEl.clientWidth || 0, 1);
    var viewportHeight = Math.max(window.innerHeight || 0, docEl.clientHeight || 0, 1);
    var bounds = getContentBounds();
    var contentWidth = Math.max(bounds.width, 1);
    var contentHeight = Math.max(bounds.height, 1);
    var scale = Math.min(viewportWidth / contentWidth, viewportHeight / contentHeight, 1);

    if (!isFinite(scale) || scale <= 0) {
      scale = 1;
    }

    var offsetX = ((viewportWidth - (contentWidth * scale)) / 2) - (bounds.minLeft * scale);
    var offsetY = ((viewportHeight - (contentHeight * scale)) / 2) - (bounds.minTop * scale);

    body.style.transformOrigin = '0 0';
    body.style.setProperty(
      FIT_TRANSFORM_VAR,
      'translate(' + offsetX.toFixed(2) + 'px, ' + offsetY.toFixed(2) + 'px) scale(' + scale.toFixed(6) + ')'
    );
  };

  var scheduleFitToViewport = function () {
    if (isFitScheduled) return;
    isFitScheduled = true;
    window.requestAnimationFrame(function () {
      isFitScheduled = false;
      fitToViewport();
    });
  };

  var startFitObservers = function () {
    var gameRoot = findObservedGameRoot();
    if (!gameRoot) return;

    if (typeof MutationObserver === 'function') {
      fitMutationObserver = new MutationObserver(function () {
        scheduleFitToViewport();
      });
      fitMutationObserver.observe(gameRoot, {
        childList: true,
        attributes: true,
      });
    }

    if (typeof ResizeObserver === 'function') {
      fitResizeObserver = new ResizeObserver(function () {
        scheduleFitToViewport();
      });
      fitResizeObserver.observe(gameRoot);
    }

    window.addEventListener('resize', scheduleFitToViewport);
    window.addEventListener('orientationchange', scheduleFitToViewport);
    window.addEventListener('load', scheduleFitToViewport);
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
    startFitObservers();
    scheduleFitToViewport();
    window.setTimeout(scheduleFitToViewport, 80);
    window.setTimeout(scheduleFitToViewport, 240);
    window.setTimeout(scheduleFitToViewport, 640);
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
    transform-origin: 0 0;
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
