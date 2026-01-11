
import { createLowResImageFromDataUrl, createLowFpsVideoFromDataUrl } from '../utils/mediaUtils';

export async function processMediaForUpload(
    dataUrl: string,
    mimeType: string,
    shouldOptimize: boolean,
    callbacks?: {
        onProgress?: (label: string, done?: number, total?: number, etaMs?: number) => void;
        t?: (key: string) => string; 
    }
): Promise<{ dataUrl: string; mimeType: string; isOptimized: boolean }> {
    if (!shouldOptimize) {
        return { dataUrl, mimeType, isOptimized: false };
    }

    const txtOptimizingImage = callbacks?.t ? callbacks.t('chat.sendPrep.optimizingImage') : 'Optimizing image…';
    const txtOptimizingVideo = callbacks?.t ? callbacks.t('chat.sendPrep.optimizingVideo') : 'Optimizing video…';

    try {
        if (mimeType.startsWith('image/')) {
            if (callbacks?.onProgress) callbacks.onProgress(txtOptimizingImage);
            const res = await createLowResImageFromDataUrl(dataUrl, { maxDim: 768, quality: 0.6, outputMime: 'image/jpeg' });
            return { dataUrl: res.dataUrl, mimeType: res.mimeType, isOptimized: true };
        } else if (mimeType.startsWith('video/')) {
            const res = await createLowFpsVideoFromDataUrl(dataUrl, {
                fps: 1,
                maxWidth: 1920,
                mimeType: 'video/webm',
                onProgress: (elapsedMs, durationMs, etaMs) => {
                    if (callbacks?.onProgress) {
                        const elapsedSec = Math.floor(elapsedMs / 1000);
                        const totalSec = Math.max(elapsedSec, Math.floor((durationMs || 0) / 1000));
                        callbacks.onProgress(txtOptimizingVideo, elapsedSec, totalSec, etaMs);
                    }
                }
            });
            return { dataUrl: res.dataUrl, mimeType: res.mimeType, isOptimized: true };
        }
    } catch (e) {
        console.warn('Media optimization failed, falling back to original', e);
    }

    return { dataUrl, mimeType, isOptimized: false };
}
