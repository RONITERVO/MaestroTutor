import React from 'react';

interface VisualContextVideoProps {
  videoRef: React.RefObject<HTMLVideoElement | null>;
}

const VisualContextVideo: React.FC<VisualContextVideoProps> = ({ videoRef }) => (
  <video ref={videoRef} playsInline muted className="hidden w-px h-px" aria-hidden="true" />
);

export default VisualContextVideo;
