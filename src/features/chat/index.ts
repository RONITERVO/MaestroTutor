// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Chat Feature - Public API
 * 
 * This is the single entry point for chat functionality.
 * External code should only import from this file.
 * 
 * Owned Store Slice: chatSlice
 */

// Components
export { default as ChatInterface } from './components/ChatInterface';
export { default as ChatMessageBubble } from './components/ChatMessageBubble';
export { default as InputArea } from './components/InputArea';
export { default as SuggestionsList } from './components/SuggestionsList';
export { default as BookmarkActions } from './components/BookmarkActions';
export { default as TextScrollwheel } from './components/TextScrollwheel';

// Shared book/phone attachment renderers.
export { default as MiniGameViewer } from './components/MiniGameViewer';
export { default as PdfViewer } from './components/PdfViewer';
export { default as TextFileViewer } from './components/TextFileViewer';
export { default as MiniGameErrorBoundary } from './components/MiniGameErrorBoundary';
export { isRunnableMiniGameAttachment } from './utils/miniGameAttachment';

// Services
export { 
  getChatHistoryDB,
  saveChatHistoryDB,
  safeSaveChatHistoryDB,
  getChatMetaDB,
  setChatMetaDB,
  getAllChatHistoriesDB,
  getAllChatMetasDB,
  hasAnyChatHistoriesDB,
  iterateChatHistoriesDB,
  clearAndSaveAllHistoriesDB,
  deriveHistoryForApi,
} from './services/chatHistory';

// Utils
export {
  computeTtsCacheKey,
  getCachedAudioForKey,
  upsertTtsCacheEntries,
  INLINE_CAP_AUDIO,
} from './utils/persistence';

// Hooks
export { useAgentTaskSpeech } from './hooks/useAgentTaskSpeech';
export { useTutorConversation } from './hooks/useTutorConversation';
export { useSuggestions } from './hooks/useSuggestions';
export { useChatPersistence } from './hooks/useChatPersistence';

export { prepareLiveRoomAgentContext, captureLiveRoomAgentHandoff } from './services/roomAgentTasks';
