// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo, useRef, useState, useCallback, useEffect } from 'react';
import { CameraDevice } from '../../../../core/types';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { IMAGE_GEN_CAMERA_ID } from '../../../../core/config/app';
import { IconPaperclip, IconCameraOff, IconCameraFront, IconCamera, IconSparkles, IconBookOpen } from '../../../../shared/ui/Icons';

// Data attribute for identifying interactive elements
const DATA_ACTION = 'data-camera-action';
const DATA_DEVICE_ID = 'data-device-id';

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

// Special device ID for "camera off" option
const CAMERA_OFF_ID = '__camera_off__';

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

  // Unified interaction state
  const [isExpanded, setIsExpanded] = useState(false);
  const [highlightedAction, setHighlightedAction] = useState<string | null>(null);
  const [isFlashing, setIsFlashing] = useState(false);
  
  // Pointer tracking
  const pointerTypeRef = useRef<'mouse' | 'touch' | 'pen' | null>(null);
  const activePointerIdRef = useRef<number | null>(null);
  const isDraggingRef = useRef(false);
  const dragStartPosRef = useRef<{ x: number; y: number } | null>(null);
  const wasExpandedOnTouchStartRef = useRef(false);
  
  // Timers
  const collapseTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const autoCollapseTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Clean up timers on unmount
  useEffect(() => {
    return () => {
      if (collapseTimeoutRef.current) clearTimeout(collapseTimeoutRef.current);
      if (autoCollapseTimeoutRef.current) clearTimeout(autoCollapseTimeoutRef.current);
    };
  }, []);

  // Find the action target from an element (walks up the DOM tree)
  const getActionFromElement = useCallback((element: Element | null): { action: string; deviceId?: string } | null => {
    let current = element;
    while (current && current !== document.body) {
      const action = current.getAttribute(DATA_ACTION);
      if (action) {
        const deviceId = current.getAttribute(DATA_DEVICE_ID) || undefined;
        return { action, deviceId };
      }
      current = current.parentElement;
    }
    return null;
  }, []);

  // Get element at point (handles touch accurately)
  const getElementAtPoint = useCallback((x: number, y: number): Element | null => {
    return document.elementFromPoint(x, y);
  }, []);

  // Clear all collapse timers
  const clearCollapseTimers = useCallback(() => {
    if (collapseTimeoutRef.current) {
      clearTimeout(collapseTimeoutRef.current);
      collapseTimeoutRef.current = null;
    }
    if (autoCollapseTimeoutRef.current) {
      clearTimeout(autoCollapseTimeoutRef.current);
      autoCollapseTimeoutRef.current = null;
    }
  }, []);

  // Trigger flash effect and delayed collapse
  const triggerFlashAndCollapse = useCallback(() => {
    clearCollapseTimers();
    setIsFlashing(true);

    // End flash after 250ms
    setTimeout(() => setIsFlashing(false), 250);

    // Collapse after 600ms total (gives time to see the selection)
    collapseTimeoutRef.current = setTimeout(() => {
      setIsExpanded(false);
      setHighlightedAction(null);
    }, 600);
  }, [clearCollapseTimers]);

  // Start auto-collapse timer
  const startAutoCollapseTimer = useCallback((delay: number = 2000) => {
    if (autoCollapseTimeoutRef.current) {
      clearTimeout(autoCollapseTimeoutRef.current);
    }
    autoCollapseTimeoutRef.current = setTimeout(() => {
      setIsExpanded(false);
      setHighlightedAction(null);
    }, delay);
  }, []);

  // Execute an action
  const executeAction = useCallback((action: string, deviceId?: string) => {
    switch (action) {
      case 'expand':
        clearCollapseTimers();
        setIsExpanded(true);
        startAutoCollapseTimer();
        break;
        
      case 'book-toggle':
        onToggleImageGenerationMode();
        triggerFlashAndCollapse();
        break;
        
      case 'camera-select':
        if (deviceId === CAMERA_OFF_ID) {
          // Turn off camera
          if (sendWithSnapshotEnabled) onToggleSendWithSnapshot();
          if (useVisualContextForReengagementEnabled) onToggleUseVisualContextForReengagement();
          triggerFlashAndCollapse();
        } else if (deviceId) {
          // Select camera (or re-enable same camera if it was turned off)
          if (deviceId !== selectedCameraId) {
            onSelectCamera(deviceId);
          }
          // Always ensure camera features are enabled when selecting any camera
          if (!sendWithSnapshotEnabled) onToggleSendWithSnapshot();
          if (!useVisualContextForReengagementEnabled) onToggleUseVisualContextForReengagement();
          triggerFlashAndCollapse();
        }
        break;
    }
  }, [
    clearCollapseTimers, startAutoCollapseTimer, triggerFlashAndCollapse,
    onToggleImageGenerationMode, onSelectCamera, selectedCameraId,
    sendWithSnapshotEnabled, useVisualContextForReengagementEnabled,
    onToggleSendWithSnapshot, onToggleUseVisualContextForReengagement
  ]);

  // Unified pointer down handler
  const handlePointerDown = useCallback((e: React.PointerEvent) => {
    // Track pointer type for behavior differentiation
    pointerTypeRef.current = e.pointerType as 'mouse' | 'touch' | 'pen';
    activePointerIdRef.current = e.pointerId;
    isDraggingRef.current = false;
    dragStartPosRef.current = { x: e.clientX, y: e.clientY };

    const target = getActionFromElement(e.target as Element);
    
    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      // Track whether selector was already open before this touch
      wasExpandedOnTouchStartRef.current = isExpanded;
      // Touch/pen: expand immediately on any touch within cluster
      clearCollapseTimers();
      setIsExpanded(true);
      
      if (target) {
        setHighlightedAction(target.deviceId ? `camera-${target.deviceId}` : target.action);
      }
      
      // Capture pointer for drag tracking
      (e.target as Element).setPointerCapture?.(e.pointerId);
    } else {
      // Mouse: only expand on center button click
      if (target?.action === 'expand') {
        clearCollapseTimers();
        setIsExpanded(true);
        startAutoCollapseTimer();
      }
    }
  }, [getActionFromElement, clearCollapseTimers, startAutoCollapseTimer, isExpanded]);

  // Unified pointer move handler
  const handlePointerMove = useCallback((e: React.PointerEvent) => {
    // Only track moves for the active pointer
    if (activePointerIdRef.current !== e.pointerId) return;
    
    // Check if this is a drag (moved more than 5px)
    if (dragStartPosRef.current) {
      const dx = e.clientX - dragStartPosRef.current.x;
      const dy = e.clientY - dragStartPosRef.current.y;
      if (Math.abs(dx) > 5 || Math.abs(dy) > 5) {
        isDraggingRef.current = true;
      }
    }

    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      // Touch/pen: update highlight based on element under pointer
      const element = getElementAtPoint(e.clientX, e.clientY);
      const target = getActionFromElement(element);
      
      if (target) {
        setHighlightedAction(target.deviceId ? `camera-${target.deviceId}` : target.action);
      } else {
        setHighlightedAction(null);
      }
    }
  }, [getElementAtPoint, getActionFromElement]);

  // Unified pointer up handler
  const handlePointerUp = useCallback((e: React.PointerEvent) => {
    if (activePointerIdRef.current !== e.pointerId) return;
    
    // Release pointer capture
    (e.target as Element).releasePointerCapture?.(e.pointerId);
    
    // Get the element under the pointer at release
    const element = getElementAtPoint(e.clientX, e.clientY);
    const target = getActionFromElement(element);
    
    if (pointerTypeRef.current === 'touch' || pointerTypeRef.current === 'pen') {
      const isTap = !isDraggingRef.current;
      const wasClosed = !wasExpandedOnTouchStartRef.current;

      if (isTap && wasClosed && target?.action === 'expand') {
        // Tap on expand button when selector was closed → open and keep open for tap-to-select
        setHighlightedAction(null);
        startAutoCollapseTimer();
      } else if (isTap && !wasClosed) {
        // Tap when selector was already open (from a previous tap) → execute action or close
        if (target && target.action !== 'expand') {
          executeAction(target.action, target.deviceId);
        } else {
          // Tapped expand again or empty area → close
          clearCollapseTimers();
          setIsExpanded(false);
        }
        setHighlightedAction(null);
      } else {
        // Hold-and-drag gesture: execute action on release if over a valid target
        if (target) {
          executeAction(target.action, target.deviceId);
        }
        setHighlightedAction(null);
        if (!target || target.action === 'expand') {
          setIsExpanded(false);
        }
      }
    } else {
      // Mouse: execute action on click (not drag)
      if (target && !isDraggingRef.current) {
        executeAction(target.action, target.deviceId);
      }
      setHighlightedAction(null);
    }
    
    // Reset pointer tracking state
    activePointerIdRef.current = null;
    isDraggingRef.current = false;
    dragStartPosRef.current = null;
  }, [getElementAtPoint, getActionFromElement, executeAction, clearCollapseTimers, startAutoCollapseTimer]);

  // Pointer cancel handler
  const handlePointerCancel = useCallback((e: React.PointerEvent) => {
    if (activePointerIdRef.current === e.pointerId) {
      setHighlightedAction(null);
      setIsExpanded(false);
      activePointerIdRef.current = null;
      isDraggingRef.current = false;
      dragStartPosRef.current = null;
    }
  }, []);

  // Mouse enter/leave for hover expansion (mouse only)
  const handleMouseEnter = useCallback(() => {
    if (pointerTypeRef.current !== 'touch' && pointerTypeRef.current !== 'pen') {
      clearCollapseTimers();
      setIsExpanded(true);
    }
  }, [clearCollapseTimers]);

  const handleMouseLeave = useCallback(() => {
    if (pointerTypeRef.current !== 'touch' && pointerTypeRef.current !== 'pen') {
      // Don't collapse immediately if flashing (selection feedback)
      if (!isFlashing) {
        startAutoCollapseTimer(300); // Short delay for mouse leave
      }
    }
  }, [isFlashing, startAutoCollapseTimer]);

  const allCameraOptions = useMemo(() => {
    // Start with "Camera Off" option
    const cameraOptions: CameraDevice[] = [
      { deviceId: CAMERA_OFF_ID, label: t('chat.camera.turnOff'), facingMode: 'unknown' }
    ];
    // Add available cameras
    cameraOptions.push(...availableCameras);
    // Add image gen camera if enabled
    if (imageGenerationModeEnabled) {
      cameraOptions.push({ deviceId: IMAGE_GEN_CAMERA_ID, label: t('chat.camera.imageGenCameraLabel'), facingMode: 'unknown' });
    }
    return cameraOptions;
  }, [availableCameras, imageGenerationModeEnabled, t]);

  // Get the current camera icon based on selection and facing mode
  const CurrentCameraIcon = useMemo(() => {
    if (isImageGenCameraSelected) return IconSparkles;
    if (currentCameraFacingMode === 'user') return IconCameraFront;
    return IconCamera;
  }, [isImageGenCameraSelected, currentCameraFacingMode]);

  // Selector colors
  const selectorColors = isSuggestionMode
    ? { bg: 'bg-gray-200/80', activeBg: 'bg-white', activeText: 'text-gray-800', inactiveText: 'text-gray-500', hoverBg: 'hover:bg-gray-300/80' }
    : { bg: 'bg-blue-500/40', activeBg: 'bg-white', activeText: 'text-blue-600', inactiveText: 'text-blue-200', hoverBg: 'hover:bg-blue-400/60' };

  // Check if a specific action is highlighted (for visual feedback during touch drag)
  const isHighlighted = useCallback((action: string, deviceId?: string) => {
    if (!highlightedAction) return false;
    if (deviceId) return highlightedAction === `camera-${deviceId}`;
    return highlightedAction === action;
  }, [highlightedAction]);

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

          {/* Camera toggle cluster - unified pointer event handling */}
          <div
            className={`relative flex items-center transition-all duration-200 ${isFlashing ? 'brightness-125 scale-105' : ''}`}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerCancel={handlePointerCancel}
            onMouseEnter={handleMouseEnter}
            onMouseLeave={handleMouseLeave}
            style={{ touchAction: 'none' }} // Prevent browser handling of touch gestures
          >
            {/* Book Toggle (Left Wing) */}
            <button
              type="button"
              {...{ [DATA_ACTION]: 'book-toggle' }}
              className={`absolute right-full top-1/2 -translate-y-1/2 p-2 rounded-full flex items-center justify-center shadow-md z-0 transition-all duration-200 ${
                isExpanded 
                  ? 'opacity-100 -translate-x-0 pointer-events-auto' 
                  : 'opacity-0 translate-x-6 scale-75 pointer-events-none'
              } ${
                imageGenerationModeEnabled
                  ? `${selectorColors.activeBg} text-purple-600`
                  : `${selectorColors.bg} ${selectorColors.inactiveText}`
              } ${
                isHighlighted('book-toggle') ? 'scale-125 z-20 ring-2 ring-purple-400' : 'hover:scale-110'
              }`}
              title={t('chat.bookIcon.toggleImageGen')}
            >
              <IconBookOpen className="w-5 h-5" />
            </button>

            {/* Central Camera Icon - click/tap to expand selector */}
            <button
              type="button"
              {...{ [DATA_ACTION]: 'expand' }}
              className={`p-2 cursor-pointer rounded-full transition-colors relative z-10 ${iconButtonStyle} ${
                isHighlighted('expand') ? 'scale-110' : ''
              }`}
              title={t('chat.camera.selectCamera')}
            >
              {isCameraActive ? (
                /* Camera ON: show current camera icon */
                <CurrentCameraIcon className={`w-5 h-5 ${isImageGenCameraSelected ? 'text-purple-400' : ''}`} />
              ) : (
                /* Camera OFF: show camera with X overlay */
                <IconCameraOff className="w-5 h-5" />
              )}
            </button>

            {/* Right Wing Container - camera options */}
            {allCameraOptions.length > 0 && (
              <div
                className={`absolute left-full top-1/2 -translate-y-1/2 flex items-center transition-all duration-200 ${
                  isExpanded 
                    ? 'opacity-100 translate-x-1 pointer-events-auto' 
                    : 'opacity-0 -translate-x-6 pointer-events-none'
                }`}
              >
                {/* Camera Options */}
                {allCameraOptions.map((cam, idx) => {
                  const isCameraOffOption = cam.deviceId === CAMERA_OFF_ID;
                  // Camera off shows selected when camera is inactive
                  // Other cameras only show selected when camera IS active AND their ID matches
                  const isSelected = isCameraOffOption
                    ? !isCameraActive
                    : (isCameraActive && cam.deviceId === selectedCameraId);
                  
                  let Icon;
                  if (isCameraOffOption) Icon = IconCameraOff;
                  else if (cam.deviceId === IMAGE_GEN_CAMERA_ID) Icon = IconSparkles;
                  else if (cam.facingMode === 'user') Icon = IconCameraFront;
                  else Icon = IconCamera;

                  const isImageGen = cam.deviceId === IMAGE_GEN_CAMERA_ID;
                  const isButtonHighlighted = isHighlighted('camera-select', cam.deviceId);

                  return (
                    <button
                      type="button"
                      key={cam.deviceId}
                      {...{ [DATA_ACTION]: 'camera-select', [DATA_DEVICE_ID]: cam.deviceId }}
                      className={`p-2 -ml-2 rounded-full flex items-center justify-center shadow-md transition-all duration-200 ${
                        isSelected
                          ? `${selectorColors.activeBg} ${isImageGen ? 'text-purple-600' : selectorColors.activeText}`
                          : `${selectorColors.bg} ${isImageGen ? 'text-purple-300' : selectorColors.inactiveText}`
                      } ${
                        isButtonHighlighted 
                          ? 'scale-125 z-30 ring-2 ring-white/50' 
                          : 'hover:scale-110 hover:z-20'
                      }`}
                      title={cam.label}
                      style={{ zIndex: isButtonHighlighted ? 30 : allCameraOptions.length - idx }}
                    >
                      <Icon className="w-5 h-5" />
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export default CameraControls;
