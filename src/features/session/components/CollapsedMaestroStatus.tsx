// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { TranslationReplacements } from '../../../core/i18n/index';
import { MaestroActivityStage } from '../../../core/types';
import { useMaestroStore } from '../../../store';
import {
  TOKEN_CATEGORY,
  TOKEN_SUBTYPE,
  buildToken,
  getTokenCategory,
  getTokenSubtype,
  LIVE_TOKEN_DISPLAY,
  type TokenDisplayConfig,
  UI_TOKEN_DISPLAY,
} from '../../../core/config/activityTokens';
import { useShallow } from 'zustand/react/shallow';
import { selectActiveUiTokens, selectIsLive } from '../../../store/slices/uiSlice';
import * as Icons from '../../../shared/ui/Icons';

interface CollapsedMaestroStatusProps {
  stage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;
  targetLanguageFlag?: string;
  targetLanguageTitle?: string;
  className?: string;
  isExpanded?: boolean;
}

const STAGE_DISPLAY: Record<MaestroActivityStage, { icon: keyof typeof Icons; textKey: string; titleKey: string; animate?: boolean }> = {
  speaking: {
    icon: 'IconSpeaker',
    textKey: 'chat.maestro.speaking',
    titleKey: 'chat.maestro.title.speaking',
    animate: true,
  },
  typing: {
    icon: 'IconKeyboard',
    textKey: 'chat.maestro.typing',
    titleKey: 'chat.maestro.title.typing',
  },
  listening: {
    icon: 'IconMicrophone',
    textKey: 'chat.maestro.listening',
    titleKey: 'chat.maestro.title.listening',
  },
  observing_low: {
    icon: 'IconSleepingZzz',
    textKey: 'chat.maestro.resting',
    titleKey: 'chat.maestro.title.resting',
  },
  observing_medium: {
    icon: 'IconEyeOpen',
    textKey: 'chat.maestro.observing',
    titleKey: 'chat.maestro.title.observing',
  },
  observing_high: {
    icon: 'IconKeyboard',
    textKey: 'chat.maestro.aboutToEngage',
    titleKey: 'chat.maestro.title.aboutToEngage',
  },
  idle: {
    icon: 'IconSparkles',
    textKey: 'chat.maestro.idle',
    titleKey: 'chat.maestro.title.idle',
  },
};

const getTokenDisplayConfig = (token: string): TokenDisplayConfig | null => {
  const category = getTokenCategory(token);
  if (category === TOKEN_CATEGORY.LIVE) return LIVE_TOKEN_DISPLAY;
  if (category === TOKEN_CATEGORY.UI) return UI_TOKEN_DISPLAY[getTokenSubtype(token)] || null;
  return null;
};

// Helper to get configuration for the parent container (The Flag)
export const getStatusConfig = (
  stage: MaestroActivityStage,
  activeUiTokens: string[] = [],
  isHolding = false,
  isLive = false
) => {
  if (isHolding) {
    const holdColor = UI_TOKEN_DISPLAY[TOKEN_SUBTYPE.HOLD]?.color;
    if (holdColor) {
      return { color: holdColor.bg, borderColor: holdColor.border, textColor: holdColor.text };
    }
    return { color: 'bg-flag-hold-bg', borderColor: 'border-flag-hold-border', textColor: 'text-flag-hold-text' };
  }

  switch (stage) {
    case 'speaking':
      return { color: 'bg-flag-speaking-bg', borderColor: 'border-flag-speaking-border', textColor: 'text-flag-speaking-text' };
    case 'typing':
      return { color: 'bg-flag-typing-bg', borderColor: 'border-flag-typing-border', textColor: 'text-flag-typing-text' };
    case 'listening':
      return { color: 'bg-flag-listening-bg', borderColor: 'border-flag-listening-border', textColor: 'text-flag-listening-text' };
    case 'observing_high':
      return { color: 'bg-flag-engaging-bg', borderColor: 'border-flag-engaging-border', textColor: 'text-flag-engaging-text' };
    case 'observing_low':
    case 'observing_medium':
      return { color: 'bg-flag-observing-bg', borderColor: 'border-flag-observing-border', textColor: 'text-flag-observing-text' };
    case 'idle':
    default: {
      const hasBusyTasks = activeUiTokens.length > 0 || isLive;
      if (activeUiTokens.length > 0) {
        const primaryConfig = UI_TOKEN_DISPLAY[getTokenSubtype(activeUiTokens[0])];
        if (primaryConfig?.color) {
          return {
            color: primaryConfig.color.bg,
            borderColor: primaryConfig.color.border,
            textColor: primaryConfig.color.text,
          };
        }
      }
      if (hasBusyTasks) {
        return { color: 'bg-flag-busy-bg/20', borderColor: 'border-flag-busy-border/30', textColor: 'text-flag-busy-text' };
      }
      return { color: 'bg-flag-idle-bg', borderColor: 'border-flag-idle-border', textColor: 'text-flag-idle-text' };
    }
  }
};

