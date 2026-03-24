import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';

interface AudioPlayerProps {
  src?: string | null;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  statusText?: string | null;
  waveformSeed?: string;
  placeholderProgress?: number;
}

const BAR_COUNT = 32;
const BAR_COUNT_COMPACT = 24;

/** Fallback: derive a stable pseudo-random waveform from the src string */
function generateFallbackWaveform(seed: string, count: number): number[] {
  let h = 0;
  for (let i = 0; i < Math.min(seed.length, 200); i++) {
    h = ((h << 5) - h + seed.charCodeAt(i)) | 0;
  }
  const bars: number[] = [];
  for (let i = 0; i < count; i++) {
    h = ((h << 13) ^ h) | 0;
    h = (h * 1597334677) | 0;
    const v = ((h >>> 0) % 100) / 100;
    const center = count / 2;
    const dist = Math.abs(i - center) / center;
    const envelope = 1 - dist * 0.5;
    bars.push(0.15 + v * 0.85 * envelope);
  }
  return bars;
}

/** Decode audio src into real amplitude bars via Web Audio API */
async function decodeWaveform(src: string, barCount: number): Promise<number[]> {
  const response = await fetch(src);
  const arrayBuf = await response.arrayBuffer();
  const ctx = new (window.AudioContext || (window as any).webkitAudioContext)();
  try {
    const audioBuffer = await ctx.decodeAudioData(arrayBuf);
    const channel = audioBuffer.getChannelData(0);
    const samplesPerBar = Math.floor(channel.length / barCount);
    const bars: number[] = [];
    for (let i = 0; i < barCount; i++) {
      let sum = 0;
      const start = i * samplesPerBar;
      const end = Math.min(start + samplesPerBar, channel.length);
      for (let j = start; j < end; j++) {
        sum += Math.abs(channel[j]);
      }
      bars.push(sum / (end - start));
    }
    // Normalize to 0.1-1.0 range
    const max = Math.max(...bars) || 1;
    return bars.map(v => 0.1 + (v / max) * 0.9);
  } finally {
    ctx.close();
  }
}

