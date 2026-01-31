import React, { useMemo, useState } from 'react';
import { IconCheck, IconShield, IconXMark } from '../../../shared/ui/Icons';
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
  const canClose = !isBlocking;

  const canSave = useMemo(() => {
    return value.trim().length >= 20 && !isSaving;
  }, [value, isSaving]);

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
          {canClose && (
            <button
              onClick={onClose}
              className="text-slate-400 hover:text-slate-700"
              aria-label={t('apiKeyGate.close')}
            >
              <IconXMark className="h-5 w-5" />
            </button>
          )}
        </div>

        <div className="px-6 py-4 space-y-4">
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700 space-y-2">
            <div className="font-medium text-slate-800">{t('apiKeyGate.stepsTitle')}</div>
            <ol className="list-decimal pl-5 space-y-1">
              <li>{t('apiKeyGate.stepOne')}</li>
              <li>{t('apiKeyGate.stepTwo')}</li>
            </ol>
            <button
              className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-2 text-white hover:bg-blue-700"
              onClick={() => openExternalUrl(AI_STUDIO_URL)}
            >
              {t('apiKeyGate.openAiStudio')}
            </button>
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
        </div>

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
      </div>
    </div>
  );
};

export default ApiKeyGate;
