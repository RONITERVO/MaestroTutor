// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Browser SVG animation repair is optional; headless keeps its existing raw SVG behavior. */
export interface AssistantArtifactOptions {
  sanitizeSvg?: (svgText: string) => string;
}

export const sanitizeSvgArtifact = (svgText: string, options?: AssistantArtifactOptions): string =>
  options?.sanitizeSvg ? options.sanitizeSvg(svgText) : svgText;
