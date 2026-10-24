// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID && DEVELOPMENT_BUILD
using System.Collections;
using Meta.XR;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Book
{
    public sealed partial class NativeBookBrowser
    {
        IEnumerator RegisterRenderingDiagnostics()
        {
            yield return null;
            MetaXROperatorExternalTool.RegisterAgenticTool("maestro_book_rendering",
                "Read book frame/copy counters, or compare newFrames versus continuous copying. Development-only, session-only; no room/chat data or visual changes. Use observe to read without changes.",
                new[] {new AgenticToolParameter {Name="mode",Description="observe, newFrames, or continuous",ParamType=XrAgenticExternalToolParameterTypeMETAX1.String,IsRequired=true}},
                input=>{
                    if(!this||!IsReady)return "{\"available\":false}";
                    string mode=(string)JObject.Parse(input)["mode"];
                    if(mode!="observe"&&mode!="newFrames"&&mode!="continuous")return "{\"available\":false,\"error\":\"Unknown comparison mode\"}";
                    return m_NativePlugin.Call<string>("RenderingDiagnostics",mode);
                });
        }
    }
}
#endif
