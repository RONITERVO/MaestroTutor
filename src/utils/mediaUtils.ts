
export const getFacingModeFromLabel = (label: string): 'user' | 'environment' | 'unknown' => {
    const lowerLabel = label.toLowerCase();
    if (lowerLabel.includes('front') || lowerLabel.includes('user')) return 'user';
    if (lowerLabel.includes('back') || lowerLabel.includes('rear') || lowerLabel.includes('environment')) return 'environment';
    return 'unknown';
};

export const createLowResImageFromDataUrl = async (dataUrl: string, opts?: { maxDim?: number; quality?: number; outputMime?: string }): Promise<{ dataUrl: string; mimeType: string }> => {
    const { maxDim = 768, quality = 0.6, outputMime = 'image/jpeg' } = opts || {};
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error('Image load failed'));
      img.src = dataUrl;
    });
    const w = img.naturalWidth || 1;
    const h = img.naturalHeight || 1;
    const scale = Math.min(1, maxDim / Math.max(w, h));
    const outW = Math.max(1, Math.round(w * scale));
    const outH = Math.max(1, Math.round(h * scale));
    const canvas = document.createElement('canvas');
    canvas.width = outW; canvas.height = outH;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D context not available');
    ctx.drawImage(img, 0, 0, outW, outH);
    const out = canvas.toDataURL(outputMime, quality);
    return { dataUrl: out, mimeType: outputMime };
};

export const createLowFpsVideoFromDataUrl = async (dataUrl: string, opts?: { fps?: number; maxWidth?: number; mimeType?: string; onProgress?: (elapsedMs: number, durationMs: number, etaMs?: number) => void }): Promise<{ dataUrl: string; mimeType: string }> => {
    const { fps = 1, maxWidth = 1920, mimeType = 'video/webm;codecs=vp9,opus' } = opts || {};
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.muted = true;
    await new Promise<void>((resolve, reject) => {
      video.onloadedmetadata = () => resolve();
      video.onerror = () => reject(new Error('Video metadata load failed'));
      video.src = dataUrl;
    });
    const srcW = video.videoWidth || maxWidth;
    const srcH = video.videoHeight || maxWidth;
    const targetW = Math.min(maxWidth, srcW);
    const scale = srcW > 0 ? targetW / srcW : 1;
    const targetH = Math.max(1, Math.round(srcH * scale));

    const canvas = document.createElement('canvas');
    canvas.width = targetW; canvas.height = targetH;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D context not available for video');

    const durationMs = (Number.isFinite(video.duration) && video.duration > 0)
      ? Math.round(video.duration * 1000)
      : NaN;
    try {
      if (opts?.onProgress) {
        opts.onProgress(0, Number.isFinite(durationMs) ? durationMs : 0, Number.isFinite(durationMs) ? durationMs : undefined);
      }
    } catch {}

    const canvasStream = canvas.captureStream(Math.max(1, fps));
    let mixedStream: MediaStream = canvasStream;
    try {
      let audioTracks: MediaStreamTrack[] = [];
      if ((video as any).captureStream) {
        const elStream: MediaStream = (video as any).captureStream();
        audioTracks = elStream.getAudioTracks();
      }
      if (!audioTracks.length) {
        const AudioCtx = (window as any).AudioContext || (window as any).webkitAudioContext;
        const audioCtx = new AudioCtx();
        try { await audioCtx.resume(); } catch {}
        const sourceNode = audioCtx.createMediaElementSource(video);
        const dest = audioCtx.createMediaStreamDestination();
        sourceNode.connect(dest);
        audioTracks = dest.stream.getAudioTracks();
      }
      mixedStream = new MediaStream([
        ...canvasStream.getVideoTracks(),
        ...audioTracks,
      ]);
    } catch {
      mixedStream = canvasStream; 
    }

    const recorder = new MediaRecorder(mixedStream, { mimeType: mimeType as any });
    const chunks: BlobPart[] = [];
    recorder.ondataavailable = (e) => { if (e.data && e.data.size) chunks.push(e.data); };
    const stopPromise = new Promise<string>((resolve) => {
      recorder.onstop = () => {
        const blob = new Blob(chunks, { type: mimeType });
        const reader = new FileReader();
        reader.onloadend = () => resolve(reader.result as string);
        reader.readAsDataURL(blob);
      };
    });

    await video.play().catch(() => {});
    recorder.start();
    const frameIntervalMs = Math.max(250, Math.round(1000 / Math.max(1, fps)));
    const interval = window.setInterval(() => {
      try { ctx.drawImage(video, 0, 0, targetW, targetH); } catch {}
    }, frameIntervalMs);
    const progressIv = window.setInterval(() => {
      try {
        const currentMs = Math.max(0, Math.round((video.currentTime || 0) * 1000));
        const totalMs = Number.isFinite(durationMs) ? (durationMs as number) : (currentMs || 0);
        const eta = Number.isFinite(durationMs) ? Math.max(0, (durationMs as number) - currentMs) : undefined;
        opts?.onProgress?.(currentMs, totalMs, eta);
      } catch {}
    }, Math.min(1000, frameIntervalMs));
    await new Promise<void>((resolve) => { video.onended = () => resolve(); video.onerror = () => resolve(); });
    window.clearInterval(interval);
    window.clearInterval(progressIv);
    recorder.stop();
    const out = await stopPromise;
    return { dataUrl: out, mimeType };
};

