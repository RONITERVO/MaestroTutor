// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createContext, useContext } from 'react';
import type { RefObject } from 'react';
import type { BookLayout } from './bookModel';

export interface BookPresentation {
  layout: BookLayout;
  spreadRoot: RefObject<HTMLDivElement | null>;
  earlierPageTarget: HTMLDivElement | null;
  earlierMessageIds: ReadonlySet<string>;
  visibleMessageIds: ReadonlySet<string>;
  historyPageKey: string;
  isLatestPage: boolean;
  selectedId: string | null;
  selectArtifact: (messageId: string) => void;
  posters: ReadonlyMap<string, string>;
}

export const BookPresentationContext = createContext<BookPresentation | null>(null);
export const useBookPresentation = () => useContext(BookPresentationContext);
