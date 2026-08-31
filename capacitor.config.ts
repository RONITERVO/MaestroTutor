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
    // Opt-in per build rather than a constant that has to be remembered and
    // reverted. `MAESTRO_WEBVIEW_DEBUG=1 npx cap sync android` produces a
    // release-signed build that chrome://inspect can attach to, so a device
    // can be debugged without installing a debug build — which, being signed
    // differently, would require an uninstall first and take the user's chat
    // history and API key with it.
    webContentsDebuggingEnabled: process.env.MAESTRO_WEBVIEW_DEBUG === '1'
  }
};

export default config;