const CollapsedMaestroStatus: React.FC<CollapsedMaestroStatusProps> = ({
  stage,
  t,
  targetLanguageFlag,
  targetLanguageTitle,
  className,
  isExpanded = true,
}) => {
  const activeUiTokens = useMaestroStore(useShallow(selectActiveUiTokens));
  const isLive = useMaestroStore(selectIsLive);
  const silentObserverState = useMaestroStore(state => state.silentObserverState);
  const liveVideoStream = useMaestroStore(state => state.liveVideoStream);
  const isHolding = activeUiTokens.some(token => getTokenSubtype(token) === TOKEN_SUBTYPE.HOLD);

  const isObserverActive = silentObserverState === 'active' || silentObserverState === 'connecting';
  const showMicUsageBadge = isObserverActive || isLive;
  const showCameraUsageBadge = showMicUsageBadge && Boolean(liveVideoStream && liveVideoStream.active);
  const showHoldUsageBadge = isHolding;

  const displayTokens = useMemo(() => {
    const tokens: string[] = activeUiTokens.filter(token => getTokenSubtype(token) !== TOKEN_SUBTYPE.HOLD);
    if (isLive) {
      tokens.push(buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.SESSION));
    }
    return tokens
      .map(token => ({ token, config: getTokenDisplayConfig(token) }))
      .filter((entry): entry is { token: string; config: TokenDisplayConfig } => Boolean(entry.config))
      .sort((a, b) => (a.config.priority ?? 100) - (b.config.priority ?? 100));
  }, [activeUiTokens, isLive]);

  const renderUsageBadges = () => {
    if (!showHoldUsageBadge && !showMicUsageBadge) return null;

    const badges: React.ReactNode[] = [];
    if (showHoldUsageBadge) badges.push(<Icons.IconPause key="hold" className="w-2.5 h-2.5 drop-shadow-sm" />);
    if (showMicUsageBadge) badges.push(<Icons.IconMicrophone key="mic" className="w-2.5 h-2.5 drop-shadow-sm" />);
    if (showCameraUsageBadge) badges.push(<Icons.IconCamera key="cam" className="w-2.5 h-2.5 drop-shadow-sm" />);

    // Position badges as a superscript row to the right of the icon,
    // sitting clearly outside the icon boundary like an exponent.
    return (
      <span
        className="pointer-events-none inline-flex items-start gap-0.5 leading-none opacity-80 -mt-1.5 -ml-0.5"
        aria-hidden
      >
        {badges}
      </span>
    );
  };

  const renderIcon = (token: string, config: TokenDisplayConfig, idx: number, includeUsageBadges = false) => {
    const IconComponent = Icons[config.icon as keyof typeof Icons];
    if (!IconComponent) return null;
    return (
      <React.Fragment key={`${token}-${idx}`}>
        <span className="inline-flex items-center justify-center">
          <IconComponent
            className={`w-4 h-4 ${config.animate ? 'animate-pulse' : ''}`}
            title={t(config.titleKey)}
          />
        </span>
        {includeUsageBadges ? renderUsageBadges() : null}
      </React.Fragment>
    );
  };

  if (stage !== 'idle') {
    const config = STAGE_DISPLAY[stage];
    const IconComponent = Icons[config.icon as keyof typeof Icons];
    return (
      <div className={`flex items-center ${className || ''}`} title={t(config.titleKey)}>
        {IconComponent && (
          <>
            <span className="inline-flex items-center justify-center">
              <IconComponent className={`w-4 h-4 ${config.animate ? 'animate-pulse' : ''}`} />
            </span>
            {renderUsageBadges()}
          </>
        )}
        <div
          className={`flex items-center overflow-hidden transition-all duration-500 ease-in-out ${
            isExpanded ? 'max-w-xs opacity-100 ml-2' : 'max-w-0 opacity-0 ml-0'
          }`}
        >
          {targetLanguageFlag && (
            <span className="text-base mr-2" title={targetLanguageTitle}>
              {targetLanguageFlag}
            </span>
          )}
          <span className="text-xs font-semibold uppercase tracking-wide whitespace-nowrap">
            {t(config.textKey)}
          </span>
        </div>
      </div>
    );
  }

  if (displayTokens.length > 0) {
    const primary = displayTokens[0];
    return (
      <div className={`flex items-center ${className || ''}`} title={t(primary.config.titleKey)}>
        <div className="flex items-center gap-1">
          {renderIcon(primary.token, primary.config, 0, true)}
          {isExpanded &&
            displayTokens.slice(1).map((entry, idx) => renderIcon(entry.token, entry.config, idx + 1, false))}
        </div>
        <div
          className={`flex items-center overflow-hidden transition-all duration-500 ease-in-out ${
            isExpanded ? 'max-w-xs opacity-100 ml-2' : 'max-w-0 opacity-0 ml-0'
          }`}
        >
          {targetLanguageFlag && (
            <span className="text-base mr-2" title={targetLanguageTitle}>
              {targetLanguageFlag}
            </span>
          )}
          <span className="text-xs font-semibold uppercase tracking-wide whitespace-nowrap">
            {t(primary.config.textKey)}
          </span>
        </div>
      </div>
    );
  }

  const holdConfig = UI_TOKEN_DISPLAY[TOKEN_SUBTYPE.HOLD];
  const idleConfig = STAGE_DISPLAY.idle;
  const idleTextKey = isHolding && holdConfig ? holdConfig.textKey : idleConfig.textKey;
  const idleTitleKey = isHolding && holdConfig ? holdConfig.titleKey : idleConfig.titleKey;
  const IdleIcon = Icons[idleConfig.icon as keyof typeof Icons];
  return (
    <div className={`flex items-center ${className || ''}`} title={t(idleTitleKey)}>
      {IdleIcon && (
        <>
          <span className="inline-flex items-center justify-center">
            <IdleIcon className="w-4 h-4" />
          </span>
          {renderUsageBadges()}
        </>
      )}
      <div
        className={`flex items-center overflow-hidden transition-all duration-500 ease-in-out ${
          isExpanded ? 'max-w-xs opacity-100 ml-2' : 'max-w-0 opacity-0 ml-0'
        }`}
      >
        {targetLanguageFlag && (
          <span className="text-base mr-2" title={targetLanguageTitle}>
            {targetLanguageFlag}
          </span>
        )}
        <span className="text-xs font-semibold uppercase tracking-wide whitespace-nowrap">
          {t(idleTextKey)}
        </span>
      </div>
    </div>
  );
};

export default CollapsedMaestroStatus;
