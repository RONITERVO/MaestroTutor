// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Maestro.Quest.Creation
{
    public sealed partial class RoomStorage
    {
        internal sealed class PublicationResult
        {
            internal readonly bool Saved,MayHavePublished;
            internal readonly string Error;
            internal PublicationResult(bool saved,bool mayHavePublished,string error)
            {Saved=saved;MayHavePublished=mayHavePublished;Error=error;}
        }
        // Pure storage ownership. A native caller must separately reserve its
        // journal/resources and retain the workspace until Completion drains.
        // Starting this operation is not native acceptance or an action receipt.
        internal sealed class PreparedPublication
        {
            // 0 queued/preflight; 1 authorized/crossed boundary; 2 cancelled; 3 rejected; 4 ready.
            // Once crossed, cancellation cannot retract a possibly durable effect.
            int boundary;
            readonly TaskCompletionSource<bool> acceptance=new(TaskCreationOptions.RunContinuationsAsynchronously);
            // State 4 is an advisory preflight, with no disk lock held.
            internal bool AwaitingAcceptance=>Volatile.Read(ref boundary)==4;
            internal bool Authorize()
            {
                if(Interlocked.CompareExchange(ref boundary,1,4)!=4)return false;
                acceptance.TrySetResult(true);return true;
            }
            internal Task<PublicationResult> Completion {get;private set;}
            internal bool Started=>Volatile.Read(ref boundary)==1;
            internal bool CancelBeforeStart()
            {
                int observed=Volatile.Read(ref boundary);
                while(observed==0||observed==4){
                    int previous=Interlocked.CompareExchange(ref boundary,2,observed);
                    if(previous==observed){acceptance.TrySetResult(false);return true;}
                    observed=previous;
                }
                return false;
            }
            internal static PreparedPublication Rejected(string error)=>new(){
                boundary=3,Completion=Task.FromResult(new PublicationResult(false,false,error))
            };
            internal PreparedPublication(){}
            internal void Dispatch(RoomStorage storage,PreparedSave preparation,
                VersionedRoomFile<RoomDocument>.PreparedSave candidate,Action<string> checkpoint,bool awaitAcceptance)
            {
                // No cancellation token on Task.Run: even a cancelled queued job
                // must execute its cleanup and release the preparation owner.
                Completion=Task.Run(async()=>{
                    try {
                        checkpoint?.Invoke("queued");
                        if(awaitAcceptance){
                            checkpoint?.Invoke("preflight");
                            if(!storage.CheckPreparedCandidate(candidate,out var issue)){
                                Interlocked.CompareExchange(ref boundary,3,0);
                                return new PublicationResult(false,false,issue);
                            }
                            checkpoint?.Invoke("ready");
                            if(Interlocked.CompareExchange(ref boundary,4,0)!=0||!await acceptance.Task.ConfigureAwait(false))
                                return new PublicationResult(false,false,"Room publication was cancelled before native acceptance.");
                        }else if(Interlocked.CompareExchange(ref boundary,1,0)!=0)
                            return new PublicationResult(false,false,"Room publication was cancelled before it started.");
                        checkpoint?.Invoke("publishing");
                        bool saved=storage.PublishPreparedCandidate(candidate,out var error,wait:awaitAcceptance);
                        if(saved)checkpoint?.Invoke("published");
                        return new PublicationResult(saved,true,saved?null:error??"Room publication was not confirmed; inspect saved data before another request.");
                    }catch(Exception){
                        bool attempted=Started;
                        if(!attempted)Interlocked.Exchange(ref boundary,3);
                        return new PublicationResult(false,attempted,attempted?
                            "Room publication was not confirmed and may have changed saved data. Inspect it before another request.":
                            "Room publication could not start; accepted data was not changed.");
                    }finally{
                        // The future does not finish while cleanup still owns bytes
                        // or admission. Dispose on the caller cannot steal this stage.
                        try{candidate.Dispose();}finally{preparation.FinishPublication();}
                    }
                });
            }
        }
        // Consume on the caller before dispatch, without doing file IO there.
        // Wrong-store/replayed requests do not consume another owner's preparation.
        // The single preparation slot bounds queued + running publication together.
        internal PreparedPublication StartPreparedPublication(PreparedSave prepared,Action<string> checkpoint=null,bool awaitAcceptance=false)
        {
            if(prepared==null||!prepared.Take(this,out var candidate))
                return PreparedPublication.Rejected("This room save preparation was already used or belongs to another store.");
            var publication=new PreparedPublication();
            try{publication.Dispatch(this,prepared,candidate,checkpoint,awaitAcceptance);return publication;}
            catch{
                try{candidate.Dispose();}finally{prepared.FinishPublication();}
                return PreparedPublication.Rejected("Room publication could not be dispatched; accepted data was not changed.");
            }
        }
    }
}
