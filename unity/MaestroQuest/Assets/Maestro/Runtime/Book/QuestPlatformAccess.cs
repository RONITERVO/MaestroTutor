// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Book
{
    [Serializable] public sealed class QuestPlatformConfiguration { public int version; public bool enabled; public string appId; }
    public sealed class QuestPlatformAccess:MonoBehaviour
    {
        static QuestPlatformAccess instance;
        QuestPlatformSession session;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void StartPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Ensure();
#endif
        }
        public static QuestPlatformAccess Ensure()
        {
            if(!instance){var owner=new GameObject("Maestro platform access");instance=owner.AddComponent<QuestPlatformAccess>();DontDestroyOnLoad(owner);}
            return instance;
        }
        void Awake()
        {
            bool required=true;
#if UNITY_EDITOR || MAESTRO_QUEST_DEVELOPMENT
            required=false;
#endif
            session=new QuestPlatformSession(new MetaApi(),()=>Time.realtimeSinceStartupAsDouble,required);
            QuestPlatformConfiguration config=null;
            try{var text=Resources.Load<TextAsset>("QuestPlatform");if(text)config=JsonUtility.FromJson<QuestPlatformConfiguration>(text.text);}catch(Exception){}
            session.Begin(config!=null&&config.version==1&&config.enabled,config?.appId);
        }
        void Update(){session.Tick();if(session.MustExit){Debug.LogError("Maestro could not verify access to this Store installation.");Application.Quit();enabled=false;}}
        public void Integrity(string nonce,Action<string,string> complete)=>session.Integrity(nonce,complete);
        void OnDestroy(){session?.Dispose();if(instance==this)instance=null;}
        sealed class MetaApi:IQuestPlatformApi
        {
            public void Initialize(string appId,Action<bool> complete)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Oculus.Platform.Core.AsyncInitialize(appId).OnComplete(result=>complete(result!=null&&!result.IsError));
#else
                complete(false);
#endif
            }
            public void Entitlement(Action<bool> complete)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(result=>complete(result!=null&&!result.IsError));
#else
                complete(false);
#endif
            }
            public void Integrity(string nonce,Action<string> complete)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Oculus.Platform.DeviceApplicationIntegrity.GetIntegrityToken(nonce).OnComplete(result=>complete(result!=null&&!result.IsError?result.Data:null));
#else
                complete(null);
#endif
            }
        }
    }
}
