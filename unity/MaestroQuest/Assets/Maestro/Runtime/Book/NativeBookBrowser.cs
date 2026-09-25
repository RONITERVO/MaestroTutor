// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using TLab.WebView;
using UnityEngine;
using UnityEngine.UI;

namespace Maestro.Quest.Book
{
    [Serializable]
    public class BookSnapshot
    {
        public int version;
        public string layout;
        public string activity;
        public string bookmarkMessageId;
        public string selectedArtifactId;
        public int historyStart;
        public int historyEnd;
        public int historyTotal;
        public bool audioPaused;
    }

    public sealed class NativeBookBrowser : FragmentCapture, IBookBrowser
    {
        public override string package => "com.maestro.quest.browser.BookWebView";
        public Texture Surface => m_contentView;
        public Vector2Int Resolution => m_viewSize;
        public bool IsReady => m_state == State.Initialized;
        public BookSnapshot Snapshot { get; private set; }
        public string Error { get; private set; }
        public event Action<BookSnapshot> SnapshotChanged;
        public event Action<string> ExternalLinkRequested;
        long pointerDownTime;
        bool pointerHeld;
        bool suspended;
        bool? nativeSuspended;
        bool applicationPaused, applicationFocused = true;
        float nextPoll;
        string previousSnapshot;

        void Start()
        {
            // CSS matches a 512px-wide phone page, with a sharper GPU texture.
            m_viewSize = new Vector2Int(1024, 768);
            m_texSize = new Vector2Int(2048, 1536);
            m_captureMode = CaptureMode.HardwareBuffer;
            m_fps = 30;
            var transport = new GameObject("Browser texture transport", typeof(RectTransform), typeof(RawImage));
            transport.transform.SetParent(transform, false);
            m_rawImage = transport.GetComponent<RawImage>();
            m_rawImage.enabled = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            Init();
#else
            Error = "The native browser is available in the Android build. Use the web book fixture to test page presentation on desktop.";
#endif
        }

        protected override void InitNativePlugin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            m_NativePlugin.Call("SetFps", m_fps);
            m_NativePlugin.Call("InitBook", m_viewSize.x, m_viewSize.y, m_texSize.x, m_texSize.y, m_screenFullRes.x, m_isVulkan, (int)m_captureMode);
#endif
        }

        void Update()
        {
            GarbageCollect();
            if (!IsReady) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeSuspended != suspended) { m_NativePlugin.Call("SetSuspended", suspended); nativeSuspended = suspended; }
#endif
            if (suspended) return;
            UpdateFrame();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .2f;
            m_NativePlugin.Call("RequestSnapshot");
            string json = m_NativePlugin.Call<string>("ReadSnapshot");
            Error = m_NativePlugin.Call<string>("ReadError");
            if (!string.IsNullOrEmpty(json) && json.Length <= 4096 && json != previousSnapshot)
            {
                try
                {
                    var snapshot = JsonUtility.FromJson<BookSnapshot>(json);
                    if (snapshot != null && snapshot.version == 1)
                    {
                        previousSnapshot = json;
                        Snapshot = snapshot;
                        SnapshotChanged?.Invoke(snapshot);
                    }
                }
                catch (ArgumentException) { Error = "The book sent an invalid state update."; }
            }
            var link = m_NativePlugin.Call<string>("TakeExternalLink");
            if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == "https") ExternalLinkRequested?.Invoke(link);
#endif
        }

        public void Pointer(int x, int y, BrowserPointerPhase phase)
        {
            if (!IsReady || suspended) return;
            if (phase == BrowserPointerPhase.Down) pointerHeld = true;
            else if (!pointerHeld) return;
            x = Mathf.Clamp(x, 0, m_viewSize.x - 1); y = Mathf.Clamp(y, 0, m_viewSize.y - 1);
            int action = phase == BrowserPointerPhase.Down ? 0 : phase == BrowserPointerPhase.Up ? 1 : phase == BrowserPointerPhase.Move ? 2 : 3;
#if UNITY_ANDROID && !UNITY_EDITOR
            pointerDownTime = m_NativePlugin.Call<long>("TouchEvent", x, y, action, pointerDownTime);
#endif
            if (phase == BrowserPointerPhase.Up || phase == BrowserPointerPhase.Cancel) { pointerHeld = false; pointerDownTime = 0; }
        }

        public void ExecuteBookCommand(string commandJson)
        {
            if (!IsReady || suspended || string.IsNullOrEmpty(commandJson) || commandJson.Length > 4096) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            m_NativePlugin.Call("ExecuteBookCommand", commandJson);
#endif
        }

        public void SetSuspended(bool value)
        {
            if (value && pointerHeld) Pointer(0, 0, BrowserPointerPhase.Cancel);
            suspended = value;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsReady && nativeSuspended != value) { m_NativePlugin.Call("SetSuspended", value); nativeSuspended = value; }
#endif
        }

        void OnApplicationPause(bool paused) { applicationPaused = paused; SetSuspended(applicationPaused || !applicationFocused); }
        void OnApplicationFocus(bool focused) { applicationFocused = focused; SetSuspended(applicationPaused || !applicationFocused); }
    }
}
