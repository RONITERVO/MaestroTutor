
import React from 'react';
import { TranslationReplacements } from '../../translations/index';
import { MaestroActivityStage } from '../../types';
import {
  IconSpeaker,
  IconKeyboard,
  IconMicrophone,
  IconSleepingZzz,
  IconEyeOpen,
  IconSparkles,
  IconCamera,
  IconPlay,
  IconBookOpen,
  IconPencil,
  IconSave,
  IconFolderOpen,
  IconSend,
  IconHandRaised
} from '../../constants';

interface CollapsedMaestroStatusProps {
  stage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;
  uiBusyTaskTags?: string[];
  targetLanguageFlag?: string;
  targetLanguageTitle?: string;
  className?: string;
  isExpanded?: boolean;
}

// Helper to get configuration for the parent container (The Flag)
export const getStatusConfig = (stage: MaestroActivityStage, uiBusyTaskTags: string[] = []) => {
  const hasBusyTasks = uiBusyTaskTags.filter(Boolean).length > 0;
  
  // Check specifically for hold tag to override generic busy color
  if (uiBusyTaskTags.includes('user-hold')) {
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
    default:
      if (hasBusyTasks) {
        // Generic busy state
        return { color: 'bg-indigo-100', borderColor: 'border-indigo-200', textColor: 'text-indigo-700' };
      }
      return { color: 'bg-slate-100', borderColor: 'border-slate-200', textColor: 'text-slate-500' };
  }
};

