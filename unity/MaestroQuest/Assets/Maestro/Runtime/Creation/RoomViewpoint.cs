// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>An authored floor position and horizontal heading, never a physical anchor.</summary>
    [Serializable]
    public sealed class RoomViewpoint
    {
        public int version=1;
        public bool active;
        public Vector3 position;
        public float yaw;
        public RoomViewpoint Copy()=>new(){version=version,active=active,position=position,yaw=yaw};
        internal static bool ValidPosition(Vector3 value)=>RoomRecipe.Finite(value)&&value.sqrMagnitude<=625;
        public bool Valid=>version==1&&ValidPosition(position)&&
            float.IsFinite(yaw)&&yaw>=-180&&yaw<=180&&(active||position==Vector3.zero&&yaw==0);
        internal bool Same(RoomViewpoint other)=>other!=null&&active==other.active&&
            (position-other.position).sqrMagnitude<.0001f&&Mathf.Abs(Mathf.DeltaAngle(yaw,other.yaw))<.1f;
        // Require the wire fields explicitly: Unity's field initializers must not
        // make a truncated current save look like an intentionally fresh location.
        internal static bool ValidWire(JObject room)
        {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            bool legacy=(int)room["version"]<22;
            if(legacy&&room["viewpoint"]==null)return true;
            if(room["viewpoint"] is not JObject view||view.Count!=4||view["version"]?.Type!=JTokenType.Integer||
                view["active"]?.Type!=JTokenType.Boolean||view["position"] is not JObject point||point.Count!=3)return false;
            if(!Number(view["yaw"])||!Number(point["x"])||!Number(point["y"])||!Number(point["z"]))return false;
            return !legacy||(int)view["version"]==1&&!(bool)view["active"]&&(double)view["yaw"]==0&&
                (double)point["x"]==0&&(double)point["y"]==0&&(double)point["z"]==0;
        }
        static bool Number(JToken token)=>token?.Type is JTokenType.Integer or JTokenType.Float;
    }
    public sealed partial class RoomJournal
    {
        RoomViewpoint viewpoint=new();
        internal RoomViewpoint Viewpoint=>viewpoint.Copy();
        internal bool UpdateViewpoint(RoomViewpoint value)
        {
            if(value==null||!value.Valid||viewpoint.Same(value))return false;
            viewpoint=value.Copy();return true;
        }
    }
    public sealed partial class RoomEditor
    {
        internal RoomViewpoint Viewpoint=>journal.Viewpoint;
        internal void RememberViewpoint(RoomViewpoint value)
        {
            using var write=WriteGate.TryWrite(out _);
            if(write==null||journal==null||storage==null||storage.ReadOnly||!journal.UpdateViewpoint(value))return;
            // Viewer motion is personal navigation, not an authored entity edit:
            // do not churn the shared scene revision or create geometry Undo.
            if(!TemporaryRoom){if(!dirty)saveAt=Time.unscaledTime+.5f;dirty=true;}
        }
    }
}
