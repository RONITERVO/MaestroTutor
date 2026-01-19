
import React, { forwardRef, useState, useEffect, useRef } from 'react';
import CollapsedMaestroStatus, { getStatusConfig } from './CollapsedMaestroStatus';
import { IconTerminal } from '../../../shared/ui/Icons';
import { LanguageDefinition } from '../../../core/config/languages';
import { ChatMessage, MaestroActivityStage, LanguagePair } from '../../../core/types';
import { TranslationReplacements } from '../../../core/i18n/index';

interface HeaderProps {
  isTopbarOpen: boolean; // Kept for prop compatibility
  setIsTopbarOpen: (open: boolean) => void;
  maestroActivityStage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;
  uiBusyTaskTags: string[];
  targetLanguageDef?: LanguageDefinition;
  selectedLanguagePair: LanguagePair | undefined;
  messages: ChatMessage[];
  onLanguageSelectorClick: (e: React.MouseEvent) => void;
  onToggleDebugLogs: () => void;
  onToggleHold: () => void;
}

const Header = forwardRef<HTMLDivElement, HeaderProps>(({
  maestroActivityStage,
  t,
  uiBusyTaskTags,
  targetLanguageDef,
  selectedLanguagePair,
  onLanguageSelectorClick,
  onToggleDebugLogs,
  onToggleHold,
}, ref) => {
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
      onLanguageSelectorClick(e);
    }
  };

  const handlePointerDown = (_e: React.PointerEvent) => {
      isLongPressRef.current = false;
      if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = window.setTimeout(() => {
          isLongPressRef.current = true;
          onToggleHold();
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

  const statusConfig = getStatusConfig(maestroActivityStage, uiBusyTaskTags);

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
            uiBusyTaskTags={uiBusyTaskTags}
            targetLanguageFlag={selectedLanguagePair ? targetLanguageDef?.flag : undefined}
            targetLanguageTitle={selectedLanguagePair ? t('header.targetLanguageTitle', { language: targetLanguageDef?.displayName || '' }) : undefined}
            className={statusConfig.textColor}
            isExpanded={isOpen}
          />
        </div>
      </div>

      {/* Debug Log Toggle (Top Right) */}
      <button
        onClick={onToggleDebugLogs}
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
