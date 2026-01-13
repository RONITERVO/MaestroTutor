
import React, { forwardRef, useState, useEffect, useRef } from 'react';
import CollapsedMaestroStatus, { getStatusConfig } from './CollapsedMaestroStatus';
import { LanguageDefinition } from '../../constants';
import { ChatMessage, MaestroActivityStage, LanguagePair } from '../../types';
import { TranslationReplacements } from '../../translations/index';

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
}

const Header = forwardRef<HTMLDivElement, HeaderProps>(({
  maestroActivityStage,
  t,
  uiBusyTaskTags,
  targetLanguageDef,
  selectedLanguagePair,
  onLanguageSelectorClick,
}, ref) => {
  // Explicit open state managed by user interaction
  const [isOpen, setIsOpen] = useState(false);
  const timerRef = useRef<number | null>(null);

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
    if (!isOpen) {
      // First click: Open the flag to show text
      e.stopPropagation();
      setIsOpen(true);
    } else {
      // Second click (while open): Trigger the actual action (Language Selector)
      onLanguageSelectorClick(e);
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
        className={`fixed top-4 left-0 z-50 transition-all duration-500 ease-out shadow-md border-y border-r rounded-r-full flex items-center cursor-pointer
          ${statusConfig.color} ${statusConfig.borderColor}
          ${isOpen ? 'pr-4 pl-3 py-1.5 translate-x-0' : 'pl-3 pr-1 py-1.5 -translate-x-1 hover:translate-x-0'}
        `}
        onClick={handleClick}
        role="status"
        aria-live="polite"
        title={!isOpen ? "Click to view status" : undefined}
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
    </>
  );
});

Header.displayName = 'Header';
export default Header;
