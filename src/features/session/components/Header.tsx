
import React, { forwardRef, useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useShallow } from 'zustand/react/shallow';
import CollapsedMaestroStatus, { getStatusConfig } from './CollapsedMaestroStatus';
import { IconTerminal } from '../../../shared/ui/Icons';
import { useMaestroStore } from '../../../store';
import { parseLanguagePairId } from '../../../shared/utils/languageUtils';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { selectActiveUiTokens, selectIsLive, selectIsUserHold, selectIsSending } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair, selectTargetLanguageDef } from '../../../store/slices/settingsSlice';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';

const Header = forwardRef<HTMLDivElement>((_, ref) => {
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
        Defaults to a small icon-only view. Expands on click.
      */}
      <div 
        ref={ref}
        className={`fixed top-4 left-0 z-50 transition-all duration-500 ease-out shadow-md border-y border-r rounded-r-full flex items-center cursor-pointer select-none touch-none
          ${statusConfig.color} ${statusConfig.borderColor}
          ${isOpen ? 'pr-4 pl-3 py-1.5 translate-x-0' : 'pl-3 pr-1 py-1.5 -translate-x-1 hover:translate-x-0'}
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

      {/* Debug Log Toggle (Top Right) */}
      <button
        onClick={toggleDebugLogs}
        className="fixed top-4 right-4 z-40 p-2 bg-slate-800/80 hover:bg-slate-700 text-slate-400 hover:text-white rounded-full shadow-sm backdrop-blur-sm transition-all"
        title="View Traffic Logs"
      >
        <IconTerminal className="w-4 h-4" />
      </button>
    </>
  );
});

Header.displayName = 'Header';
export default Header;
