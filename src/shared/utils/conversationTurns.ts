// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface RoleTaggedItem {
  role: string;
}

export interface RoleGroupedItems<T extends RoleTaggedItem> {
  role: T['role'];
  items: T[];
}

// The app's persisted/UI message list can legitimately contain consecutive
// messages from the same side due to deletes, imports, tools, artifacts, or
// multi-step assistant updates. Gemini, however, is more reliable when the
// outbound payload alternates by side. We therefore group only at request-build
// time so the stored history stays lossless while the API payload becomes stable.
export const groupAdjacentRoleItems = <T extends RoleTaggedItem>(
  items: readonly T[]
): RoleGroupedItems<T>[] => {
  const groups: RoleGroupedItems<T>[] = [];

  items.forEach((item) => {
    const previous = groups[groups.length - 1];
    if (previous && previous.role === item.role) {
      previous.items.push(item);
      return;
    }
    groups.push({
      role: item.role,
      items: [item],
    });
  });

  return groups;
};

type GeminiContentTurn = {
  role: string;
  parts?: unknown[];
};

// Collapse adjacent Gemini turns by concatenating all parts into a single turn.
// This preserves everything we intended to send, including invisible context
// parts such as files or compact assistant metadata, while avoiding multiple
// same-role turns in one payload.
export const collapseGeminiContents = <T extends GeminiContentTurn>(
  contents: readonly T[]
): T[] => (
  groupAdjacentRoleItems(
    contents.filter((content) => Array.isArray(content.parts) && content.parts.length > 0)
  ).map((group) => {
    const [first] = group.items;
    const mergedParts = group.items.flatMap((content) => (
      Array.isArray(content.parts) ? content.parts : []
    ));
    return {
      ...first,
      parts: mergedParts,
    };
  })
);
