
import React, { forwardRef, useState } from 'react';
import CollapsedMaestroStatus from './CollapsedMaestroStatus';
import GlobalProfileSummary from './GlobalProfileSummary';
import { IconPencil, IconCog, LanguageDefinition } from '../../constants';
import { ChatMessage, MaestroActivityStage, LanguagePair } from '../../types';
import { TranslationReplacements } from '../../translations/index';
import { getGlobalProfileDB, setGlobalProfileDB } from '../services/globalProfile';

interface HeaderProps {
  isTopbarOpen: boolean;
  setIsTopbarOpen: (open: boolean) => void;
  maestroActivityStage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;
  uiBusyTaskTags: string[];
  targetLanguageDef?: LanguageDefinition;
  selectedLanguagePair: LanguagePair | undefined;
  messages: ChatMessage[];
  onLanguageSelectorClick: (e: React.MouseEvent) => void;
  sttProvider?: string;
  ttsProvider?: string;
  onToggleSttProvider?: () => void;
  onToggleTtsProvider?: () => void;
  isSpeechRecognitionSupported?: boolean;
  mediaOptimizationEnabled?: boolean;
  onToggleMediaOptimization?: () => void;
}

const Header = forwardRef<HTMLDivElement, HeaderProps>(({
  isTopbarOpen,
  setIsTopbarOpen,
  maestroActivityStage,
  t,
  uiBusyTaskTags,
  targetLanguageDef,
  selectedLanguagePair,
  messages,
  onLanguageSelectorClick,
  sttProvider,
  ttsProvider,
  onToggleSttProvider,
  onToggleTtsProvider,
  isSpeechRecognitionSupported
}, ref) => {
  const [showSettings, setShowSettings] = useState(false);

  return (
    <div
      className={`fixed top-0 left-0 right-0 ${isTopbarOpen ? 'py-2 bg-slate-100/95 shadow-sm' : 'py-1 bg-slate-100/80'} backdrop-blur supports-[backdrop-filter]:bg-slate-100/60 border-b border-slate-200 flex-none px-3 text-slate-800 z-50 overflow-x-hidden transition-[padding,background-color]`}
      role="banner"
      ref={ref}
      onClick={() => { if (!isTopbarOpen) { setIsTopbarOpen(true); } }}
      title={!isTopbarOpen ? t('chat.header.backToHome') : undefined}
      aria-expanded={isTopbarOpen}
      aria-controls="topbar-controls"
    >
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <button
          type="button"
          className="cursor-pointer inline-flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-slate-400 rounded"
          onClick={onLanguageSelectorClick}
          title={t('chat.header.backToHome')}
          aria-label={t('chat.header.backToHome')}
        >
          <CollapsedMaestroStatus
            stage={maestroActivityStage}
            t={t}
            uiBusyTaskTags={uiBusyTaskTags}
            targetLanguageFlag={selectedLanguagePair ? targetLanguageDef?.flag : undefined}
            targetLanguageTitle={selectedLanguagePair ? t('header.targetLanguageTitle', { language: targetLanguageDef?.displayName || '' }) : undefined}
          />
        </button>

        {isTopbarOpen && (
          <div id="topbar-controls" className="flex items-center gap-2 flex-wrap min-w-0">
              <div className="flex items-center gap-2 bg-slate-200/60 px-2 py-1 rounded w-full max-w-full overflow-x-hidden min-w-0">
                <GlobalProfileSummary t={t} messages={messages} />

                  <button
                    className="p-1 rounded hover:bg-slate-300/80 text-slate-700"
                    title="Edit global profile"
                    aria-label="Edit global profile"
                    onClick={async (e) => {
                      e.stopPropagation();
                      try {
                        const current = (await getGlobalProfileDB())?.text ?? '';
                        const next = window.prompt('Edit global profile', current);
                        if (next !== null) {
                          const trimmed = next.trim();
                          await setGlobalProfileDB(trimmed);
                          try { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); } catch {}
                        }
                      } catch {}
                    }}
                  >
                    <IconPencil className="w-4 h-4" />
                  </button>
            </div>
            
            <button
                className="p-1.5 rounded hover:bg-slate-300/80 text-slate-700"
                title="Settings"
                aria-label="Settings"
                onClick={(e) => { e.stopPropagation(); setShowSettings(!showSettings); }}
            >
                <span className="text-lg">⚙️</span>
            </button>
          </div>
        )}
      </div>
      
      {showSettings && isTopbarOpen && (
          <div className="mt-2 p-3 bg-white rounded shadow-lg border border-slate-200 text-sm animate-in fade-in slide-in-from-top-2">
              <div className="flex flex-col gap-2">
                  <div className="flex items-center justify-between">
                      <span className="font-medium text-slate-700">Speech Recognition (STT)</span>
                      <button 
                          onClick={onToggleSttProvider} 
                          className="px-2 py-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs border border-slate-300 min-w-[80px]"
                          disabled={!isSpeechRecognitionSupported && sttProvider === 'gemini'}
                      >
                          {sttProvider === 'gemini' ? 'Gemini Live' : 'Browser'}
                      </button>
                  </div>
                  <div className="text-xs text-slate-500">
                      {sttProvider === 'gemini' ? 'High quality, online only.' : 'Faster, works offline (if browser supports).'}
                  </div>
                  
                  <div className="h-px bg-slate-100 my-1"></div>

                  <div className="flex items-center justify-between">
                      <span className="font-medium text-slate-700">Text-to-Speech (TTS)</span>
                      <button 
                          onClick={onToggleTtsProvider} 
                          className="px-2 py-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs border border-slate-300 min-w-[80px]"
                      >
                          {ttsProvider === 'gemini' ? 'Gemini' : 'Browser'}
                      </button>
                  </div>
                  <div className="text-xs text-slate-500">
                      {ttsProvider === 'gemini' ? 'Natural neural voice, online only.' : 'Robotic, works offline.'}
                  </div>
              </div>
          </div>
      )}
    </div>
  );
});

Header.displayName = 'Header';
export default Header;