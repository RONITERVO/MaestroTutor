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
const WEB_RUNTIME_MIME_HINTS = ['text/html', 'application/xhtml+xml', 'text/javascript', 'application/javascript'];

const HTML_MARKUP_HINT = /<(!doctype\s+html|html|body|script|canvas|button|style|main|section|div)\b/i;
const GAMEPLAY_HINT = /data-maestro-mini-game|@maestro-mini-game|<canvas\b|<button\b|onclick\s*=|onpointerdown\s*=|addEventListener\(\s*["'](?:click|pointerdown|touchstart)|requestAnimationFrame|flashcard|vocab(?:ulary)?\s*quiz|word\s*match/i;
const BROWSER_SCRIPT_HINT = /\b(document|window)\.|getContext\(|querySelector\(|createElement\(|addEventListener\(/i;

const escapeForInlineScript = (value: string): string => value.replace(/<\/script/gi, '<\\/script');

const getFileExtension = (fileName?: string | null): string => {
  const normalized = (fileName || '').trim().toLowerCase();
  const dot = normalized.lastIndexOf('.');
  if (dot < 0 || dot >= normalized.length - 1) return '';
  return normalized.slice(dot + 1);
};

const isLikelyModuleScript = (code: string): boolean => /^\s*import\s+/m.test(code) || /^\s*export\s+/m.test(code);

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
  return `
<script data-maestro-runtime="bridge">
(function () {
  var FRAME_ID = ${JSON.stringify(frameId)};
  var EVENT_TYPE = 'maestro-mini-game-status';
  var backgroundSyncTimer = 0;
  var metricsSyncTimer = 0;
  var metricsAnimationFrame = 0;
  var lastReportedHeight = 0;
  var lastReportedWidth = 0;
  var lastReportedAspectRatio = 0;

  var sendStatus = function (status, detail, metrics) {
    try { parent.postMessage({ type: EVENT_TYPE, frameId: FRAME_ID, status: status, detail: detail || '', metrics: metrics || null }, '*'); } catch (e) {}
  };

  var clearShellBackground = function (element) {
    if (!element || !element.style) return;
    element.style.setProperty('background', 'transparent', 'important');
    element.style.setProperty('background-color', 'transparent', 'important');
    element.style.setProperty('background-image', 'none', 'important');
  };

  var parseAlpha = function (color) {
    if (!color || color === 'transparent') return 0;
    var rgbaMatch = color.match(/rgba\\(([^)]+)\\)/i);
    if (rgbaMatch) {
      var rgbaParts = rgbaMatch[1].split(',');
      if (rgbaParts.length >= 4) {
        var alpha = parseFloat(rgbaParts[3].trim());
        return Number.isFinite(alpha) ? alpha : 1;
      }
      return 1;
    }
    if (/^rgb\\(/i.test(color) || /^hsl\\(/i.test(color) || /^hsla\\(/i.test(color) || color[0] === '#') return 1;
    return 0;
  };

  var hasVisibleBackground = function (element) {
    if (!element || !window.getComputedStyle) return false;
    var style = window.getComputedStyle(element);
    if (!style) return false;
    if (style.backgroundImage && style.backgroundImage !== 'none') return true;
    return parseAlpha(style.backgroundColor) > 0.04;
  };

  var rectArea = function (rect) {
    if (!rect) return 0;
    return Math.max(0, rect.width) * Math.max(0, rect.height);
  };

  var fillsViewport = function (rect) {
    var viewportWidth = Math.max(document.documentElement ? document.documentElement.clientWidth : 0, window.innerWidth || 0);
    var viewportHeight = Math.max(document.documentElement ? document.documentElement.clientHeight : 0, window.innerHeight || 0);
    if (!viewportWidth || !viewportHeight) return false;
    return rect.width >= viewportWidth * 0.92 && rect.height >= viewportHeight * 0.92;
  };

  var getElementChildren = function (element) {
    var children = [];
    if (!element || !element.children) return children;
    for (var i = 0; i < element.children.length; i += 1) {
      var child = element.children[i];
      if (child && child.nodeType === 1) children.push(child);
    }
    return children;
  };

  var roundMetric = function (value) {
    if (!Number.isFinite(value)) return 0;
    return Math.round(value * 100) / 100;
  };

  var parseAspectRatio = function (value) {
    if (!value) return 0;
    var normalized = String(value).trim().toLowerCase();
    if (!normalized || normalized === 'auto') return 0;

    var slashMatch = normalized.match(/^([0-9]*\\.?[0-9]+)\\s*\\/\\s*([0-9]*\\.?[0-9]+)$/);
    if (slashMatch) {
      var width = parseFloat(slashMatch[1]);
      var height = parseFloat(slashMatch[2]);
      if (Number.isFinite(width) && Number.isFinite(height) && width > 0 && height > 0) {
        return width / height;
      }
      return 0;
    }

    var numeric = parseFloat(normalized);
    return Number.isFinite(numeric) && numeric > 0 ? numeric : 0;
  };

  var getIntrinsicAspectRatio = function (element) {
    if (!element) return 0;
    var tagName = (element.tagName || '').toLowerCase();
    if (tagName === 'canvas') {
      var canvasWidth = Number(element.width) || 0;
      var canvasHeight = Number(element.height) || 0;
      if (canvasWidth > 0 && canvasHeight > 0) return canvasWidth / canvasHeight;
    }

    if (tagName === 'svg') {
      var viewBox = element.getAttribute('viewBox') || '';
      var viewBoxParts = viewBox.trim().split(/\\s+/);
      if (viewBoxParts.length === 4) {
        var vbWidth = parseFloat(viewBoxParts[2]);
        var vbHeight = parseFloat(viewBoxParts[3]);
        if (Number.isFinite(vbWidth) && Number.isFinite(vbHeight) && vbWidth > 0 && vbHeight > 0) {
          return vbWidth / vbHeight;
        }
      }

      var svgWidth = parseFloat(element.getAttribute('width') || '');
      var svgHeight = parseFloat(element.getAttribute('height') || '');
      if (Number.isFinite(svgWidth) && Number.isFinite(svgHeight) && svgWidth > 0 && svgHeight > 0) {
        return svgWidth / svgHeight;
      }
    }

    return 0;
  };

  var getRenderableBodyChildren = function () {
    var nodes = [];
    if (!document.body || !window.getComputedStyle) return nodes;

    var children = getElementChildren(document.body);
    for (var i = 0; i < children.length; i += 1) {
      var child = children[i];
      var tagName = (child.tagName || '').toLowerCase();
      if (tagName === 'script' || tagName === 'style' || tagName === 'link' || tagName === 'meta' || tagName === 'template') {
        continue;
      }

      var style = window.getComputedStyle(child);
      if (!style || style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity || '1') <= 0.01) {
        continue;
      }

      var rect = child.getBoundingClientRect();
      if (rect.width < 2 && rect.height < 2) continue;
      nodes.push({ element: child, rect: rect });
    }

    return nodes;
  };

  var measureContentMetrics = function () {
    if (!document.body) return null;

    var viewportWidth = Math.max(document.documentElement ? document.documentElement.clientWidth : 0, window.innerWidth || 0);
    if (!viewportWidth) return null;

    var renderableChildren = getRenderableBodyChildren();
    var primary = null;
    var minLeft = 0;
    var minTop = 0;
    var maxRight = 0;
    var maxBottom = 0;

    if (renderableChildren.length > 0) {
      var largestArea = 0;
      for (var i = 0; i < renderableChildren.length; i += 1) {
        var item = renderableChildren[i];
        var rect = item.rect;
        var area = rectArea(rect);

        if (!primary || area >= largestArea) {
          primary = item.element;
          largestArea = area;
        }

        if (i === 0) {
          minLeft = rect.left;
          minTop = rect.top;
          maxRight = rect.right;
          maxBottom = rect.bottom;
        } else {
          minLeft = Math.min(minLeft, rect.left);
          minTop = Math.min(minTop, rect.top);
          maxRight = Math.max(maxRight, rect.right);
          maxBottom = Math.max(maxBottom, rect.bottom);
        }
      }
    } else {
      primary = document.body;
      var bodyRect = document.body.getBoundingClientRect();
      minLeft = bodyRect.left;
      minTop = bodyRect.top;
      maxRight = bodyRect.right;
      maxBottom = bodyRect.bottom;
    }

    var measuredWidth = Math.max(0, maxRight - minLeft);
    var measuredHeight = Math.max(0, maxBottom - minTop);
    var preferredWidth = measuredWidth > 0 ? measuredWidth : viewportWidth;
    var aspectRatio = 0;

    if (primary && window.getComputedStyle) {
      var primaryRect = primary.getBoundingClientRect();
      if (primaryRect.width > 0) preferredWidth = primaryRect.width;

      var primaryStyle = window.getComputedStyle(primary);
      if (primaryStyle) {
        aspectRatio = parseAspectRatio(primaryStyle.aspectRatio);
      }

      if (!aspectRatio) aspectRatio = getIntrinsicAspectRatio(primary);
      if (!aspectRatio && primaryRect.width > 0 && primaryRect.height > 0) {
        aspectRatio = primaryRect.width / primaryRect.height;
      }
    }

    var preferredHeight = measuredHeight;
    if (aspectRatio > 0 && preferredWidth > 0) {
      preferredHeight = preferredWidth / aspectRatio;
    }

    preferredWidth = roundMetric(preferredWidth);
    preferredHeight = roundMetric(preferredHeight);
    aspectRatio = aspectRatio > 0 ? roundMetric(aspectRatio) : 0;

    if (preferredWidth < 16 || preferredHeight < 16) return null;
    return {
      width: preferredWidth,
      height: preferredHeight,
      aspectRatio: aspectRatio,
    };
  };

  var syncContentMetrics = function (force) {
    var metrics = measureContentMetrics();
    if (!metrics) return;

    var sameHeight = Math.abs(metrics.height - lastReportedHeight) < 2;
    var sameWidth = Math.abs(metrics.width - lastReportedWidth) < 2;
    var sameAspectRatio = Math.abs(metrics.aspectRatio - lastReportedAspectRatio) < 0.02;
    if (!force && sameHeight && sameWidth && sameAspectRatio) return;

    lastReportedHeight = metrics.height;
    lastReportedWidth = metrics.width;
    lastReportedAspectRatio = metrics.aspectRatio;
    sendStatus('metrics', '', metrics);
  };

  var scheduleMetricsSync = function (delay, force) {
    if (metricsSyncTimer) {
      window.clearTimeout(metricsSyncTimer);
    }

    metricsSyncTimer = window.setTimeout(function () {
      metricsSyncTimer = 0;
      if (metricsAnimationFrame) {
        window.cancelAnimationFrame(metricsAnimationFrame);
      }
      metricsAnimationFrame = window.requestAnimationFrame(function () {
        metricsAnimationFrame = 0;
        syncContentMetrics(!!force);
      });
    }, delay || 0);
  };

  var isCenteringShell = function (element, rect) {
    if (!element || !window.getComputedStyle) return false;
    var style = window.getComputedStyle(element);
    var display = (style.display || '').toLowerCase();
    var justifyContent = (style.justifyContent || '').toLowerCase();
    var alignItems = (style.alignItems || '').toLowerCase();
    var placeContent = (style.placeContent || '').toLowerCase();
    var placeItems = (style.placeItems || '').toLowerCase();
    var centersChildren =
      (((display === 'flex' || display === 'inline-flex') && (justifyContent.indexOf('center') >= 0 || alignItems.indexOf('center') >= 0))) ||
      (((display === 'grid' || display === 'inline-grid') && (placeContent.indexOf('center') >= 0 || placeItems.indexOf('center') >= 0 || alignItems.indexOf('center') >= 0 || justifyContent.indexOf('center') >= 0)));
    if (centersChildren) return true;

    var children = getElementChildren(element);
    if (children.length !== 1) return false;
    var childRect = children[0].getBoundingClientRect();
    if (!rectArea(rect) || !rectArea(childRect)) return false;
    return childRect.width <= rect.width * 0.94 || childRect.height <= rect.height * 0.94;
  };

  var syncShellBackgrounds = function () {
    try {
      clearShellBackground(document.documentElement);
      if (!document.body) return;
      clearShellBackground(document.body);

      var bodyChildren = getElementChildren(document.body);
      for (var i = 0; i < bodyChildren.length; i += 1) {
        var child = bodyChildren[i];
        if (!child || !hasVisibleBackground(child)) continue;
        var rect = child.getBoundingClientRect();
        if (!fillsViewport(rect)) continue;
        if (!isCenteringShell(child, rect)) continue;
        clearShellBackground(child);
      }
    } catch (e) {}
  };

  var scheduleShellSync = function () {
    if (backgroundSyncTimer) {
      window.clearTimeout(backgroundSyncTimer);
    }
    backgroundSyncTimer = window.setTimeout(function () {
      backgroundSyncTimer = 0;
      syncShellBackgrounds();
    }, 40);
  };

  window.addEventListener('error', function (e) { sendStatus('error', e.message); });
  window.addEventListener('unhandledrejection', function (e) { sendStatus('error', String(e.reason)); });
  window.addEventListener('DOMContentLoaded', function () {
    syncShellBackgrounds();
    syncContentMetrics(true);
    window.requestAnimationFrame(syncShellBackgrounds);
    window.requestAnimationFrame(function () { syncContentMetrics(false); });
    window.setTimeout(syncShellBackgrounds, 140);
    window.setTimeout(function () { syncContentMetrics(false); }, 140);
    sendStatus('ready', 'ok');
  });
  window.addEventListener('load', function () {
    syncShellBackgrounds();
    window.setTimeout(syncShellBackgrounds, 220);
    syncContentMetrics(false);
    window.setTimeout(function () { syncContentMetrics(false); }, 220);
  });
  window.addEventListener('resize', function () {
    scheduleShellSync();
    scheduleMetricsSync(40, false);
  });

  if (document.fonts && document.fonts.ready && typeof document.fonts.ready.then === 'function') {
    document.fonts.ready.then(function () {
      scheduleMetricsSync(0, false);
    }).catch(function () {});
  }

  if (typeof MutationObserver !== 'undefined' && document.documentElement) {
    try {
      var observer = new MutationObserver(function () {
        scheduleShellSync();
        scheduleMetricsSync(40, false);
      });
      observer.observe(document.documentElement, {
        subtree: true,
        childList: true,
        characterData: true,
        attributes: true,
        attributeFilter: ['style', 'class', 'width', 'height', 'hidden']
      });
    } catch (e) {}
  }
})();
</script>`;
};

const buildRuntimeStyle = (): string => `
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://unpkg.com https://cdnjs.cloudflare.com https://esm.sh; style-src 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com data:; img-src data: blob: https://www.transparenttextures.com https://upload.wikimedia.org https://images.unsplash.com https://picsum.photos https://fastly.picsum.photos https://api.dicebear.com; media-src data: blob: https://commons.wikimedia.org https://cdn.freesound.org; connect-src https://opentdb.com https://api.datamuse.com blob: data:; worker-src blob: data: 'self'; child-src blob: data: 'self';">
<style>
  :root { color-scheme: light dark; background: transparent !important; }
  /* Enforce 100% width/height so LLM responsive games fit the iframe perfectly */
  html, body {
    margin: 0; padding: 0; width: 100%; height: 100%;
    overflow: hidden; touch-action: none;
    -webkit-user-select: none; user-select: none; -webkit-touch-callout: none;
    background: transparent !important; color: CanvasText; font-family: ui-sans-serif, system-ui, sans-serif;
  }
  * { box-sizing: border-box; }
  canvas { display: block; outline: none; -webkit-tap-highlight-color: transparent; touch-action: none; max-width: 100%; max-height: 100%; }
</style>`;

const ensureHtmlDocument = (sourceCode: string): string => {
  const source = (sourceCode || '').trim();
  if (!source) return '<!doctype html><html><head><meta charset="utf-8" /></head><body></body></html>';
  if (/<html[\s>]/i.test(source) || /<!doctype\s+html/i.test(source)) return source;
  return `<!doctype html>\n<html>\n  <head>\n    <meta charset="utf-8" />\n    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, maximum-scale=1.0, user-scalable=no" />\n  </head>\n  <body>${source}</body>\n</html>`;
};

const injectIntoHead = (html: string, payload: string): string => {
  if (/<\/head>/i.test(html)) return html.replace(/<\/head>/i, `${payload}</head>`);
  if (/<html[^>]*>/i.test(html)) return html.replace(/<html[^>]*>/i, (openTag) => `${openTag}<head>${payload}</head>`);
  return `<head>${payload}</head>${html}`;
};

const injectIntoBody = (html: string, payload: string): string => {
  if (/<\/body>/i.test(html)) return html.replace(/<\/body>/i, `${payload}</body>`);
  return `${html}${payload}`;
};

export const isRunnableMiniGameAttachment = (candidate: MiniGameAttachmentCandidate): boolean => {
  const source = (candidate.sourceCode || '').trim();
  if (!source) return false;
  const ext = getFileExtension(candidate.fileName);
  const mime = (candidate.mimeType || '').trim().toLowerCase();
  const kind = detectDocumentKind(candidate);
  const hasRuntimeHint = WEB_RUNTIME_EXTENSIONS.has(ext) || WEB_RUNTIME_MIME_HINTS.some((hint) => mime.includes(hint)) || kind !== 'unknown';
  if (!hasRuntimeHint) return false;
  const hasGameplayHint = GAMEPLAY_HINT.test(source);
  if (kind === 'script') return hasGameplayHint && BROWSER_SCRIPT_HINT.test(source);
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
    return `<!doctype html>\n<html>\n  <head>\n    <meta charset="utf-8" />\n    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, maximum-scale=1.0, user-scalable=no" />\n    ${runtimeStyle}\n  </head>\n  <body>\n    <div id="mini-game-root"></div>\n    <script type="${scriptType}">\n${escapeForInlineScript(source)}\n    </script>\n    ${runtimeBridge}\n  </body>\n</html>`;
  }

  let htmlDoc = ensureHtmlDocument(source);
  htmlDoc = injectIntoHead(htmlDoc, runtimeStyle);
  htmlDoc = injectIntoBody(htmlDoc, runtimeBridge);
  return htmlDoc;
};
