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
        public string librarySession;
        public int libraryRevision;
        public LibraryBookRequest libraryRequest;
        public QuestIntegrityRequest integrityRequest;
    }

    public sealed partial class NativeBookBrowser : FragmentCapture, IBookBrowser
    {
        public override string package => "com.maestro.quest.browser.BookWebView";
        public Texture Surface => m_contentView;
        public Vector2Int Resolution => m_viewSize;
        public bool IsReady => m_state == State.Initialized;
        public BookSnapshot Snapshot { get; private set; }
        public string Error { get; private set; }
        public event Action<BookSnapshot> SnapshotChanged;
        public event Action<string> ExternalLinkRequested;
        public void ClearError()
        {
            Error = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsReady) m_NativePlugin.Call("ClearError");
#endif
        }
        QuestIntegrityExchange integrity;
        long pointerDownTime;
        bool pointerHeld;
        bool suspended;
        bool? nativeSuspended;
        bool applicationPaused, applicationFocused = true;
        float nextPoll;
        string previousSnapshot,libraryPublication,libraryPublicationSession;
        int libraryPublicationRevision;

        void Start()
        {
            integrity=new QuestIntegrityExchange((nonce,complete)=>QuestPlatformAccess.Ensure().Integrity(nonce,complete));
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
#if DEVELOPMENT_BUILD
            StartCoroutine(RegisterRenderingDiagnostics());
#endif
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
            ReadSnapshot(json);
            integrity.Poll(Snapshot?.integrityRequest,Time.realtimeSinceStartupAsDouble,value=>m_NativePlugin.Call("PublishIntegrityResult",value));
            DeliverLibraryState(true,Snapshot,value=>m_NativePlugin.Call("PublishLibraryState",value));
            var link = m_NativePlugin.Call<string>("TakeExternalLink");
            try
            {
                if (!BookExternalLinks.TryOpen(link, Application.OpenURL)
                    && Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == "https") ExternalLinkRequested?.Invoke(link);
            }
            catch (Exception) {
                Error = link == BookExternalLinks.PrivacyUrl ? "Open chatwithmaestro.com/privacy.html on your phone or computer to read the privacy policy."
                    : link == BookExternalLinks.GeminiTermsUrl ? "Open ai.google.dev/gemini-api/terms on your phone or computer to read the Gemini terms."
                    : "Open chatwithmaestro.com/quest-link.html on your phone or computer to finish sign-in.";
            }
#endif
        }

        // Empty native state marks navigation/replacement. Invalidate the cached
        // activity even when the next page eventually sends identical JSON.
        void ReadSnapshot(string json)
        {
            if (suspended || string.IsNullOrEmpty(json) || json.Length > 4096) { integrity?.Clear(); Snapshot=null; previousSnapshot=null; return; }
            if (json == previousSnapshot) return;
            try
            {
                var snapshot=JsonUtility.FromJson<BookSnapshot>(json);
                if (snapshot == null || snapshot.version != 1) { Snapshot=null; previousSnapshot=null; return; }
                previousSnapshot=json; Snapshot=snapshot; SnapshotChanged?.Invoke(snapshot);
            }
            catch (ArgumentException) { Snapshot=null; previousSnapshot=null; Error="The book sent an invalid state update."; }
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

        public string ReadRoomAgentSnapshot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(IsReady && !suspended) { m_NativePlugin.Call("RequestRoomAgentSnapshot"); return m_NativePlugin.Call<string>("ReadRoomAgentSnapshot"); }
#endif
            return null;
        }
        public void PublishRoomAgentState(string json)
        {
            if(!IsReady || suspended || string.IsNullOrEmpty(json) || json.Length>327680) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            m_NativePlugin.Call("PublishRoomAgentState",json);
#endif
        }

        public void PublishRoomCapture(string json)
        {
            if(!IsReady||suspended||string.IsNullOrEmpty(json)||json.Length>140000)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            m_NativePlugin.Call("PublishRoomCapture",json);
#endif
        }

        public void PublishLibraryState(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 32768) return;
            try {
                var value=Newtonsoft.Json.Linq.JObject.Parse(json);string session=(string)value["session"];int revision=(int)value["revision"];
                if(string.IsNullOrEmpty(session)||revision<1)return;
                libraryPublication=json;libraryPublicationSession=session;libraryPublicationRevision=revision;
            }catch(Exception){return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            DeliverLibraryState(IsReady,Snapshot,value=>m_NativePlugin.Call("PublishLibraryState",value));
#endif
        }

        // The persistent browser owns retry/ack after a library owner is destroyed. JNI dispatch
        // alone is not delivery: the Android UI thread may still be suspended or navigating.
        internal void DeliverLibraryState(bool ready,BookSnapshot snapshot,Action<string> publish)
        {
            if(!ready||suspended||libraryPublication==null)return;
            if(snapshot?.librarySession==libraryPublicationSession&&snapshot.libraryRevision==libraryPublicationRevision)return;
            publish(libraryPublication);
        }

        public void SetSuspended(bool value)
        {
            if (value && pointerHeld) Pointer(0, 0, BrowserPointerPhase.Cancel);
            if (suspended != value) { integrity?.Clear(); Snapshot=null; previousSnapshot=null; }
            suspended = value;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsReady && nativeSuspended != value) { m_NativePlugin.Call("SetSuspended", value); nativeSuspended = value; }
#endif
        }

        void OnDisable() { integrity?.Clear(); }
        void OnApplicationPause(bool paused) { applicationPaused = paused; SetSuspended(applicationPaused || !applicationFocused); }
        void OnApplicationFocus(bool focused) { applicationFocused = focused; SetSuspended(applicationPaused || !applicationFocused); }
    }
}
