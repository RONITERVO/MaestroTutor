// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import { useArtifactLoadingSceneSlot } from '../embeds/useArtifactLoadingSceneSlot';

const artifactLoadingSceneSrc = `${import.meta.env.BASE_URL}artifact-loading-scene.svg`;

/**
 * The decorative "artifact is being built" animation.
 *
 * The scene animates inside a `<foreignObject>`, so it needs a real browsing
 * context — an `<img>` will not run it. What it does not need is one context
 * per in-flight artifact: that was a whole document and compositing layer per
 * loading message, purely for decoration, at exactly the moment the app is
 * already busy generating something. Only the first claimant gets the iframe;
 * concurrent loads fall back to the ordinary spinner.
 */
const ArtifactLoadingScene: React.FC = () => {
  const ownsScene = useArtifactLoadingSceneSlot(true);

  if (!ownsScene) {
    return (
      <div className="absolute inset-0 flex items-center justify-center bg-transparent">
        <SmallSpinner className="w-8 h-8 text-deep-ink/70" />
      </div>
    );
  }

  return (
    <iframe
      title="Interactive artifact loading scene"
      src={artifactLoadingSceneSrc}
      className="absolute inset-0 block h-full w-full border-0 bg-transparent"
      sandbox="allow-scripts allow-same-origin"
      referrerPolicy="no-referrer"
      loading="lazy"
      style={{ backgroundColor: 'transparent' }}
    />
  );
};

export default React.memo(ArtifactLoadingScene);
