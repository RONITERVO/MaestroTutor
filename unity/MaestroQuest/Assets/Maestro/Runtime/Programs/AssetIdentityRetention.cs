// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    // One owner-thread observation budget across documents, scopes and events.
    // A stopped traversal is uncertainty, never evidence that an asset is unused.
    internal sealed class AssetIdentityRetention
    {
        internal const int MaximumNodes=65536;
        readonly string identity;
        int remaining;
        internal bool Found {get;private set;}
        internal bool Uncertain {get;private set;}
        internal AssetIdentityRetention(string identity,int budget=MaximumNodes)
        {this.identity=identity;remaining=budget;if(!Maestro.Quest.Imports.ModelLibrary.ValidHash(identity)||budget<1)Unknown();}
        internal void Unknown()=>Uncertain=true;
        internal void Include(bool found,bool uncertain=false){Found|=found;Uncertain|=uncertain;}
        internal bool Step(){if(remaining-- >0)return true;Unknown();return false;}
        internal void Text(string value){if(Step()&&value==identity)Found=true;}
        internal void Program(BehaviourProgram value)
        {if(!Step())return;if(value==null)Unknown();else Found|=value.ReferencesAssetIdentity(identity);}
        internal void Value(ProgramValue value)
        {
            if(value.Type.Kind==ProgramType.Text){Text(value.Text);return;}
            if(value.Type.Kind is ProgramType.Void or ProgramType.Number or ProgramType.Boolean){Step();return;}
            if(value.Nodes>128||value.Characters>ProgramValue.MaximumCharacters){Unknown();return;}
            Json((JToken)value.Value);
        }
        internal void Json(JToken value,int depth=0)
        {
            if(value==null)return;
            if(!Step())return;
            if(depth>64){Unknown();return;}
            if(value.Type==JTokenType.String){Found|=(string)value==identity;return;}
            if(value is JContainer container){foreach(var child in container.Children()){if(remaining<1){Unknown();break;}Json(child,depth+1);}return;}
            if(value.Type is not (JTokenType.Integer or JTokenType.Float or JTokenType.Boolean or JTokenType.Null))Unknown();
        }
    }
}
