// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class AnimationNodeFacts {
        static JObject Schema()=>Object(new JObject{
            ["modelHash"]=Text(null,64),["total"]=Number(0,512,true),["next"]=Number(-1,512,true),
            ["nodes"]=List(Object(new JObject{["index"]=Number(0,511,true),["parent"]=Number(-1,511,true),["name"]=Text(null,64)}),0,8)
        });
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.animation.nodes",OutputType(Schema()),
            "Imported animation nodes","Inspect eight source nodes of the exact loaded model for an explicit gait anchor. Parent -1 means no parent in the imported node list. Indices belong to modelHash; do not reuse them on another model. Read the next offset until next=-1. This includes non-humanoid nodes and does not choose an anchor or animation, load a model or move the object.",
            Object(new JObject{["target"]=AnimationTargets.TargetSchema(),["offset"]=Number(0,512,true)}),
            new JObject{["target"]="maestro",["offset"]=0},(c,a)=>{
                var model=AnimationTargets.ClipModel(c.Editor?c.Editor.Find((string)a["target"]):null);
                if(!model||!model.Ready||model.Instance==null)return null;
                var nodes=model.Instance.Nodes;int offset=(int)a["offset"],end=System.Math.Min(offset+8,nodes.Count);
                var values=new JArray();
                for(int i=offset;i<end;i++){
                    int parent=-1;var node=nodes[i];
                    if(node&&node.parent)for(int j=0;j<nodes.Count;j++)if(nodes[j]==node.parent){parent=j;break;}
                    string name=node?node.name:"";
                    values.Add(new JObject{["index"]=i,["parent"]=parent,["name"]=name.Length>64?name[..64]:name});
                }
                var result=new JObject{["modelHash"]=model.AssetHash??"",["total"]=nodes.Count,["next"]=end<nodes.Count?end:-1,["nodes"]=values};
                return ProgramValue.Literal(result,OutputType(Schema()));
            },features:new[]{AquaticNavigationSchema.Feature});
    }
}
