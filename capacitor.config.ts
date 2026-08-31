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
    // reverted:
    //
    //   MAESTRO_WEBVIEW_DEBUG=1 npm run build
    //   MAESTRO_WEBVIEW_DEBUG=1 npx cap sync android
    //   cd android && ./gradlew assembleRelease
    //   adb install -r android/app/build/outputs/apk/release/app-release.apk
    //
    // The variable is needed on both commands: the build step reads it to keep
    // function names (vite.config.ts), and the sync step is what writes this
    // flag into the native config. The result is a release-signed build that
    // chrome://inspect can attach to, so a device can be debugged without
    // installing a debug build — which, being signed differently, would require
    // an uninstall first and take the user's chat history and API key with it.
    webContentsDebuggingEnabled: process.env.MAESTRO_WEBVIEW_DEBUG === '1'
  }
};

export default config;
