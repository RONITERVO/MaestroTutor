// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useRef, useEffect } from 'react';

interface StatusAnimationBubbleProps {
  currentAnimation: string | null;
  isVisible: boolean;
  onEnded: () => void;
  onTransitionEnd: () => void;
}

const StatusAnimationBubble: React.FC<StatusAnimationBubbleProps> = ({
  currentAnimation,
  isVisible,
  onEnded,
  onTransitionEnd,
}) => {
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    if (currentAnimation && videoRef.current) {
      videoRef.current.load();
      videoRef.current.play().catch(() => {});
    }
  }, [currentAnimation]);

  if (!currentAnimation) return null;

  return (
    <div
      className={`transition-opacity duration-300 ${isVisible ? 'opacity-100' : 'opacity-0'}`}
      onTransitionEnd={onTransitionEnd}
      style={{
        width: 34,
        height: 34,
        borderRadius: '50%',
        overflow: 'hidden',
      }}
    >
      <video
        ref={videoRef}
        src={currentAnimation}
        autoPlay
        muted
        playsInline
        onEnded={onEnded}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'cover',
          display: 'block',
        }}
      />
    </div>
  );
};

export default StatusAnimationBubble;
