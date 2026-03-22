package com.ronitervo.maestrotutor;

import android.util.Log;

import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

@CapacitorPlugin(name = "AndroidDebugLog")
public class AndroidDebugLogPlugin extends Plugin {

    private static final String DEFAULT_TAG = "MaestroDebug";

    @PluginMethod
    public void log(PluginCall call) {
        String message = call.getString("message");
        if (message == null || message.isEmpty()) {
            call.reject("message is required");
            return;
        }

        String tag = call.getString("tag", DEFAULT_TAG);
        String level = call.getString("level", "debug");

        switch (level) {
            case "error":
                Log.e(tag, message);
                break;
            case "warn":
                Log.w(tag, message);
                break;
            case "info":
                Log.i(tag, message);
                break;
            case "debug":
            default:
                Log.d(tag, message);
                break;
        }

        call.resolve();
    }
}
