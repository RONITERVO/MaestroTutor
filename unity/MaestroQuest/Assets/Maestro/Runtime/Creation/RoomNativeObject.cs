// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Ordinary reconciliation and unpublished activation candidates configure
        // the same native components. Identity registration is a separate step.
        void ConfigureNativeObject(RoomObjectData data,RoomItem item,RoomDocument document,RoomEditPreparation preparation,bool configure,int slot)
        {
            var environment=item.GetComponent<RoomEnvironmentBinding>()??item.gameObject.AddComponent<RoomEnvironmentBinding>();
            item.WaterTraversal=RoomWaterTraversal.Effective(data);
            environment.Apply(PhysicsWorld,item,string.IsNullOrEmpty(data.environmentProfile)?true:journal.ReadEnvironment(data.environmentProfile).realCollisions);
            if(configure) {
                item.GetComponent<RecipeObject>()?.ConfigureAppearanceBindings(data.appearanceBindings);
                item.GetComponent<CreatedRoomObject>()?.ApplyRecipe(data.recipe,preparation);
                item.GetComponent<CreatedRoomObject>()?.ApplyDrawing(data);
                item.GetComponent<CreatedRoomObject>()?.ApplyScanLayer(data);
                var surfaces=item.GetComponent<DrawingSurfaceView>();if(!surfaces&&(data.surfaces?.Length??0)>0)surfaces=item.gameObject.AddComponent<DrawingSurfaceView>();if(surfaces)surfaces.Apply(data.surfaces);
                if(ScanDrawingAnchor.Has(data)){var layer=item.GetComponent<ScannedDrawingView>()??item.gameObject.AddComponent<ScannedDrawingView>();layer.Apply(this,data);}
                var tip=item.GetComponent<DrawingTipView>();if(!tip&&(data.drawingTips?.Length??0)>0)tip=item.gameObject.AddComponent<DrawingTipView>();if(tip)tip.Apply(this,data.id,data.drawingTips);
                var liquid=item.GetComponent<ContainerFillView>();if(!liquid&&(data.containers?.Length??0)>0)liquid=item.gameObject.AddComponent<ContainerFillView>();if(liquid)liquid.Apply(data.containers);
                var field=ApplyHeightFields(data.id,item,data.heightFields,preparation);
                var sculpt=item.GetComponent<SculptTipView>();if(!sculpt&&(data.sculptTips?.Length??0)>0)sculpt=item.gameObject.AddComponent<SculptTipView>();if(sculpt)sculpt.Apply(this,data.id,data.sculptTips);
                var materialContents=item.GetComponent<MaterialToolContentsView>();if(!materialContents&&data.sculptTips?.Any(t=>t.IsMaterial)==true)materialContents=item.gameObject.AddComponent<MaterialToolContentsView>();if(materialContents)materialContents.Apply(data.sculptTips?.FirstOrDefault(),data.materialStores?.FirstOrDefault());
                item.GetComponent<CreatedRoomObject>()?.ApplyModelGeometry(data.modelGeometry);
                item.GetComponent<CreatedRoomObject>()?.ApplyCollision(data.collision,preparation);
                item.GetComponent<CreatedRoomObject>()?.SetCollisionShape(data.collisionShape,field!=null);
                item.GetComponent<RigidRoomItem>()?.Configure(PhysicsWorld,data.physics,data.mass);
                item.GetComponent<MaestroAvatar>()?.SetSavedPose(data.joints);
                var avatar = item.GetComponent<MaestroAvatar>(); if (avatar) { avatar.ConfigureActivityProfiles(ActivityProfiles,Motions); avatar.SetWalkReference(data.walkClip-1,data.walkMotionId,Motions); _ = avatar.SetModel(data.modelHash,Models); }
            }
            if (!data.IsBuiltIn)
            {
                item.SetHome(new Vector3(-.63f + (slot % 8) * .18f,.7f + ((slot / 8) % 4) * .18f,1.15f + (slot / 32) * .25f),Quaternion.identity,Vector3.one);
                item.GetComponent<CreatedRoomObject>().ApplyColor(data.appearanceBindings.Any(b=>b.kind=="root")?Color.white:data.color);
            }
            ApplyVisibility(data,item,document);
        }
    }
}
