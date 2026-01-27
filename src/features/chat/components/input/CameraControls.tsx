import React, { useMemo } from 'react';
import { CameraDevice } from '../../../../core/types';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { IMAGE_GEN_CAMERA_ID } from '../../../../core/config/app';
import { IconPaperclip, IconXMark, IconCameraFront, IconCamera, IconSparkles, IconBookOpen } from '../../../../shared/ui/Icons';

interface CameraControlsProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  isLanguageSelectionOpen: boolean;
  isSuggestionMode: boolean;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  onImageAttach: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onPaperclipClick: () => void;
  availableCameras: CameraDevice[];
  selectedCameraId: string | null;
  currentCameraFacingMode: 'user' | 'environment' | 'unknown';
  isImageGenCameraSelected: boolean;
  sendWithSnapshotEnabled: boolean;
  useVisualContextForReengagementEnabled: boolean;
  imageGenerationModeEnabled: boolean;
  onSelectCamera: (deviceId: string) => void;
  onToggleSendWithSnapshot: () => void;
  onToggleUseVisualContextForReengagement: () => void;
  onToggleImageGenerationMode: () => void;
  iconButtonStyle: string;
}

const CameraControls: React.FC<CameraControlsProps> = ({
  t,
  isLanguageSelectionOpen,
  isSuggestionMode,
  fileInputRef,
  onImageAttach,
  onPaperclipClick,
  availableCameras,
  selectedCameraId,
  currentCameraFacingMode,
  isImageGenCameraSelected,
  sendWithSnapshotEnabled,
  useVisualContextForReengagementEnabled,
  imageGenerationModeEnabled,
  onSelectCamera,
  onToggleSendWithSnapshot,
  onToggleUseVisualContextForReengagement,
  onToggleImageGenerationMode,
  iconButtonStyle,
}) => {
  const isCameraActive = sendWithSnapshotEnabled || useVisualContextForReengagementEnabled;

  const allCameraOptions = useMemo(() => {
    const cameraOptions: CameraDevice[] = [...availableCameras];
    if (imageGenerationModeEnabled) {
      cameraOptions.push({ deviceId: IMAGE_GEN_CAMERA_ID, label: t('chat.camera.imageGenCameraLabel'), facingMode: 'unknown' });
    }
    return cameraOptions;
  }, [availableCameras, imageGenerationModeEnabled, t]);

  // Symmetric toggle handlers: activation only turns features ON, deactivation only turns them OFF
  const handleCameraActivationClick = () => {
    if (!sendWithSnapshotEnabled) onToggleSendWithSnapshot();
    if (!useVisualContextForReengagementEnabled) onToggleUseVisualContextForReengagement();
  };

  const handleCameraDeactivationClick = () => {
    if (sendWithSnapshotEnabled) onToggleSendWithSnapshot();
    if (useVisualContextForReengagementEnabled) onToggleUseVisualContextForReengagement();
  };

  return (
    <div className="flex items-center space-x-1">
      {!isLanguageSelectionOpen && (
        <>
          <input
            type="file"
            accept="image/*,video/*,audio/*,application/pdf,text/plain,text/csv,text/markdown"
            ref={fileInputRef}
            onChange={onImageAttach}
            className="hidden"
            id="imageUpload"
          />
          <button
            type="button"
            className={`p-2 cursor-pointer rounded-full transition-colors ${iconButtonStyle}`}
            title={t('chat.attachImageFromFile')}
            onClick={onPaperclipClick}
          >
            <IconPaperclip className="w-5 h-5" />
          </button>
          {isCameraActive && allCameraOptions.length > 0 ? (
            <div className={`flex items-center p-0.5 ${isSuggestionMode ? 'bg-gray-300/50' : 'bg-blue-600/50'} rounded-full`}>
              <button type="button" onClick={handleCameraDeactivationClick} className="p-1.5 rounded-full bg-red-500 text-white hover:bg-red-600" title={t('chat.camera.turnOff')}>
                <IconXMark className="w-4 h-4" />
              </button>
              {/* Assistant Image Gen toggle (Book) inside camera selector, visually separated */}
              <button
                type="button"
                onClick={onToggleImageGenerationMode}
                className={`p-1.5 ml-1 rounded-full transition-colors ${imageGenerationModeEnabled ? (isSuggestionMode ? 'bg-white text-purple-700' : 'bg-white text-purple-700') : (isSuggestionMode ? 'text-gray-700 hover:bg-black/10' : 'text-blue-100 hover:bg-blue-400/80')}`}
                title={t('chat.bookIcon.toggleImageGen')}
              >
                <IconBookOpen className="w-4 h-4" />
              </button>
              <div className="flex items-center space-x-0.5 ml-1">
                {allCameraOptions.map(cam => {
                  const isSelected = cam.deviceId === selectedCameraId;
                  let Icon;
                  if (cam.deviceId === IMAGE_GEN_CAMERA_ID) Icon = IconSparkles;
                  else if (cam.facingMode === 'user') Icon = IconCameraFront;
                  else Icon = IconCamera;
                  return (
                    <button
                      type="button"
                      key={cam.deviceId}
                      onClick={() => onSelectCamera(cam.deviceId)}
                      className={`p-1.5 rounded-full transition-colors ${isSelected ? `${isSuggestionMode ? 'bg-white text-gray-800' : 'bg-white text-blue-600'}` : `${isSuggestionMode ? 'text-gray-600 hover:bg-black/10' : 'text-blue-100 hover:bg-blue-400/80'}`}`}
                      title={cam.label}
                    >
                      <Icon className="w-4 h-4" />
                    </button>
                  );
                })}
              </div>
            </div>
          ) : (
            <button
              type="button"
              onClick={handleCameraActivationClick}
              className={`p-2 cursor-pointer rounded-full transition-colors touch-manipulation ${isSuggestionMode ? 'text-gray-600 hover:text-black hover:bg-black/10' : 'hover:text-white hover:bg-blue-400/80'} ${isImageGenCameraSelected ? (isSuggestionMode ? 'text-purple-600' : 'text-purple-300 hover:text-purple-200') : ''}`}
              title={t('chat.camera.turnOn')}
            >
              {isImageGenCameraSelected ? <IconSparkles className="w-5 h-5" /> : (currentCameraFacingMode === 'user' ? <IconCameraFront className="w-5 h-5" /> : <IconCamera className="w-5 h-5" />)}
            </button>
          )}
        </>
      )}
    </div>
  );
};

export default CameraControls;
