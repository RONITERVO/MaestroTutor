import { Capacitor, registerPlugin } from '@capacitor/core';
import { useMaestroStore } from '../../store';

type SttFlowLogLevel = 'debug' | 'info' | 'warn' | 'error';
type SttFlowDetails = Record<string, unknown> | undefined;

interface AndroidDebugLogPluginInterface {
  log(options: {
    tag?: string;
    level?: SttFlowLogLevel;
    message: string;
  }): Promise<void>;
}

const AndroidDebugLogNative = registerPlugin<AndroidDebugLogPluginInterface>('AndroidDebugLog');
const isNativeAndroid = Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

export const STT_FLOW_LOG_TAG = 'MaestroSttFlow';
export const STT_FLOW_DEBUG_STORAGE_KEY = 'maestro.sttFlowDebug';

const isSttFlowDebugEnabled = (): boolean => {
  try {
    if (useMaestroStore.getState().showDebugLogs) {
      return true;
    }
  } catch {
    // Ignore store access failures during early bootstrap.
  }

  if (typeof window !== 'undefined') {
    try {
      return window.localStorage.getItem(STT_FLOW_DEBUG_STORAGE_KEY) === '1';
    } catch {
      // Ignore localStorage access failures.
    }
  }

  return false;
};

const formatMessage = (stage: string, details?: SttFlowDetails): string => {
  if (!details || Object.keys(details).length === 0) {
    return stage;
  }
  try {
    return `${stage} ${JSON.stringify(details)}`;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return `${stage} {"logSerializeError":${JSON.stringify(message)}}`;
  }
};

const logToConsole = (level: SttFlowLogLevel, message: string) => {
  const line = `[${STT_FLOW_LOG_TAG}] ${message}`;
  switch (level) {
    case 'error':
      console.error(line);
      break;
    case 'warn':
      console.warn(line);
      break;
    case 'info':
      console.info(line);
      break;
    case 'debug':
    default:
      console.debug(line);
      break;
  }
};

const emitLog = (level: SttFlowLogLevel, stage: string, details?: SttFlowDetails) => {
  if (!isSttFlowDebugEnabled()) {
    return;
  }
  const message = formatMessage(stage, details);
  if (isNativeAndroid) {
    void AndroidDebugLogNative.log({
      tag: STT_FLOW_LOG_TAG,
      level,
      message,
    }).catch(() => {
      logToConsole(level, message);
    });
    return;
  }
  logToConsole(level, message);
};

export const logSttFlow = (stage: string, details?: SttFlowDetails) => {
  emitLog('debug', stage, details);
};

export const infoSttFlow = (stage: string, details?: SttFlowDetails) => {
  emitLog('info', stage, details);
};

export const warnSttFlow = (stage: string, details?: SttFlowDetails) => {
  emitLog('warn', stage, details);
};

export const errorSttFlow = (stage: string, details?: SttFlowDetails) => {
  emitLog('error', stage, details);
};
