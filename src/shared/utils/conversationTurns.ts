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
