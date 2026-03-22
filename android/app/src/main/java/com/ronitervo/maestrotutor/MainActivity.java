package com.ronitervo.maestrotutor;

import android.os.Bundle;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
  @Override
  public void onCreate(Bundle savedInstanceState) {
    registerPlugin(AndroidDebugLogPlugin.class);
    registerPlugin(ThemeBillingPlugin.class);
    super.onCreate(savedInstanceState);
    if (getBridge() != null && getBridge().getWebView() != null) {
      getBridge().getWebView().getSettings().setMediaPlaybackRequiresUserGesture(false);
    }
  }
}