function formatTime(sec: number): string {
  if (!isFinite(sec) || sec < 0) return '0:00';
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

const AudioPlayer: React.FC<AudioPlayerProps> = React.memo(({
  src,
  variant,
  compact = false,
  statusText = null,
  waveformSeed,
  placeholderProgress = 0,
}) => {
  const { t } = useAppTranslations();
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [isLoaded, setIsLoaded] = useState(false);
  const animFrameRef = useRef<number>(0);
  const waveContainerRef = useRef<HTMLDivElement>(null);
  const resolvedSrc = typeof src === 'string' && src.trim() ? src : null;
  const isPlaceholder = !resolvedSrc;

  const barCount = compact ? BAR_COUNT_COMPACT : BAR_COUNT;
  const fallbackSeed = waveformSeed || resolvedSrc || `${variant}-${compact ? 'compact' : 'full'}-audio-placeholder`;
  const fallbackWaveform = useMemo(() => generateFallbackWaveform(fallbackSeed, barCount), [fallbackSeed, barCount]);
  const [waveform, setWaveform] = useState<number[]>(fallbackWaveform);

  useEffect(() => {
    setWaveform(fallbackWaveform);
  }, [fallbackWaveform]);

  // Decode real waveform from audio data
  useEffect(() => {
    if (!resolvedSrc) return;
    let cancelled = false;
    decodeWaveform(resolvedSrc, barCount).then(bars => {
      if (!cancelled) setWaveform(bars);
    }).catch(() => {
      // keep fallback
    });
    return () => { cancelled = true; };
  }, [resolvedSrc, barCount]);

  const progress = resolvedSrc && duration > 0
    ? currentTime / duration
    : Math.max(0, Math.min(1, placeholderProgress));

  // Variant is kept for API stability across caller sites.
  void variant;

  const updateTime = useCallback(() => {
    const audio = audioRef.current;
    if (audio) {
      setCurrentTime(audio.currentTime);
      if (!audio.paused) {
        animFrameRef.current = requestAnimationFrame(updateTime);
      }
    }
  }, []);

  useEffect(() => {
    if (!resolvedSrc) {
      setIsPlaying(false);
      setCurrentTime(0);
      setDuration(0);
      setIsLoaded(false);
      return;
    }

    const audio = audioRef.current;
    if (!audio) return;

    let durationResolved = false;

    const tryResolveDuration = () => {
      const d = audio.duration;
      if (isFinite(d) && d > 0) {
        if (!durationResolved) {
          durationResolved = true;
          setDuration(d);
          setIsLoaded(true);
        }
      }
    };

    // For blob/data URLs, duration is often Infinity on loadedmetadata.
    // Force the browser to resolve it by seeking to a huge value then back.
    const forceDurationResolve = () => {
      if (isFinite(audio.duration) && audio.duration > 0) {
        tryResolveDuration();
        return;
      }
      const onSeeked = () => {
        audio.removeEventListener('seeked', onSeeked);
        tryResolveDuration();
        audio.currentTime = 0;
      };
      audio.addEventListener('seeked', onSeeked);
      audio.currentTime = 1e101;
    };

    const onLoaded = () => {
      if (isFinite(audio.duration) && audio.duration > 0) {
        tryResolveDuration();
      } else {
        forceDurationResolve();
      }
    };
    const onDurationChange = () => tryResolveDuration();
    const onPlay = () => {
      setIsPlaying(true);
      animFrameRef.current = requestAnimationFrame(updateTime);
    };
    const onPause = () => {
      setIsPlaying(false);
      cancelAnimationFrame(animFrameRef.current);
    };
    const onEnded = () => {
      setIsPlaying(false);
      setCurrentTime(0);
      cancelAnimationFrame(animFrameRef.current);
      // Duration may become available after full playback
      tryResolveDuration();
    };

    audio.addEventListener('loadedmetadata', onLoaded);
    audio.addEventListener('durationchange', onDurationChange);
    audio.addEventListener('play', onPlay);
    audio.addEventListener('pause', onPause);
    audio.addEventListener('ended', onEnded);

    // If already loaded (cached)
    if (audio.readyState >= 1) {
      onLoaded();
    }

    return () => {
      cancelAnimationFrame(animFrameRef.current);
      audio.removeEventListener('loadedmetadata', onLoaded);
      audio.removeEventListener('durationchange', onDurationChange);
      audio.removeEventListener('play', onPlay);
      audio.removeEventListener('pause', onPause);
      audio.removeEventListener('ended', onEnded);
    };
  }, [resolvedSrc, updateTime]);

  const togglePlay = useCallback(() => {
    if (!resolvedSrc) return;
    const audio = audioRef.current;
    if (!audio) return;
    if (audio.paused) {
      audio.play().catch(() => {});
    } else {
      audio.pause();
    }
  }, [resolvedSrc]);

  const handleWaveClick = useCallback((e: React.MouseEvent<HTMLDivElement> | React.TouchEvent<HTMLDivElement>) => {
    if (!resolvedSrc) return;
    const audio = audioRef.current;
    const container = waveContainerRef.current;
    if (!audio || !container || !duration) return;

    const rect = container.getBoundingClientRect();
    let clientX: number;
    if ('touches' in e) {
      clientX = e.touches[0]?.clientX ?? e.changedTouches[0]?.clientX ?? 0;
    } else {
      clientX = e.clientX;
    }
    const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
    audio.currentTime = ratio * duration;
    setCurrentTime(audio.currentTime);
  }, [duration, resolvedSrc]);

  // Keep audio controls visually consistent across all message variants.
  const containerClasses = 'bg-audio-player-bg';
  const playBtnClasses = 'bg-audio-play-btn hover:bg-audio-play-btn/80 text-audio-play-text';

  const barPlayedColor = 'bg-audio-bar';
  const barUnplayedColor = 'bg-audio-bar/30';
  const timeColor = 'text-audio-time-text';

  const btnSize = compact ? 'w-8 h-8' : 'w-10 h-10';
  const iconScale = compact ? 'w-4 h-4' : 'w-5 h-5';
  const barHeight = compact ? 'h-6' : 'h-8';
  const containerPad = compact ? 'p-1.5 gap-2' : 'p-2 gap-2.5';
  const currentTimeLabel = resolvedSrc ? formatTime(currentTime) : '0:00';
  const durationLabel = resolvedSrc ? (isLoaded ? formatTime(duration) : '--:--') : (statusText || '--:--');

  return (
    <div className={`flex items-center ${containerPad} ${containerClasses} w-full select-none sketchy-border-thin`}>
      {resolvedSrc ? <audio ref={audioRef} src={resolvedSrc} preload="metadata" /> : null}

      {/* Play / Pause */}
      <button
        type="button"
        onClick={togglePlay}
        className={`flex-shrink-0 ${btnSize} flex items-center justify-center transition-colors ${playBtnClasses} sketchy-border-thin ${isPlaceholder ? 'cursor-default' : ''}`}
        aria-label={isPlaceholder ? (statusText || t('audio.loading') || 'Audio loading') : (isPlaying ? t('audio.pause') || 'Pause' : t('audio.play') || 'Play')}
        aria-disabled={isPlaceholder}
      >
        {isPlaying ? (
          <svg className={iconScale} viewBox="0 0 20 20" fill="currentColor">
            <rect x="5" y="4" width="3.5" height="12" rx="1" />
            <rect x="11.5" y="4" width="3.5" height="12" rx="1" />
          </svg>
        ) : (
          <svg className={iconScale} viewBox="0 0 20 20" fill="currentColor">
            <path d="M6.5 4.2a1 1 0 0 1 1.02.04l8 5.5a1 1 0 0 1 0 1.66l-8 5.08A1 1 0 0 1 6 15.58V4.92a1 1 0 0 1 .5-.72Z" />
          </svg>
        )}
      </button>

      {/* Waveform + time */}
      <div className="flex-1 min-w-0 flex flex-col gap-1">
        <div
          ref={waveContainerRef}
          className={`flex items-end gap-[2px] ${barHeight} ${resolvedSrc ? 'cursor-pointer' : 'cursor-default'}`}
          onClick={handleWaveClick}
          role="slider"
          aria-label={isPlaceholder ? (statusText || t('audio.loading') || 'Audio loading') : t('audio.progress') || 'Audio progress'}
          aria-valuenow={Math.round(progress * 100)}
          aria-valuemin={0}
          aria-valuemax={100}
          tabIndex={resolvedSrc ? 0 : -1}
        >
          {waveform.map((h, i) => {
            const barProgress = i / barCount;
            const played = barProgress <= progress;
            return (
              <div
                key={i}
                className={`flex-1 rounded-full transition-colors duration-150 ${played ? barPlayedColor : barUnplayedColor} ${isPlaceholder ? 'animate-pulse' : ''}`}
                style={{
                  height: `${h * 100}%`,
                  minHeight: '3px',
                }}
              />
            );
          })}
        </div>

        <div className={`flex justify-between text-[10px] ${timeColor} font-medium leading-none`}>
          <span>{currentTimeLabel}</span>
          <span className={statusText && isPlaceholder ? 'truncate text-right max-w-[10rem]' : undefined} title={statusText || undefined}>
            {durationLabel}
          </span>
        </div>
      </div>
    </div>
  );
});

AudioPlayer.displayName = 'AudioPlayer';

export default AudioPlayer;

