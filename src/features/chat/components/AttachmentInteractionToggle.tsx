// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { IconHandRaised, IconReturnToChatScroll } from '../../../shared/ui/Icons';

interface AttachmentInteractionToggleProps {
  isAttachmentModeEnabled: boolean;
  attachmentLabel: string;
  attachmentTitle: string;
  onToggle: () => void;
  groupLabel?: string;
  chatLabel?: string;
  chatTitle?: string;
  activeSurfaceClassName?: string;
  inactiveSurfaceClassName?: string;
  activeTextClassName?: string;
  inactiveTextClassName?: string;
}

const AttachmentInteractionToggle: React.FC<AttachmentInteractionToggleProps> = ({
  isAttachmentModeEnabled,
  attachmentLabel,
  attachmentTitle,
  onToggle,
  groupLabel = 'Attachment interaction mode',
  chatLabel = 'Chat scroll',
  chatTitle = 'Use chat scroll',
  activeSurfaceClassName = 'bg-paper-surface/90',
  inactiveSurfaceClassName = 'bg-paper-stripe/55',
  activeTextClassName = 'text-deep-ink',
  inactiveTextClassName = 'text-sketch-line',
}) => {
  const modes = [
    {
      enabled: false,
      label: chatLabel,
      Icon: IconReturnToChatScroll,
      shapeClass: 'sketch-shape-2',
    },
    {
      enabled: true,
      label: attachmentLabel,
      Icon: IconHandRaised,
      shapeClass: 'sketch-shape-3',
    },
  ];
  const actionLabel = isAttachmentModeEnabled ? chatTitle : attachmentTitle;

  return (
    <div className="relative h-7 w-[90px] shrink-0 select-none" role="group" aria-label={groupLabel}>
      {modes.map(({ enabled, label, Icon, shapeClass }) => {
        const isActive = isAttachmentModeEnabled === enabled;
        const sizeClass = isActive ? 'h-6 w-[74px]' : 'h-[22px] w-[46px]';
        const positionClass = isActive
          ? 'left-0 top-0 z-20 -rotate-2 scale-100'
          : 'right-0 bottom-0 z-10 rotate-6 scale-95';
        const toneClass = isActive
          ? `${activeSurfaceClassName} ${activeTextClassName}`
          : `${inactiveSurfaceClassName} ${inactiveTextClassName}`;
        const contentClass = isActive ? 'justify-start pl-2 pr-1' : 'justify-end px-1.5';

        return (
          <button
            key={enabled ? 'attachment' : 'chat'}
            type="button"
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              onToggle();
            }}
            className={`absolute ${sizeClass} ${positionClass} ${shapeClass} ${toneClass} border border-sketch-line/45 paper-texture isolate overflow-hidden transition-all duration-300 ease-out focus:outline-none focus:ring-2 focus:ring-mode-toggle-text/30 active:scale-95 btn-depth`}
            title={actionLabel}
            aria-label={actionLabel}
            aria-pressed={isActive}
          >
            <span className={`relative z-10 flex h-full w-full items-center gap-1 ${contentClass}`} aria-hidden="true">
              <Icon className="h-3.5 w-3.5 shrink-0" />
              {isActive && (
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