const CollapsedMaestroStatus: React.FC<CollapsedMaestroStatusProps> = ({ 
  stage, 
  t, 
  uiBusyTaskTags, 
  targetLanguageFlag, 
  targetLanguageTitle, 
  className,
  isExpanded = true 
}) => {
  let iconElement: React.ReactNode = null;
  let textKey: string = "";
  let titleKey: string = "";
  // Default text color logic (can be overridden by parent via className or the config helper above)
  let baseTextColor = "text-inherit"; 

  switch (stage) {
    case 'speaking':
      iconElement = <IconSpeaker className="w-4 h-4 animate-pulse" />;
      textKey = "chat.maestro.speaking";
      titleKey = "chat.maestro.title.speaking";
      break;
    case 'typing':
      iconElement = <IconKeyboard className="w-4 h-4" />;
      textKey = "chat.maestro.typing";
      titleKey = "chat.maestro.title.typing";
      break;
    case 'listening':
      iconElement = <IconMicrophone className="w-4 h-4" />;
      textKey = "chat.maestro.listening";
      titleKey = "chat.maestro.title.listening";
      break;
    case 'observing_low':
      iconElement = <IconSleepingZzz className="w-4 h-4" />;
      textKey = "chat.maestro.resting";
      titleKey = "chat.maestro.title.resting";
      break;
    case 'observing_medium':
      iconElement = <IconEyeOpen className="w-4 h-4" />;
      textKey = "chat.maestro.observing";
      titleKey = "chat.maestro.title.observing";
      break;
    case 'observing_high':
      iconElement = <IconKeyboard className="w-4 h-4" />;
      textKey = "chat.maestro.aboutToEngage";
      titleKey = "chat.maestro.title.aboutToEngage";
      break;
    case 'idle':
    default: {
      const activeTags = (uiBusyTaskTags || []).filter(Boolean);
      if (activeTags.length > 0) {
        const tagToIcon = (tag: string, idx: number) => {
          const key = `${tag}-${idx}`;
          const base = 'w-4 h-4';
          switch (tag) {
            case 'user-hold':
              return <IconHandRaised key={key} className={`${base} animate-pulse`} title={t('chat.maestro.title.holding')} />;
            case 'live-session':
              return <IconCamera key={key} className={`${base}`} title={'Live session active'} />;
            case 'video-play':
              return <IconPlay key={key} className={`${base}`} title={t('chat.header.watchingVideo')} />;
            case 'viewing-above':
              return <IconBookOpen key={key} className={`${base}`} title={t('chat.header.viewingAbove')} />;
            case 'bubble-annotate':
              return <IconPencil key={key} className={`${base}`} title={t('chat.header.annotating')} />;
            case 'composer-annotate':
              return <IconPencil key={key} className={`${base}`} title={t('chat.header.annotating')} />;
            case 'video-record':
              return <IconCamera key={key} className={`${base}`} title={t('chat.header.recordingVideo')} />;
            case 'audio-note':
              return <IconMicrophone key={key} className={`${base}`} title={t('chat.header.recordingAudio')} />;
            case 'save-popup':
              return <IconSave key={key} className={`${base}`} title={t('chat.header.savePopup')} />;
            case 'load-popup':
              return <IconFolderOpen key={key} className={`${base}`} title={t('chat.header.loadPopup')} />;
            case 'maestro-avatar':
              return <IconSend key={key} className={`${base}`} title={t('chat.header.maestroAvatar')} />;
            default:
              return <span key={key} className={`${base} rounded-full bg-current opacity-50 inline-block`} title={tag} />;
          }
        };
        // Show first icon always, others if expanded
        const primaryTag = activeTags.includes('user-hold') ? 'user-hold' : activeTags[0];
        
        // Ensure user-hold icon is prioritized if active
        let iconsToRender = [tagToIcon(primaryTag, 0)];
        if (isExpanded) {
            activeTags.forEach((tag, i) => {
                if (tag !== primaryTag) iconsToRender.push(tagToIcon(tag, i + 1));
            });
        }

        iconElement = (
          <div className="flex items-center gap-1">
             {iconsToRender}
          </div>
        );
        
        // Determine text based on the *first* recognized tag priority
        if (primaryTag === 'user-hold') {
           textKey = 'chat.maestro.holding';
           titleKey = 'chat.maestro.title.holding';
        } else if (primaryTag === 'bubble-annotate' || primaryTag === 'composer-annotate') {
           textKey = 'chat.header.annotating';
           titleKey = 'chat.header.annotating';
        } else if (primaryTag === 'video-record') {
           textKey = 'chat.header.recordingVideo';
           titleKey = 'chat.header.recordingVideo';
        } else if (primaryTag === 'audio-note') {
           textKey = 'chat.header.recordingAudio';
           titleKey = 'chat.header.recordingAudio';
        } else if (primaryTag === 'save-popup') {
           textKey = 'chat.header.savePopup';
           titleKey = 'chat.header.savePopup';
        } else if (primaryTag === 'load-popup') {
           textKey = 'chat.header.loadPopup';
           titleKey = 'chat.header.loadPopup';
        } else if (primaryTag === 'maestro-avatar') {
           textKey = 'chat.header.maestroAvatar';
           titleKey = 'chat.header.maestroAvatar';
        } else if (primaryTag === 'video-play') {
           textKey = 'chat.header.watchingVideo';
           titleKey = 'chat.header.watchingVideo';
        } else if (primaryTag === 'viewing-above') {
           textKey = 'chat.header.viewingAbove';
           titleKey = 'chat.header.viewingAbove';
        } else {
           textKey = 'chat.maestro.idle';
           titleKey = 'chat.maestro.title.idle';
        }
      } else {
        iconElement = <IconSparkles className="w-4 h-4" />;
        textKey = "chat.maestro.idle";
        titleKey = "chat.maestro.title.idle";
      }
      break;
    }
  }

  return (
    <div className={`flex items-center ${className || baseTextColor}`} title={t(titleKey)}>
      {iconElement}
      
      <div 
        className={`flex items-center overflow-hidden transition-all duration-500 ease-in-out ${isExpanded ? 'max-w-xs opacity-100 ml-2' : 'max-w-0 opacity-0 ml-0'}`}
      >
        {targetLanguageFlag && <span className="text-base mr-2" title={targetLanguageTitle}>{targetLanguageFlag}</span>}
        <span className="text-xs font-semibold uppercase tracking-wide whitespace-nowrap">{t(textKey)}</span>
      </div>
    </div>
  );
};

export default CollapsedMaestroStatus;
