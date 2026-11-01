// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Measured carried material. Presentation, mass and physical transport are separate components.</summary>
    [Serializable] public sealed class RoomMaterialStore {
        public const int MaximumPerRoom=16;
        public const double MaximumLitres=8000;
        public int version=1;
        public double capacityLitres=1,amountLitres;
        public string material="Snow";
        public Color color=new(.94f,.97f,1,1);
        public RoomMaterialStore Copy()=>new(){version=version,capacityLitres=capacityLitres,amountLitres=amountLitres,material=material,color=color};
        public bool Validate(out string error){
            error="Use a version-1 material store, capacity 0.001–8000 litres, contents within capacity, a stable material label and opaque colour";
            if(version!=1||!double.IsFinite(capacityLitres)||capacityLitres<.001||capacityLitres>MaximumLitres||!double.IsFinite(amountLitres)||amountLitres<0||amountLitres>capacityLitres||!RoomSnapPoint.Identifier(material)||material.Any(char.IsControl)||!RoomRecipe.ValidColor(color))return false;
            error=null;return true;
        }
        public static bool ValidateCollection(RoomObjectData owner,out string error){
            var stores=owner.materialStores??Array.Empty<RoomMaterialStore>();error="Use one measured material store on a created object";
            if(stores.Length>1||owner.IsBuiltIn&&stores.Length>0||stores.Any(s=>s==null))return false;
            if(stores.Length==1)return stores[0].Validate(out error);error=null;return true;
        }
    }
}
