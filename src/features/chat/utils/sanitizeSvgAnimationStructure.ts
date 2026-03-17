// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';
const INLINE_ANIMATION_STYLE_REGEX = /\banimation(?:-[a-z-]+)?\s*:/i;
const CSS_ANIMATION_DECLARATION_REGEX = /\banimation(?:-[a-z-]+)?\s*:/i;
const NESTED_CSS_AT_RULE_REGEX = /^@(media|supports|layer|container|scope|document)\b/i;

const findMatchingCssBrace = (source: string, openBraceIndex: number): number => {
  let depth = 0;
  for (let i = openBraceIndex; i < source.length; i++) {
    const char = source[i];
    if (char === '{') {
      depth++;
      continue;
    }
    if (char !== '}') continue;
    depth--;
    if (depth === 0) return i;
  }
  return -1;
};

const extractAnimatedSelectorsFromCss = (cssText: string): string[] => {
  const selectors: string[] = [];
  const source = cssText.replace(/\/\*[\s\S]*?\*\//g, '');
  let cursor = 0;

  while (cursor < source.length) {
    const openBraceIndex = source.indexOf('{', cursor);
    if (openBraceIndex === -1) break;

    const selectorText = source.slice(cursor, openBraceIndex).trim();
    const closeBraceIndex = findMatchingCssBrace(source, openBraceIndex);
    if (closeBraceIndex === -1) break;

    const declarationText = source.slice(openBraceIndex + 1, closeBraceIndex);
    if (selectorText) {
      if (/^@(?:-webkit-)?keyframes\b/i.test(selectorText)) {
        cursor = closeBraceIndex + 1;
        continue;
      }

      if (NESTED_CSS_AT_RULE_REGEX.test(selectorText)) {
        selectors.push(...extractAnimatedSelectorsFromCss(declarationText));
        cursor = closeBraceIndex + 1;
        continue;
      }

      if (!selectorText.startsWith('@') && CSS_ANIMATION_DECLARATION_REGEX.test(declarationText)) {
        selectors.push(
          ...selectorText
            .split(',')
            .map(part => part.trim())
            .filter(Boolean),
        );
      }
    }

    cursor = closeBraceIndex + 1;
  }

  return selectors;
};

const isAnimationChildElement = (element: Element): boolean => {
  const tagName = element.tagName.toLowerCase();
  if (tagName === 'animatetransform' || tagName === 'animatemotion') {
    return true;
  }

  if (tagName !== 'animate' && tagName !== 'set') {
    return false;
  }

  const attributeName = (element.getAttribute('attributeName') || '').trim().toLowerCase();
  return attributeName === 'transform';
};

const elementMatchesAnimatedSelector = (element: Element, selectors: string[]): boolean => {
  for (const selector of selectors) {
    try {
      if (element.matches(selector)) return true;
    } catch {
      // Ignore invalid selectors emitted by the model.
    }
  }
  return false;
};

const shouldWrapAnimatedTransformElement = (element: Element, animatedSelectors: string[]): boolean => {
  const transformValue = element.getAttribute('transform');
  if (!transformValue) return false;

  if (INLINE_ANIMATION_STYLE_REGEX.test(element.getAttribute('style') || '')) {
    return true;
  }

  if (Array.from(element.children).some(isAnimationChildElement)) {
    return true;
  }

  return elementMatchesAnimatedSelector(element, animatedSelectors);
};

export const sanitizeSvgAnimationStructure = (svgText: string): string => {
  if (typeof svgText !== 'string' || !svgText.trim()) return svgText;
  if (typeof DOMParser === 'undefined' || typeof XMLSerializer === 'undefined') return svgText;

  try {
    const parser = new DOMParser();
    const documentNode = parser.parseFromString(svgText, 'image/svg+xml');
    if (documentNode.getElementsByTagName('parsererror').length > 0) {
      return svgText;
    }

    const root = documentNode.documentElement;
    if (!root || root.tagName.toLowerCase() !== 'svg') {
      return svgText;
    }

    const animatedSelectors = Array.from(documentNode.getElementsByTagName('style'))
      .map(styleElement => styleElement.textContent || '')
      .flatMap(extractAnimatedSelectorsFromCss);

    const allElements = documentNode.getElementsByTagName('*');
    for (let i = allElements.length - 1; i >= 0; i--) {
      const element = allElements[i];
      if (element.tagName.toLowerCase() === 'svg') continue;
      if (!shouldWrapAnimatedTransformElement(element, animatedSelectors)) continue;

      const transformValue = element.getAttribute('transform');
      if (!transformValue) continue;

      const wrapper = documentNode.createElementNS(SVG_NAMESPACE, 'g');
      wrapper.setAttribute('transform', transformValue);
      element.removeAttribute('transform');

      const parentNode = element.parentNode;
      if (!parentNode) continue;
      parentNode.insertBefore(wrapper, element);
      wrapper.appendChild(element);
    }

    return new XMLSerializer().serializeToString(documentNode);
  } catch {
    return svgText;
  }
};

export default sanitizeSvgAnimationStructure;
