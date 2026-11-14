// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Interaction {
    internal sealed class WaterFollowing:FollowingOperation {
        readonly JObject arguments;
        static JObject Navigation(RoomEditor editor,RoomItem item,JObject args){
            var point=editor.Frame.PointToRoom(item.transform.position); // Retargeted after Begin; every destination uses authored metres.
            var goal=new JObject{["x"]=point.x,["y"]=point.y,["z"]=point.z};
            if((string)args["mode"]=="shore")goal["kind"]="ground";
            if((string)args["mode"]=="surface")goal.Remove("y");
            var value=new JObject{["target"]=args["target"].DeepClone(),["mode"]=args["mode"].DeepClone(),["habitat"]=args["habitat"].DeepClone(),
                ["destination"]=goal,["speed"]=args["speed"].DeepClone(),["facing"]=args["facing"].DeepClone()};
            if(args["immersion"]!=null)value["immersion"]=args["immersion"].DeepClone();
            if(args["gait"]!=null)value["gait"]=args["gait"].DeepClone();
            return value;
        }
        static IFollowingMotion Motion(RoomEditor editor,RoomItem item,JObject args){
            var navigation=Navigation(editor,item,args);
            return (string)args["mode"]=="shore"?new ShoreMovement(editor,item,navigation,true,(float)args["distance"]):
                new AquaticMovement(editor,item,navigation,following:true);
        }
        internal WaterFollowing(RoomEditor editor,RoomItem item,JObject args):
            base(editor,item,FollowObjectCapability.Leader(args),(float)args["distance"],(float)args["seconds"],Motion(editor,item,args),(string)args["mode"]=="swim"){
            arguments=(JObject)args.DeepClone();
        }
        protected override bool CanStart(out string error)=>CanBegin(editor,item,arguments,out error);
        internal static bool CanBegin(RoomEditor editor,RoomItem item,JObject args,out string error){
            if(!LeaderAvailable(editor,item,FollowObjectCapability.Leader(args),out error))return false;
            var navigation=Navigation(editor,item,args);
            return (string)args["mode"]=="shore"?ShoreMovement.CanBegin(editor,item,navigation,out error):AquaticMovement.CanBegin(editor,item,navigation,out error);
        }
    }
}
