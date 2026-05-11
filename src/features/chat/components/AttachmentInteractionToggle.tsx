// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { IconHandRaised, IconReturnToChatScroll } from '../../../shared/ui/Icons';

interface AttachmentInteractionToggleProps {
  isAttachmentModeEnabled: boolean;
  attachmentLabel: string;
  attachmentTitle: string;
  onToggle?: () => void;
  onSelectMode?: (enabled: boolean, event: React.MouseEvent<HTMLButtonElement>) => void;
  groupLabel?: string;
  chatLabel?: string;
  chatTitle?: string;
  compact?: boolean;
  isAttachmentModeAvailable?: boolean;
  attachmentUnavailableTitle?: string;
  AttachmentIcon?: React.ComponentType<{ className?: string }>;
  ChatIcon?: React.ComponentType<{ className?: string }>;
  activeSurfaceClassName?: string;
  inactiveSurfaceClassName?: string;
  activeTextClassName?: string;
  inactiveTextClassName?: string;
  borderClassName?: string;
}

const AttachmentInteractionToggle: React.FC<AttachmentInteractionToggleProps> = ({
  isAttachmentModeEnabled,
  attachmentLabel,
  attachmentTitle,
  onToggle,
  onSelectMode,
  groupLabel = 'Attachment interaction mode',
  chatLabel = 'Chat scroll',
  chatTitle = 'Use chat scroll',
  compact = false,
  isAttachmentModeAvailable = true,
  attachmentUnavailableTitle,
  AttachmentIcon = IconHandRaised,
  ChatIcon = IconReturnToChatScroll,
  activeSurfaceClassName = 'bg-paper-surface/90',
  inactiveSurfaceClassName = 'bg-paper-stripe/55',
  activeTextClassName = 'text-deep-ink',
  inactiveTextClassName = 'text-sketch-line',
  borderClassName = 'border-sketch-line/45',
}) => {
  const modes = [
    {
      enabled: false,
      label: chatLabel,
      Icon: ChatIcon,
      shapeClass: 'sketch-shape-2',
    },
    {
      enabled: true,
      label: attachmentLabel,
      Icon: AttachmentIcon,
      shapeClass: 'sketch-shape-3',
    },
  ];
  const actionLabel = isAttachmentModeEnabled
    ? chatTitle
    : (isAttachmentModeAvailable ? attachmentTitle : attachmentUnavailableTitle || attachmentTitle);

  return (
    <div
      className={`relative shrink-0 select-none ${compact ? 'h-10 w-10' : 'h-7 w-[90px]'}`}
      role="group"
      aria-label={groupLabel}
    >
      {modes.map(({ enabled, label, Icon, shapeClass }) => {
        const isActive = isAttachmentModeEnabled === enabled;
        const isUnavailable = enabled && !isAttachmentModeAvailable && !isAttachmentModeEnabled;
        const sizeClass = compact
          ? (isActive ? 'h-8 w-8' : 'h-7 w-7')
          : (isActive ? 'h-6 w-[74px]' : 'h-[22px] w-[46px]');
        const positionClass = compact
          ? (isActive
              ? 'left-0 top-0 z-20 -rotate-3 scale-100'
              : 'right-0 bottom-0 z-10 rotate-6 scale-95')
          : (isActive
              ? 'left-0 top-0 z-20 -rotate-2 scale-100'
              : 'right-0 bottom-0 z-10 rotate-6 scale-95');
        const toneClass = isActive
          ? `${activeSurfaceClassName} ${activeTextClassName}`
          : `${inactiveSurfaceClassName} ${inactiveTextClassName}`;
        const contentClass = compact
          ? (isActive ? 'justify-center px-1' : 'justify-end pl-0 pr-0.5')
          : (isActive ? 'justify-start pl-2 pr-1' : 'justify-end px-1.5');

        return (
          <button
            key={enabled ? 'attachment' : 'chat'}
            type="button"
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              if (onSelectMode) {
                onSelectMode(!isAttachmentModeEnabled, event);
                return;
              }
              onToggle?.();
            }}
            className={`absolute ${sizeClass} ${positionClass} ${shapeClass} ${toneClass} ${borderClassName} border paper-texture isolate overflow-hidden transition-all duration-300 ease-out focus:outline-none focus:ring-2 focus:ring-mode-toggle-text/30 active:scale-95 disabled:cursor-not-allowed disabled:opacity-45 ${compact ? 'shadow-[0_10px_22px_rgba(2,6,23,0.28)] backdrop-blur-sm' : 'btn-depth'}`}
            title={actionLabel}
            aria-label={actionLabel}
            aria-pressed={isActive}
            disabled={isUnavailable}
          >
            <span
              className={`relative z-10 flex h-full w-full items-center ${compact ? 'gap-0' : 'gap-1'} ${contentClass}`}
              aria-hidden="true"
            >
              <Icon className={`${compact ? 'h-5 w-5' : 'h-3.5 w-3.5'} shrink-0`} />
              {!compact && isActive && (
                <span className="max-w-[42px] truncate text-[9px] font-semibold uppercase tracking-wide">
                  {label}
                </span>
              )}
            </span>
          </button>
        );
      })}
    </div>
  );
};

export default AttachmentInteractionToggle;
