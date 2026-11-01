// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
namespace Maestro.Quest.Programs
{
    public sealed partial class ProgramMachine
    {
        internal bool RetainsAssetIdentity(string identity,out bool uncertain,bool exemptInvocation=false)
        {var scan=new AssetIdentityRetention(identity);ReadAssetRetention(scan,exemptInvocation);uncertain=scan.Uncertain;return scan.Found;}
        internal void ReadAssetRetention(AssetIdentityRetention scan,bool exemptInvocation=false)
        {
            var machines=new HashSet<ProgramMachine>();var scopes=new HashSet<Scope>();
            void ScopeValues(Scope scope)
            {
                if(scope==null||!scopes.Add(scope))return;
                if(!scan.Step())return;
                if(scope.Values==null){scan.Unknown();return;}
                foreach(var value in scope.Values.Values){if(!scan.Step())break;scan.Value(value);}
            }
            void Read(ProgramMachine machine,bool exempt)
            {
                if(machine==null){scan.Unknown();return;}
                if(!machines.Add(machine)||!scan.Step())return;
                // Only the trusted, single-call invocation wrapper may omit its
                // deletion arguments. Authored literals and every live value stay.
                if(!exempt||!machine.program.IsInvocation)scan.Program(machine.program);
                foreach(var value in machine.state.Values){if(!scan.Step())break;scan.Value(value);}
                foreach(var frame in machine.frames){if(!scan.Step())break;ScopeValues(frame.Scope);ScopeValues(frame.Caller);}
                ScopeValues(machine.observed);ScopeValues(machine.waitingScope);
                ScopeValues(machine.resultScope);ScopeValues(machine.parallelScope);
                scan.Value(machine.Result);scan.Json(machine.LastOutput);
                if(machine.Signal!=null)scan.Value(machine.Signal.Value);
                if(machine.Wait!=null)scan.Json(machine.Wait.Arguments);
                if(machine.Branches!=null)foreach(var branch in machine.Branches){if(!scan.Step())break;Read(branch,false);}
            }
            Read(this,exemptInvocation);
        }
    }
}
