import React, { useEffect, useMemo, useState } from 'react';
import { IconCheck, IconChevronLeft, IconChevronRight, IconQuestionMarkCircle, IconShield, IconXMark } from '../../../shared/ui/Icons';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { openExternalUrl } from '../../../shared/utils/openExternalUrl';

interface ApiKeyGateProps {
  isOpen: boolean;
  isBlocking: boolean;
  hasKey: boolean;
  maskedKey?: string | null;
  isSaving?: boolean;
  error?: string | null;
  onSave: (value: string) => Promise<boolean>;
  onClear: () => Promise<void>;
  onClose: () => void;
  onValueChange?: (value: string) => void;
}

const AI_STUDIO_URL = 'https://aistudio.google.com/app/apikey';
const PRIVACY_POLICY_URL = 'https://ronitervo.github.io/MaestroTutor/public/privacy.html';
const INSTRUCTION_IMAGES = Array.from({ length: 9 }, (_, index) => `/api-key-instructions/step-${index + 1}.jpg`);
const INSTRUCTION_AUTO_ADVANCE_MS = 3200;

const ApiKeyGate: React.FC<ApiKeyGateProps> = ({
  isOpen,
  isBlocking,
  hasKey,
  maskedKey,
  isSaving = false,
  error,
  onSave,
  onClear,
  onClose,
  onValueChange,
}) => {
  const { t } = useAppTranslations();
  const [value, setValue] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [showInstructions, setShowInstructions] = useState(false);
  const [instructionIndex, setInstructionIndex] = useState(0);
  const [isAutoPlaying, setIsAutoPlaying] = useState(true);
  const canClose = !isBlocking;
  const totalInstructions = INSTRUCTION_IMAGES.length;

  const canSave = useMemo(() => {
    return value.trim().length >= 20 && !isSaving;
  }, [value, isSaving]);

  useEffect(() => {
    if (!isOpen) {
      setShowInstructions(false);
      setInstructionIndex(0);
      setIsAutoPlaying(true);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!showInstructions) return;
    setInstructionIndex(0);
    setIsAutoPlaying(true);
  }, [showInstructions]);

  useEffect(() => {
    if (!showInstructions || !isAutoPlaying || totalInstructions <= 1) return;
    const intervalId = window.setInterval(() => {
      setInstructionIndex((prev) => (prev + 1) % totalInstructions);
    }, INSTRUCTION_AUTO_ADVANCE_MS);
    return () => window.clearInterval(intervalId);
  }, [showInstructions, isAutoPlaying, totalInstructions]);

  const currentInstruction = INSTRUCTION_IMAGES[instructionIndex] || '';
  const showHeaderClose = showInstructions || canClose;
  const headerCloseLabel = showInstructions ? t('apiKeyGate.closeInstructions') : t('apiKeyGate.close');
  const handleHeaderClose = showInstructions ? () => setShowInstructions(false) : onClose;

  const handleInstructionStep = (direction: number) => {
    if (totalInstructions === 0) return;
    setInstructionIndex((prev) => (prev + direction + totalInstructions) % totalInstructions);
    setIsAutoPlaying(false);
  };

  const handleInstructionJump = (nextIndex: number) => {
    setInstructionIndex(nextIndex);
    setIsAutoPlaying(false);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white shadow-xl border border-slate-200">
        <div className="flex items-start justify-between px-6 pt-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-blue-50 text-blue-600">
              <IconShield className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-slate-900">{t('apiKeyGate.title')}</h2>
              <p className="text-sm text-slate-600">
                {t('apiKeyGate.subtitle')}{' '}
                <button
                  onClick={() => openExternalUrl(PRIVACY_POLICY_URL)}
                  className="text-blue-600 hover:underline inline-flex items-center"
                >
                  {t('apiKeyGate.privacyPolicy')}
                </button>
              </p>
            </div>
          </div>
          {showHeaderClose && (
            <button
              onClick={handleHeaderClose}
              className="text-slate-400 hover:text-slate-700"
              aria-label={headerCloseLabel}
            >
              <IconXMark className="h-5 w-5" />
            </button>
          )}
        </div>

        <div className="px-6 py-4 space-y-4">
          {showInstructions ? (
            <div className="flex flex-col items-center gap-4">
              <div className="relative w-full max-w-[360px] overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="aspect-[9/16] w-full bg-slate-100">
                  <img
                    src={currentInstruction}
                    alt={t('apiKeyGate.instructionStep', { step: instructionIndex + 1, total: totalInstructions })}
                    className="h-full w-full object-contain"
                    draggable={false}
                  />
                </div>
                {totalInstructions > 1 && (
                  <>
                    <div className="pointer-events-none absolute inset-0 flex items-center justify-between px-2">
                      <button
                        type="button"
                        onClick={() => handleInstructionStep(-1)}
                        aria-label={t('apiKeyGate.previousInstruction')}
                        className="pointer-events-auto rounded-full bg-white/90 p-2 text-slate-600 shadow hover:bg-white"
                      >
                        <IconChevronLeft className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleInstructionStep(1)}
                        aria-label={t('apiKeyGate.nextInstruction')}
                        className="pointer-events-auto rounded-full bg-white/90 p-2 text-slate-600 shadow hover:bg-white"
                      >
                        <IconChevronRight className="h-5 w-5" />
                      </button>
                    </div>
                    <div className="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-white/90 px-3 py-1 shadow-sm">
                      <div className="flex items-center gap-1.5">
                        {INSTRUCTION_IMAGES.map((_, index) => (
                          <button
                            key={`instruction-dot-${index}`}
                            type="button"
                            aria-label={t('apiKeyGate.instructionStep', { step: index + 1, total: totalInstructions })}
                            onClick={() => handleInstructionJump(index)}
                            className={`h-2 w-2 rounded-full ${index === instructionIndex ? 'bg-blue-600' : 'bg-slate-300'}`}
                          />
                        ))}
                      </div>
                    </div>
                    <div className="absolute right-3 top-3 rounded-full bg-white/90 px-2 py-1 text-xs text-slate-600 shadow-sm">
                      {instructionIndex + 1} / {totalInstructions}
                    </div>
                  </>
                )}
              </div>
            </div>
          ) : (
            <>
              <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700 space-y-2">
                <div className="font-medium text-slate-800">{t('apiKeyGate.stepsTitle')}</div>
                <ol className="list-decimal pl-5 space-y-1">
                  <li>{t('apiKeyGate.stepOne')}</li>
                  <li>{t('apiKeyGate.stepTwo')}</li>
                </ol>
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-2 text-white hover:bg-blue-700"
                    onClick={() => openExternalUrl(AI_STUDIO_URL)}
                  >
                    {t('apiKeyGate.openAiStudio')}
                  </button>
                  <button
                    type="button"
                    onClick={() => setShowInstructions(true)}
                    aria-label={t('apiKeyGate.viewInstructions')}
                    className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-100"
                  >
                    <IconQuestionMarkCircle className="h-5 w-5" />
                  </button>
                </div>
              </div>

              <label className="block text-sm font-medium text-slate-800">{t('apiKeyGate.keyLabel')}</label>
              <div className="flex items-center gap-2">
                <input
                  type={showKey ? 'text' : 'password'}
                  value={value}
                  onChange={(e) => {
                    const next = e.target.value;
                    setValue(next);
                    onValueChange?.(next);
                  }}
                  placeholder={t('apiKeyGate.placeholder')}
                  className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  autoFocus
                />
                <button
                  className="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-100"
                  onClick={() => setShowKey(!showKey)}
                  type="button"
                >
                  {showKey ? t('apiKeyGate.hide') : t('apiKeyGate.show')}
                </button>
              </div>

              {error && <div className="text-sm text-red-600">{error}</div>}

              {hasKey && (
                <div className="rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-800 flex items-center gap-2">
                  <IconCheck className="h-4 w-4" />
                  {t('apiKeyGate.currentKeySaved', { maskedKey: maskedKey ? `(${maskedKey})` : '' }).trim()}
                </div>
              )}
            </>
          )}
        </div>

        {!showInstructions && (
          <div className="flex items-center justify-between px-6 pb-6">
            <button
              className="text-sm text-slate-500 hover:text-slate-700"
              onClick={onClear}
              disabled={!hasKey}
            >
              {t('apiKeyGate.clearSavedKey')}
            </button>
            <div className="flex items-center gap-2">
              {canClose && (
                <button
                  onClick={onClose}
                  className="rounded-lg border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-100"
                >
                  {t('apiKeyGate.cancel')}
                </button>
              )}
              <button
                onClick={async () => {
                  const ok = await onSave(value);
                  if (ok) {
                    setValue('');
                    if (canClose) onClose();
                  }
                }}
                disabled={!canSave}
                className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {isSaving ? t('apiKeyGate.saving') : t('apiKeyGate.saveKey')}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default ApiKeyGate;