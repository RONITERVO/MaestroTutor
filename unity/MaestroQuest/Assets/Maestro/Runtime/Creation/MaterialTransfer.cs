// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>One measured transfer kernel for saved fields and carried stores; no shadow balance.</summary>
    internal static class MaterialTransfer {
        internal readonly struct Endpoint {
            public readonly RoomHeightField Field;public readonly RoomMaterialStore Store;public readonly Vector2 Centre;public readonly float Radius;
            Endpoint(RoomHeightField field,RoomMaterialStore store,Vector2 centre,float radius){Field=field;Store=store;Centre=centre;Radius=radius;}
            public static Endpoint Surface(RoomHeightField field,Vector2 centre,float radius)=>new(field,null,centre,radius);
            public static Endpoint Stored(RoomMaterialStore store)=>new(null,store,default,0);
            public double Amount=>Field!=null?Field.VolumeLitres:Store.amountLitres;
            public string Material=>Field!=null?Field.material:Store.material;
            public Color Color=>Field!=null?Field.color:Store.color;
            public bool Valid=>Field!=null?Field.Validate(out _)&&HeightFieldTransfer.FootprintValid(Field,Centre,Radius):Store!=null&&Store.Validate(out _);
            public Endpoint Copy()=>new(Field?.Copy(),Store?.Copy(),Centre,Radius);
            public void Accept(Endpoint candidate){if(Field!=null)Field.heights=candidate.Field.heights;else Store.amountLitres=candidate.Store.amountLitres;}
        }
        internal static bool Apply(Endpoint source,Endpoint destination,double requested,out HeightFieldTransfer.Result result,out string error){
            result=default;error="Choose valid distinct material endpoints, matching material and colour, and 0.001–8000 local litres";
            if(!source.Valid||!destination.Valid||!double.IsFinite(requested)||requested<.001||requested>8000||
                source.Field!=null&&ReferenceEquals(source.Field,destination.Field)||source.Store!=null&&ReferenceEquals(source.Store,destination.Store)||
                source.Material!=destination.Material||!source.Color.Equals(destination.Color))return false;
            // Fit on detached candidates. Even numeric refusal must preserve both inputs.
            var a=source.Copy();var b=destination.Copy();double amount=requested;
            if(a.Store!=null)amount=Math.Min(amount,a.Store.amountLitres);
            if(b.Store!=null)amount=Math.Min(amount,b.Store.capacityLitres-b.Store.amountLitres);
            error="The source or receiver has less than 0.001 transferable litre available";if(amount<.001)return false;
            if(a.Field!=null&&b.Field!=null){
                if(!HeightFieldTransfer.Apply(a.Field,a.Centre,a.Radius,b.Field,b.Centre,b.Radius,amount,out _,out error))return false;
            }else if(a.Field!=null){
                if(!HeightFieldTransfer.Extract(a.Field,a.Centre,a.Radius,amount,out double removed,out error))return false;
                b.Store.amountLitres+=removed;
            }else if(b.Field!=null){
                if(!HeightFieldTransfer.Deposit(b.Field,b.Centre,b.Radius,amount,out double added,out error))return false;
                a.Store.amountLitres-=added;
            }else{a.Store.amountLitres-=amount;b.Store.amountLitres+=amount;}
            double taken=source.Amount-a.Amount,given=b.Amount-destination.Amount;
            error="This transfer is below numeric resolution; neither endpoint changed";
            if(!a.Valid||!b.Valid||taken<=0||given<=0||taken>requested+HeightFieldTransfer.Tolerance(requested)||
                Math.Abs(given-taken)>HeightFieldTransfer.Tolerance(taken))return false;
            source.Accept(a);destination.Accept(b);result=new HeightFieldTransfer.Result(taken,given);error=null;return true;
        }
    }
}
