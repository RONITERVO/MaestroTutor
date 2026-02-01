// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { forwardRef, useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useShallow } from 'zustand/react/shallow';
import CollapsedMaestroStatus, { getStatusConfig } from './CollapsedMaestroStatus';
import { IconShield, IconTerminal } from '../../../shared/ui/Icons';
import { useMaestroStore } from '../../../store';
import { parseLanguagePairId } from '../../../shared/utils/languageUtils';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { selectActiveUiTokens, selectIsLive, selectIsUserHold, selectIsSending } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair, selectTargetLanguageDef } from '../../../store/slices/settingsSlice';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';

interface HeaderProps {
  onOpenApiKey?: () => void;
  hasApiKey?: boolean;
}

const Header = forwardRef<HTMLDivElement, HeaderProps>(({ onOpenApiKey, hasApiKey }, ref) => {
  const { t } = useAppTranslations();
  const maestroActivityStage = useMaestroStore(state => state.maestroActivityStage);
  const selectedLanguagePair = useMaestroStore(selectSelectedLanguagePair);
  const targetLanguageDef = useMaestroStore(selectTargetLanguageDef);
  const toggleDebugLogs = useMaestroStore(state => state.toggleDebugLogs);
  const setIsLanguageSelectionOpen = useMaestroStore(state => state.setIsLanguageSelectionOpen);
  const setTempNativeLangCode = useMaestroStore(state => state.setTempNativeLangCode);
  const setTempTargetLangCode = useMaestroStore(state => state.setTempTargetLangCode);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  // Explicit open state managed by user interaction
  const [isOpen, setIsOpen] = useState(false);
  const timerRef = useRef<number | null>(null);
  const longPressTimerRef = useRef<number | null>(null);
  const isLongPressRef = useRef(false);

  // Auto-close after 5 seconds when opened
  useEffect(() => {
    if (isOpen) {
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = window.setTimeout(() => {
        setIsOpen(false);
      }, 5000);
    }
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [isOpen]);

  const handleLanguageSelectorClick = useCallback((_e: React.MouseEvent) => {
    const state = useMaestroStore.getState();
    if (selectIsSending(state)) return;
    setIsLanguageSelectionOpen(true);
    const currentPairId = state.settings.selectedLanguagePairId;
    if (currentPairId && typeof currentPairId === 'string') {
      // Parse language pair ID (format: "target-native")
      const parsed = parseLanguagePairId(currentPairId);
      if (parsed) {
        setTempTargetLangCode(parsed.targetCode);
        setTempNativeLangCode(parsed.nativeCode);
      } else {
        setTempNativeLangCode(null);
        setTempTargetLangCode(null);
      }
    } else {
      setTempNativeLangCode(null);
      setTempTargetLangCode(null);
    }
  }, [setIsLanguageSelectionOpen, setTempNativeLangCode, setTempTargetLangCode]);

  const handleClick = (e: React.MouseEvent) => {
    if (isLongPressRef.current) {
        e.stopPropagation();
        e.preventDefault();
        return;
    }
    
    if (!isOpen) {
      // First click: Open the flag to show text
      e.stopPropagation();
      setIsOpen(true);
    } else {
      // Second click (while open): Trigger the actual action (Language Selector)
      handleLanguageSelectorClick(e);
    }
  };

  const holdTokenRef = useRef<string | null>(null);
  const handleToggleHold = useCallback(() => {
    if (holdTokenRef.current) {
      removeActivityToken(holdTokenRef.current);
      holdTokenRef.current = null;
    } else {
      holdTokenRef.current = addActivityToken(TOKEN_CATEGORY.UI, TOKEN_SUBTYPE.HOLD);
    }
  }, [addActivityToken, removeActivityToken]);

  const handlePointerDown = (_e: React.PointerEvent) => {
      isLongPressRef.current = false;
      if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = window.setTimeout(() => {
          isLongPressRef.current = true;
            handleToggleHold();
          // Optional: vibrate to indicate success
          if (navigator.vibrate) navigator.vibrate(50);
      }, 800);
  };

  const handlePointerUp = () => {
      if (longPressTimerRef.current) {
          clearTimeout(longPressTimerRef.current);
          longPressTimerRef.current = null;
      }
  };

  const handlePointerLeave = () => {
      if (longPressTimerRef.current) {
          clearTimeout(longPressTimerRef.current);
          longPressTimerRef.current = null;
      }
  };

  const activeUiTokens = useMaestroStore(useShallow(selectActiveUiTokens));
  const isHolding = useMaestroStore(selectIsUserHold);
  const isLive = useMaestroStore(selectIsLive);

  const statusConfig = useMemo(
    () => getStatusConfig(maestroActivityStage, activeUiTokens, isHolding, isLive),
    [maestroActivityStage, activeUiTokens, isHolding, isLive]
  );

  return (
    <>
      {/* 
        Maestro Status Flag 
        Positioned fixed top-left. 
        Wrapped in a positioning div to allow separate transform animations (positioning vs waving).
      */}
      <div 
        ref={ref}
        className={`fixed top-4 left-0 z-50 transition-transform duration-500 ease-out
          ${isOpen ? 'translate-x-0' : '-translate-x-1 hover:translate-x-0'}
        `}
        style={{ top: 'calc(1rem + env(safe-area-inset-top))' }}
      >
        <div
          className={`flex items-center cursor-pointer select-none touch-none transition-all duration-300
            ${statusConfig.color} ${statusConfig.borderColor}
            ${statusConfig.textColor}
            ${isOpen ? 'shadow-md border-y border-r rounded-r-xl pr-4 pl-3 py-1.5' : 'drop-shadow-md pl-3 pr-3 py-2 animate-flag-wave'}
          `}
          onClick={handleClick}
          onPointerDown={handlePointerDown}
          onPointerUp={handlePointerUp}
          onPointerLeave={handlePointerLeave}
          onContextMenu={(e) => e.preventDefault()}
          role="status"
          aria-live="polite"
          title={!isOpen ? "Click to view status, Long press to Hold" : undefined}
        >
          <div className={`transition-opacity duration-300`}>
            <CollapsedMaestroStatus
              stage={maestroActivityStage}
              t={t}
              targetLanguageFlag={selectedLanguagePair ? targetLanguageDef?.flag : undefined}
              targetLanguageTitle={selectedLanguagePair ? t('header.targetLanguageTitle', { language: targetLanguageDef?.displayName || '' }) : undefined}
              className={statusConfig.textColor}
              isExpanded={isOpen}
            />
          </div>
        </div>
       </div>

       <div
         className={`fixed top-4 right-4 z-40 flex items-center gap-2 transition-all duration-300 ease-in-out ${
           isOpen ? 'opacity-100 translate-x-0 pointer-events-auto' : 'opacity-0 translate-x-4 pointer-events-none'
         }`}
         style={{ top: 'calc(1rem + env(safe-area-inset-top))' }}
       >
         {onOpenApiKey && (
           <button
             onClick={onOpenApiKey}
             className={`flex items-center gap-2 px-3 py-2 rounded-full shadow-sm backdrop-blur-sm transition-all text-xs sm:text-sm
               ${hasApiKey ? 'bg-emerald-600/85 text-white hover:bg-emerald-600' : 'bg-rose-600/90 text-white hover:bg-rose-600'}
             `}
             title={hasApiKey ? 'Manage API Key' : 'API Key Required'}
           >
             <IconShield className="w-4 h-4" />
             <span className="hidden sm:inline">{hasApiKey ? 'API Key' : 'API Key Required'}</span>
           </button>
         )}

         <button
           onClick={toggleDebugLogs}
           className="p-2 bg-slate-800/80 hover:bg-slate-700 text-slate-400 hover:text-white rounded-full shadow-sm backdrop-blur-sm transition-all"
           title="View Traffic Logs"
         >
           <IconTerminal className="w-4 h-4" />
         </button>
      </div>
    </>
  );
});

Header.displayName = 'Header';
export default Header;
