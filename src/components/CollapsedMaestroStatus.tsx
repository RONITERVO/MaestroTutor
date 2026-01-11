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
  IconSend
} from '../../constants';

interface CollapsedMaestroStatusProps {
  stage: MaestroActivityStage;
  t: (key: string, replacements?: TranslationReplacements) => string;
  uiBusyTaskTags?: string[];
  targetLanguageFlag?: string;
  targetLanguageTitle?: string;
}

const CollapsedMaestroStatus: React.FC<CollapsedMaestroStatusProps> = ({ stage, t, uiBusyTaskTags, targetLanguageFlag, targetLanguageTitle }) => {
  let iconElement: React.ReactNode = null;
  let textKey: string = "";
  let titleKey: string = "";
  let textColor = "text-gray-600";

  switch (stage) {
    case 'speaking':
      iconElement = <IconSpeaker className="w-4 h-4 text-blue-500 animate-pulse" />;
      textKey = "chat.maestro.speaking";
      titleKey = "chat.maestro.title.speaking";
      textColor = "text-blue-600";
      break;
    case 'typing':
      iconElement = <IconKeyboard className="w-4 h-4 text-blue-500" />;
      textKey = "chat.maestro.typing";
      titleKey = "chat.maestro.title.typing";
      textColor = "text-blue-600";
      break;
    case 'listening':
      iconElement = <IconMicrophone className="w-4 h-4 text-green-600" />;
      textKey = "chat.maestro.listening";
      titleKey = "chat.maestro.title.listening";
      textColor = "text-green-700";
      break;
    case 'observing_low':
      iconElement = <IconSleepingZzz className="w-4 h-4 text-gray-400" />;
      textKey = "chat.maestro.resting";
      titleKey = "chat.maestro.title.resting";
      textColor = "text-gray-500";
      break;
    case 'observing_medium':
      iconElement = <IconEyeOpen className="w-4 h-4 text-gray-500" />;
      textKey = "chat.maestro.observing";
      titleKey = "chat.maestro.title.observing";
      textColor = "text-gray-600";
      break;
    case 'observing_high':
      iconElement = <IconKeyboard className="w-4 h-4 text-amber-600" />;
      textKey = "chat.maestro.aboutToEngage";
      titleKey = "chat.maestro.title.aboutToEngage";
      textColor = "text-amber-700";
      break;
    case 'idle':
    default: {
      const activeTags = (uiBusyTaskTags || []).filter(Boolean);
      if (activeTags.length > 0) {
        const tagToIcon = (tag: string, idx: number) => {
          const key = `${tag}-${idx}`;
          const base = 'w-4 h-4';
          switch (tag) {
            case 'live-session':
              return <IconCamera key={key} className={`${base} text-green-600`} title={'Live session active'} />;
            case 'video-play':
              return <IconPlay key={key} className={`${base} text-lime-600`} title={t('chat.header.watchingVideo')} />;
            case 'viewing-above':
              return <IconBookOpen key={key} className={`${base} text-indigo-600`} title={t('chat.header.viewingAbove')} />;
            case 'bubble-annotate':
              return <IconPencil key={key} className={`${base} text-amber-600`} title={t('chat.header.annotating')} />;
            case 'composer-annotate':
              return <IconPencil key={key} className={`${base} text-amber-600`} title={t('chat.header.annotating')} />;
            case 'video-record':
              return <IconCamera key={key} className={`${base} text-red-600`} title={t('chat.header.recordingVideo')} />;
            case 'audio-note':
              return <IconMicrophone key={key} className={`${base} text-red-600`} title={t('chat.header.recordingAudio')} />;
            case 'save-popup':
              return <IconSave key={key} className={`${base} text-sky-600`} title={t('chat.header.savePopup')} />;
            case 'load-popup':
              return <IconFolderOpen key={key} className={`${base} text-emerald-600`} title={t('chat.header.loadPopup')} />;
            case 'maestro-avatar':
              return <IconSend key={key} className={`${base} text-purple-600`} title={t('chat.header.maestroAvatar')} />;
            default:
              return <span key={key} className={`${base} rounded-full bg-gray-400 inline-block`} title={tag} />;
          }
        };
        iconElement = (
          <div className="flex items-center gap-1">
            {activeTags.map((tag, i) => tagToIcon(tag, i))}
          </div>
        );
        textKey = 'chat.maestro.idle';
        titleKey = 'chat.maestro.title.idle';
        textColor = 'text-gray-600';
      } else {
        iconElement = <IconSparkles className="w-4 h-4 text-gray-500" />;
        textKey = "chat.maestro.idle";
        titleKey = "chat.maestro.title.idle";
        textColor = "text-gray-600";
      }
      break;
    }
  }

  return (
    <div className={`flex items-center space-x-1.5 ${textColor}`} title={t(titleKey)}>
      {iconElement}
      {targetLanguageFlag && <span className="text-base" title={targetLanguageTitle}>{targetLanguageFlag}</span>}
      <span className="text-xs font-medium">{t(textKey)}</span>
    </div>
  );
};

export default CollapsedMaestroStatus;
