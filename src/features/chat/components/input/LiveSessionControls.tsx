// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback } from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { LiveSessionState } from '../../../speech';
import { SmallSpinner } from '../../../../shared/ui/SmallSpinner';

interface LiveSessionControlsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  liveSessionState: LiveSessionState;
  isSuggestionMode: boolean;
  onStartLiveSession: () => Promise<void> | void;
  onStopLiveSession: () => void;
}

const LiveSessionControls: React.FC<LiveSessionControlsProps> = ({
  t,
  liveSessionState,
  isSuggestionMode,
  onStartLiveSession,
  onStopLiveSession,
}) => {
  const liveSessionActive = liveSessionState === 'active';
  const liveSessionConnecting = liveSessionState === 'connecting';
  const liveSessionErrored = liveSessionState === 'error';

  const liveSessionButtonLabel = liveSessionActive
    ? t('chat.liveSession.stop')
    : (liveSessionErrored ? t('chat.liveSession.retry') : t('chat.liveSession.start'));
  const liveSessionButtonClasses = liveSessionActive
    ? 'bg-live-session-button-active-bg hover:bg-live-session-button-active-hover-bg text-live-session-button-active-text'
    : (liveSessionErrored
      ? 'bg-live-session-button-error-bg hover:bg-live-session-button-error-hover-bg text-live-session-button-error-text'
      : (isSuggestionMode ? 'bg-primary/80 hover:bg-primary text-primary-foreground' : 'bg-primary/60 hover:bg-primary/80 text-primary-foreground'));

  const handleLiveSessionToggle = useCallback(() => {
    if (liveSessionActive) {
      onStopLiveSession();
    } else {
      Promise.resolve(onStartLiveSession()).catch((err) => {
        console.error('Failed to start live session:', err);
      });
    }
  }, [liveSessionActive, onStartLiveSession, onStopLiveSession]);

  return (
    <div className="absolute top-1 right-1 flex items-center gap-2 z-30">
      {liveSessionConnecting && <SmallSpinner className="w-5 h-5 text-primary-foreground drop-shadow" />}
      <button
        type="button"
        onClick={handleLiveSessionToggle}
        disabled={liveSessionConnecting}
        className={`px-2 py-1 text-xs font-semibold rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-white/50 ${liveSessionButtonClasses} ${liveSessionConnecting ? 'opacity-70 cursor-wait' : ''}`}
      >
        {liveSessionButtonLabel}
      </button>
    </div>
  );
};

export default LiveSessionControls;
