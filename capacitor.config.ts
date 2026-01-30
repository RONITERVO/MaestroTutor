import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.ronitervo.maestrotutor',
  appName: 'Maestro Tutor',
  webDir: 'dist',
  server: {
    androidScheme: 'https'
  },
  android: {
    allowMixedContent: false,
    webContentsDebuggingEnabled: false
  }
};

export default config;
