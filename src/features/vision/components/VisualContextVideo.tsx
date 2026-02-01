// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';

interface VisualContextVideoProps {
  videoRef: React.RefObject<HTMLVideoElement | null>;
}

const VisualContextVideo: React.FC<VisualContextVideoProps> = ({ videoRef }) => (
  <video ref={videoRef} playsInline muted className="hidden w-px h-px" />
);

export default VisualContextVideo;
