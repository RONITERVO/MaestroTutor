// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    // The native importer and pre-instantiation metadata use one normalization.
    internal readonly struct ModelGeometryLayout
    {
        internal const float MaximumSize=12.5f;
        internal readonly Bounds Bounds;
        internal readonly float Factor;
        internal readonly Vector3 Position;
        ModelGeometryLayout(Bounds bounds,float factor,Vector3 position){Bounds=bounds;Factor=factor;Position=position;}
        internal static bool ValidSource(Bounds bounds)
        {
            var size=bounds.size;float maximum=Mathf.Max(size.x,size.y,size.z);
            return RoomRecipe.Finite(bounds.center)&&RoomRecipe.Finite(size)&&size.x>=0&&size.y>=0&&size.z>=0&&maximum>=.00001f&&maximum<=10000;
        }
        internal static bool TryCreate(Bounds source,Quaternion orientation,RoomModelGeometry settings,out ModelGeometryLayout layout,out string error)
        {
            layout=default;error="Invalid model scale, pivot or collision settings";
            if(settings==null||!settings.Valid)return false;
            error="The model has invalid dimensions. Apply transforms and export it again.";
            if(!ValidSource(source))return false;
            float factor=settings.scaleMode=="source"?settings.metresPerUnit:.35f/Mathf.Max(source.size.x,source.size.y,source.size.z);
            var size=source.size*factor;
            error="The configured model must fit within 12.5 metres before the object's own scale; use a smaller source scale or split the environment";
            if(!RoomRecipe.Finite(size)||Mathf.Max(size.x,size.y,size.z)>MaximumSize)return false;
            var x=orientation*(Vector3.right*size.x);var y=orientation*(Vector3.up*size.y);var z=orientation*(Vector3.forward*size.z);
            size=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            var centre=settings.pivot=="source"?orientation*source.center*factor:settings.pivot=="base"?Vector3.up*size.y*.5f:Vector3.zero;
            error="The source pivot is too far from the model; choose center or base";
            if(!RoomRecipe.Finite(centre)||centre.magnitude>MaximumSize)return false;
            var position=centre-orientation*source.center*factor;
            if(!RoomRecipe.Finite(position))return false;
            layout=new(new Bounds(centre,size),factor,position);error=null;return true;
        }
    }
}
