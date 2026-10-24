// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>One cancellable request shared by the book camera and capability runner.</summary>
    internal sealed class VirtualRoomCapture:IDisposable
    {
        readonly CancellationTokenSource cancellation=new();
        readonly Func<bool> current;
        JObject image;string error;bool done,disposed;
        internal bool Done=>done;
        internal VirtualRoomCapture(Func<bool> current,Func<CancellationToken,Task<JObject>> start){this.current=current;_=Run(start);}
        async Task Run(Func<CancellationToken,Task<JObject>> start){
            try{var result=await start(cancellation.Token);if(!disposed)image=result;}
            catch(OperationCanceledException){error="Virtual room capture was interrupted; request a new view";}
            catch(InvalidOperationException failure){error=failure.Message;}
            catch(Exception){error="The virtual room view could not be prepared";}
            finally{done=true;cancellation.Dispose();}
        }
        internal bool Read(out JObject result,out string issue){
            result=null;issue=null;if(!done)return false;
            if(disposed||!current()){issue="Virtual room capture changed or ended; request a new view";return false;}
            issue=error;if(issue!=null)return false;
            result=(JObject)image.DeepClone();return true;
        }
        public void Dispose(){if(disposed)return;disposed=true;image=null;if(!done)cancellation.Cancel();}
    }
}
