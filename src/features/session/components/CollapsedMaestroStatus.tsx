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
    return { color: 'bg-fuchsia-500', borderColor: 'border-fuchsia-600', textColor: 'text-white' };
  }

  switch (stage) {
    case 'speaking':
      return { color: 'bg-blue-500', borderColor: 'border-blue-600', textColor: 'text-white' };
    case 'typing':
      return { color: 'bg-blue-400', borderColor: 'border-blue-500', textColor: 'text-white' };
    case 'listening':
      return { color: 'bg-green-500', borderColor: 'border-green-600', textColor: 'text-white' };
    case 'observing_high':
      return { color: 'bg-amber-500', borderColor: 'border-amber-600', textColor: 'text-white' };
    case 'observing_low':
    case 'observing_medium':
      return { color: 'bg-slate-200', borderColor: 'border-slate-300', textColor: 'text-slate-600' };
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
        return { color: 'bg-indigo-100', borderColor: 'border-indigo-200', textColor: 'text-indigo-700' };
      }
      return { color: 'bg-slate-100', borderColor: 'border-slate-200', textColor: 'text-slate-500' };
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

  const displayTokens = useMemo(() => {
    const tokens: string[] = [...activeUiTokens];
    if (isLive) {
      tokens.push(buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.SESSION));
    }
    return tokens
      .map(token => ({ token, config: getTokenDisplayConfig(token) }))
      .filter((entry): entry is { token: string; config: TokenDisplayConfig } => Boolean(entry.config))
      .sort((a, b) => (a.config.priority ?? 100) - (b.config.priority ?? 100));
  }, [activeUiTokens, isLive]);

  const renderIcon = (token: string, config: TokenDisplayConfig, idx: number) => {
    const IconComponent = Icons[config.icon as keyof typeof Icons];
    if (!IconComponent) return null;
    return (
      <IconComponent
        key={`${token}-${idx}`}
        className={`w-4 h-4 ${config.animate ? 'animate-pulse' : ''}`}
        title={t(config.titleKey)}
      />
    );
  };

  if (stage !== 'idle') {
    const config = STAGE_DISPLAY[stage];
    const IconComponent = Icons[config.icon as keyof typeof Icons];
    return (
      <div className={`flex items-center ${className || ''}`} title={t(config.titleKey)}>
        {IconComponent && (
          <IconComponent className={`w-4 h-4 ${config.animate ? 'animate-pulse' : ''}`} />
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
          {renderIcon(primary.token, primary.config, 0)}
          {isExpanded &&
            displayTokens.slice(1).map((entry, idx) => renderIcon(entry.token, entry.config, idx + 1))}
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

  const idleConfig = STAGE_DISPLAY.idle;
  const IdleIcon = Icons[idleConfig.icon as keyof typeof Icons];
  return (
    <div className={`flex items-center ${className || ''}`} title={t(idleConfig.titleKey)}>
      {IdleIcon && <IdleIcon className="w-4 h-4" />}
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
          {t(idleConfig.textKey)}
        </span>
      </div>
    </div>
  );
};

export default CollapsedMaestroStatus;