export const createKeyframeFromVideoDataUrl = async (
    dataUrl: string,
    opts?: { at?: 'start' | 'middle' | 'end'; maxDim?: number; quality?: number; outputMime?: string; startOffsetSec?: number; endOffsetSec?: number }
  ): Promise<{ dataUrl: string; mimeType: string }> => {
    const {
      at = 'start',
      maxDim = 768,
      quality = 0.7,
      outputMime = 'image/jpeg',
      startOffsetSec = 0.12, 
      endOffsetSec = 0.05,
    } = opts || {};
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.muted = true;
    await new Promise<void>((resolve, reject) => {
      video.onloadedmetadata = () => resolve();
      video.onerror = () => reject(new Error('Video metadata load failed'));
      video.src = dataUrl;
    });
    const duration = Number.isFinite(video.duration) ? video.duration : 0;
    const epsilon = 0.001;
    const clampTime = (t: number) => Math.max(0, duration > 0 ? Math.min(t, Math.max(0, duration - epsilon)) : 0);
    const calcTime = (): number => {
      if (at === 'middle') return duration > 0 ? duration * 0.5 : 0;
      if (at === 'end') return duration > 0 ? clampTime(duration - endOffsetSec) : 0;
      return clampTime(startOffsetSec);
    };
    const seekTo = async (t: number) => {
      try {
        await new Promise<void>((resolve) => {
          const handler = () => { video.removeEventListener('seeked', handler); resolve(); };
          video.addEventListener('seeked', handler);
          try { video.currentTime = t; } catch {
            video.play()
              .then(() => { video.pause(); try { video.currentTime = t; } catch {} })
              .catch(() => { resolve(); });
          }
        });
      } catch {}
    };

    await seekTo(calcTime());
    const srcW = video.videoWidth || 1;
    const srcH = video.videoHeight || 1;
    const scale = Math.min(1, maxDim / Math.max(srcW, srcH));
    const outW = Math.max(1, Math.round(srcW * scale));
    const outH = Math.max(1, Math.round(srcH * scale));
    const canvas = document.createElement('canvas');
    canvas.width = outW; canvas.height = outH;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D context not available for keyframe');
    try { ctx.drawImage(video, 0, 0, outW, outH); } catch {}
    if (at === 'start') {
      try {
        const imgData = ctx.getImageData(0, 0, Math.min(32, outW), Math.min(32, outH));
        let sum = 0;
        const data = imgData.data;
        for (let i = 0; i < data.length; i += 16 * 4) {
          const r = data[i], g = data[i + 1], b = data[i + 2];
          sum += (0.2126 * r + 0.7152 * g + 0.0722 * b);
        }
        const samples = Math.ceil(data.length / (16 * 4));
        const avg = samples > 0 ? sum / samples : 255;
        if (avg < 8 && duration > 0) {
          const retries = [clampTime(startOffsetSec + 0.15), clampTime(startOffsetSec + 0.3)];
          for (const t of retries) {
            await seekTo(t);
            try { ctx.drawImage(video, 0, 0, outW, outH); } catch {}
            const id = ctx.getImageData(0, 0, Math.min(32, outW), Math.min(32, outH));
            let s2 = 0; const d2 = id.data;
            for (let i = 0; i < d2.length; i += 16 * 4) {
              const r = d2[i], g = d2[i + 1], b = d2[i + 2];
              s2 += (0.2126 * r + 0.7152 * g + 0.0722 * b);
            }
            const n2 = Math.ceil(d2.length / (16 * 4));
            const avg2 = n2 > 0 ? s2 / n2 : 255;
            if (avg2 >= 8) break; 
          }
        }
      } catch {}
    }
    const frameDataUrl = canvas.toDataURL(outputMime, quality);
    return { dataUrl: frameDataUrl, mimeType: outputMime };
};
