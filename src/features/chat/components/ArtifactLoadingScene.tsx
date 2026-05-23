// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';

const artifactLoadingSceneSrc = `${import.meta.env.BASE_URL}artifact-loading-scene.svg`;

const ArtifactLoadingScene: React.FC = () => (
  <iframe
    title="Interactive artifact loading scene"
    src={artifactLoadingSceneSrc}
    className="absolute inset-0 block h-full w-full border-0 bg-transparent"
    sandbox="allow-scripts allow-same-origin"
    referrerPolicy="no-referrer"
    style={{ backgroundColor: 'transparent' }}
  />
);

export default React.memo(ArtifactLoadingScene);
