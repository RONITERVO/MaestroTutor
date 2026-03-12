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
  var fitScheduleTimer = null;
  var fitMutationObserver = null;
  var fitResizeObserver = null;
  var lastMetricsWidth = 0;
  var lastMetricsHeight = 0;
  var GAME_ROOT_SELECTOR = '[data-mini-game-root], #mini-game-root, .mini-game-root';
  var FIT_TRANSFORM_VAR = '--maestro-fit-transform';
  var FIT_TRANSFORM_REF = 'var(' + FIT_TRANSFORM_VAR + ')';
  var ROOT_INLINE_HEIGHT_ATTR = 'data-maestro-inline-height';
  var ROOT_INLINE_WIDTH_ATTR = 'data-maestro-inline-width';

  var sendStatus = function (status, detail) {
    try {
      parent.postMessage({ type: EVENT_TYPE, frameId: FRAME_ID, status: status, detail: detail || '' }, '*');
    } catch (_err) {}
  };

  var sendMetrics = function (width, height) {
    try {
      parent.postMessage({ type: EVENT_TYPE, frameId: FRAME_ID, status: 'metrics', width: width, height: height }, '*');
    } catch (_err) {}
  };

  var lockToClickAndTap = function () {
    window.addEventListener('keydown', function (event) {
      // Allow F11 or Esc for fullscreen handling
      if (event.key === 'F11' || event.key === 'Escape') return;
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

  var rememberInlineStyle = function (node, attrName, value) {
    if (!node || node.nodeType !== 1 || node.hasAttribute(attrName)) return;
    node.setAttribute(attrName, value || '');
  };

  var restoreInlineStyle = function (node, attrName, propName) {
    if (!node || node.nodeType !== 1 || !node.hasAttribute(attrName)) return;
    var previous = node.getAttribute(attrName);
    node.style[propName] = previous || '';
    node.removeAttribute(attrName);
  };

  var rootContainsRuntimeSurface = function (root) {
    return !!(root && root.querySelector && root.querySelector('canvas, video, iframe'));
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

  var normalizeObservedGameRoot = function () {
    var root = findObservedGameRoot();
    if (!root || root.nodeType !== 1 || !window.getComputedStyle) return root;

    var storedInlineWidth = root.hasAttribute(ROOT_INLINE_WIDTH_ATTR)
      ? root.getAttribute(ROOT_INLINE_WIDTH_ATTR)
      : null;
    var storedInlineHeight = root.hasAttribute(ROOT_INLINE_HEIGHT_ATTR)
      ? root.getAttribute(ROOT_INLINE_HEIGHT_ATTR)
      : null;

    if (storedInlineWidth !== null) {
      root.style.width = storedInlineWidth || '';
    }
    if (storedInlineHeight !== null) {
      root.style.height = storedInlineHeight || '';
    }

    var computed = window.getComputedStyle(root);
    var overflowX = String(computed.overflowX || computed.overflow || '').toLowerCase();
    var overflowY = String(computed.overflowY || computed.overflow || '').toLowerCase();
    var clientWidth = Math.max(root.clientWidth || 0, 0);
    var clientHeight = Math.max(root.clientHeight || 0, 0);
    var scrollWidth = Math.max(root.scrollWidth || 0, 0);
    var scrollHeight = Math.max(root.scrollHeight || 0, 0);
    var widthOverflow = scrollWidth - clientWidth;
    var heightOverflow = scrollHeight - clientHeight;
    var hasRuntimeSurface = rootContainsRuntimeSurface(root);
    var shouldExpandWidth = (
      !hasRuntimeSurface &&
      clientWidth > 120 &&
      widthOverflow > 18 &&
      widthOverflow < Math.max(96, clientWidth * 0.22) &&
      (overflowX === 'hidden' || overflowX === 'clip')
    );
    var shouldExpandHeight = (
      !hasRuntimeSurface &&
      clientHeight > 120 &&
      heightOverflow > 18 &&
      heightOverflow < Math.max(160, clientHeight * 0.35) &&
      (overflowY === 'hidden' || overflowY === 'clip')
    );

    if (shouldExpandWidth) {
      rememberInlineStyle(root, ROOT_INLINE_WIDTH_ATTR, root.style.width || '');
      var expandedWidth = scrollWidth + 'px';
      if (root.style.width !== expandedWidth) {
        root.style.width = expandedWidth;
      }
    } else {
      restoreInlineStyle(root, ROOT_INLINE_WIDTH_ATTR, 'width');
    }

    if (shouldExpandHeight) {
      rememberInlineStyle(root, ROOT_INLINE_HEIGHT_ATTR, root.style.height || '');
      var expandedHeight = scrollHeight + 'px';
      if (root.style.height !== expandedHeight) {
        root.style.height = expandedHeight;
      }
    } else {
      restoreInlineStyle(root, ROOT_INLINE_HEIGHT_ATTR, 'height');
    }

    return root;
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

    normalizeObservedGameRoot();

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

    var children = body.children;
    var firstVisible = null;
    var multipleVisible = false;
    for (var ci = 0; ci < children.length; ci += 1) {
      if (shouldIgnoreNode(children[ci])) continue;
      if (!firstVisible) { firstVisible = children[ci]; }
      else { multipleVisible = true; break; }
    }

    if (firstVisible && !multipleVisible) {
      var implicitRect = firstVisible.getBoundingClientRect();
      if (
        implicitRect &&
        isFinite(implicitRect.left) &&
        isFinite(implicitRect.top) &&
        isFinite(implicitRect.right) &&
        isFinite(implicitRect.bottom)
      ) {
        return {
          minLeft: implicitRect.left,
          minTop: implicitRect.top,
          width: Math.max(implicitRect.right - implicitRect.left, 1),
          height: Math.max(implicitRect.bottom - implicitRect.top, 1),
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

    normalizeObservedGameRoot();
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

    var leftoverHeight = Math.max(viewportHeight - (contentHeight * scale), 0);
    var topPadding = Math.min(Math.max(leftoverHeight * 0.12, 0), 18);
    var offsetX = ((viewportWidth - (contentWidth * scale)) / 2) - (bounds.minLeft * scale);
    var offsetY = topPadding - (bounds.minTop * scale);

    body.style.transformOrigin = '0 0';
    body.style.setProperty(
      FIT_TRANSFORM_VAR,
      'translate(' + offsetX.toFixed(2) + 'px, ' + offsetY.toFixed(2) + 'px) scale(' + scale.toFixed(6) + ')'
    );

    var roundedWidth = Math.max(Math.round(contentWidth), 1);
    var roundedHeight = Math.max(Math.round(contentHeight), 1);
    if (roundedWidth !== lastMetricsWidth || roundedHeight !== lastMetricsHeight) {
      lastMetricsWidth = roundedWidth;
      lastMetricsHeight = roundedHeight;
      sendMetrics(roundedWidth, roundedHeight);
    }
  };

  var scheduleFitToViewport = function () {
    if (fitScheduleTimer !== null) return;
    fitScheduleTimer = window.setTimeout(function () {
      fitScheduleTimer = null;
      fitToViewport();
    }, 200);
  };

  var startFitObservers = function () {
    normalizeObservedGameRoot();
    var gameRoot = findObservedGameRoot();
    if (!gameRoot) return;

    if (typeof MutationObserver === 'function') {
      fitMutationObserver = new MutationObserver(function () {
        normalizeObservedGameRoot();
        scheduleFitToViewport();
      });
      fitMutationObserver.observe(gameRoot, {
        attributes: true,
        characterData: true,
        childList: true,
        subtree: true,
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

// RELAXED CSP: Added worker-src, child-src, and 'unsafe-eval' for modern HTML5 Game Engines
const buildRuntimeStyle = (): string => `
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://unpkg.com https://cdnjs.cloudflare.com https://esm.sh; style-src 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com data:; img-src data: blob: https://www.transparenttextures.com https://upload.wikimedia.org https://images.unsplash.com https://picsum.photos https://fastly.picsum.photos https://api.dicebear.com; media-src data: blob: https://commons.wikimedia.org https://cdn.freesound.org; connect-src https://opentdb.com https://api.datamuse.com blob: data:; worker-src blob: data: 'self'; child-src blob: data: 'self';">
<style>
  :root { color-scheme: light dark; }
  html, body {
    margin: 0;
    padding: 0;
    width: 100%;
    height: 100%;
    overflow: hidden; /* Prevents double scrollbars */
    transform-origin: 0 0;
    touch-action: none; /* Crucial: Prevents mobile browser pull-to-refresh & swipe navigation */
    -webkit-user-select: none;
    user-select: none;
    -webkit-touch-callout: none;
    background: #000; /* Industry standard for game containers to hide letterboxing */
    color: #e2e8f0;
    font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, sans-serif;
  }
  * { box-sizing: border-box; }
  
  canvas {
    display: block; /* Removes baseline ghost padding underneath the canvas */
    outline: none;
    -webkit-tap-highlight-color: transparent;
    touch-action: none; /* Explicitly disable touch scrolling on the canvas itself */
  }
  
  button, [role="button"], a, input, textarea {
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
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, maximum-scale=1.0, user-scalable=no" />
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

  // Allow any HTML document with markup to render (not just interactive games).
  if (kind === 'html' && HTML_MARKUP_HINT.test(source)) return true;

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
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, maximum-scale=1.0, user-scalable=no" />
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