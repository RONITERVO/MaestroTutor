
import React, { forwardRef, useState, useEffect } from 'react';
import CollapsedMaestroStatus, { getStatusConfig } from './CollapsedMaestroStatus';
import { LanguageDefinition } from '../../constants';
import { ChatMessage, MaestroActivityStage, LanguagePair } from '../../types';
import { TranslationReplacements } from '../../translations/index';

interface HeaderProps {
  isTopbarOpen: boolean; // Kept for prop compatibility, but functionality changed
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
  // Determine if we should show the full flag
  const isBusy = uiBusyTaskTags.length > 0;
  const isActive = maestroActivityStage !== 'idle' && maestroActivityStage !== 'observing_low';
  
  // Auto-expand logic based on state + hover
  const [isHovered, setIsHovered] = useState(false);
  
  // The flag is expanded if the system is doing something (speaking/listening), 
  // if there are UI tasks (uploading/annotating), or if the user hovers.
  const isExpanded = isActive || isBusy || isHovered;

  const statusConfig = getStatusConfig(maestroActivityStage, uiBusyTaskTags);

  return (
    <>
      {/* 
        Maestro Status Flag 
        Positioned fixed top-left. 
        Slides out from the left edge.
      */}
      <div 
        ref={ref}
        className={`fixed top-4 left-0 z-50 transition-all duration-300 ease-out shadow-md border-y border-r rounded-r-full flex items-center
          ${statusConfig.color} ${statusConfig.borderColor}
          ${isExpanded ? 'translate-x-0 pr-4 pl-3 py-1.5' : '-translate-x-2 pl-4 pr-3 py-1.5 opacity-80 hover:translate-x-0 hover:opacity-100'}
        `}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        onClick={onLanguageSelectorClick}
        role="status"
        aria-live="polite"
        style={{ cursor: 'pointer' }}
      >
        <div className={`transition-opacity duration-300 ${isExpanded ? 'opacity-100' : 'opacity-70'}`}>
          <CollapsedMaestroStatus
            stage={maestroActivityStage}
            t={t}
            uiBusyTaskTags={uiBusyTaskTags}
            targetLanguageFlag={isExpanded && selectedLanguagePair ? targetLanguageDef?.flag : undefined}
            targetLanguageTitle={selectedLanguagePair ? t('header.targetLanguageTitle', { language: targetLanguageDef?.displayName || '' }) : undefined}
            className={statusConfig.textColor}
          />
        </div>
      </div>
    </>
  );
});

Header.displayName = 'Header';
export default Header;
