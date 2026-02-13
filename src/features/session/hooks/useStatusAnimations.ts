// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useEffect, useRef, useCallback } from 'react';
import { MaestroActivityStage } from '../../../core/types';
import { useMaestroStore } from '../../../store';

interface AnimationManifest {
  version: number;
  animations: Array<{ file: string; stages: MaestroActivityStage[] }>;
}

type StageAnimationMap = Map<MaestroActivityStage, string[]>;

export const useStatusAnimations = () => {
  const stageMapRef = useRef<StageAnimationMap>(new Map());
  const [currentAnimation, setCurrentAnimation] = useState<string | null>(null);
  const [isVisible, setIsVisible] = useState(false);
  const maestroActivityStage = useMaestroStore(state => state.maestroActivityStage);
  const prevStageRef = useRef<MaestroActivityStage>(maestroActivityStage);

  // Load manifest once
  useEffect(() => {
    (async () => {
      try {
        const resp = await fetch('/animations/manifest.json', { cache: 'force-cache' });
        if (!resp.ok) return;
        const data: AnimationManifest = await resp.json();
        const map: StageAnimationMap = new Map();
        for (const entry of data.animations) {
          for (const stage of entry.stages) {
            if (!map.has(stage)) map.set(stage, []);
            map.get(stage)!.push(entry.file);
          }
        }
        stageMapRef.current = map;
      } catch { /* silent fail - animations are non-critical */ }
    })();
  }, []);

  // React to stage changes
  useEffect(() => {
    if (maestroActivityStage === prevStageRef.current) return;
    prevStageRef.current = maestroActivityStage;

    const pool = stageMapRef.current.get(maestroActivityStage);
    if (!pool || pool.length === 0) return;

    const randomIndex = Math.floor(Math.random() * pool.length);
    setCurrentAnimation('/animations/' + pool[randomIndex]);
    setIsVisible(true);
  }, [maestroActivityStage]);

  const handleAnimationEnded = useCallback(() => {
    setIsVisible(false);
  }, []);

  const handleTransitionEnd = useCallback(() => {
    if (!isVisible) {
      setCurrentAnimation(null);
    }
  }, [isVisible]);

  return { currentAnimation, isVisible, handleAnimationEnded, handleTransitionEnd };
};
