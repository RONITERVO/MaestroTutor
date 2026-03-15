// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';

interface AttachmentTextScrollContainerProps extends React.HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode;
  ariaLabel?: string;
  spacerClassName?: string;
}

const baseScrollStyle: React.CSSProperties = {
  WebkitMaskImage:
    'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
  maskImage:
    'linear-gradient(to top, rgba(0,0,0,0.1) 0%, rgba(0,0,0,1) 40%, rgba(0,0,0,1) 60%, rgba(0,0,0,0.7) 65%, rgba(0,0,0,0) 75%, rgba(0,0,0,0) 100%)',
  clipPath: 'inset(25% 0 0 0)',
  height: '33cqw',
  // @ts-ignore
  containerType: 'inline-size',
};

const AttachmentTextScrollContainer = React.forwardRef<HTMLDivElement, AttachmentTextScrollContainerProps>(({
  children,
  className = '',
  style,
  ariaLabel,
  spacerClassName = 'text-attachment-overlay-native-text/40',
  ...rest
}, ref) => (
  <div
    ref={ref}
    className={`overflow-y-auto relative scrollbar-hide pointer-events-none ${className}`.trim()}
    style={{ ...baseScrollStyle, ...style }}
    aria-label={ariaLabel}
    {...rest}
  >
    <div
      className="flex flex-col items-center justify-start"
      style={{
        paddingTop: '8cqw',
        paddingBottom: '8cqw',
      }}
    >
      <div
        aria-hidden
        role="presentation"
        className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
      >
        <p
          className={`italic ${spacerClassName}`}
          style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
        >
          {'\u00A0'}
        </p>
      </div>
      {children}
      <div
        aria-hidden
        role="presentation"
        className="text-center p-1 w-full opacity-0 select-none pointer-events-none"
      >
        <p
          className={`italic ${spacerClassName}`}
          style={{ fontSize: '3.55cqw', lineHeight: 1.3 }}
        >
          {'\u00A0'}
        </p>
      </div>
    </div>
  </div>
));

AttachmentTextScrollContainer.displayName = 'AttachmentTextScrollContainer';

export default AttachmentTextScrollContainer;
