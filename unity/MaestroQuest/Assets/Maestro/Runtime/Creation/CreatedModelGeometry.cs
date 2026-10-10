// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class CreatedRoomObject
    {
        RoomModelGeometry requestedModelGeometry=new(),appliedModelGeometry;
        ImportedCollisionGeometry importedCollision;
        bool importedObject,modelGeometryReady;
        ModelGeometryPreparation preparedGeometry;
        internal sealed class ModelGeometryPreparation:System.IDisposable
        {
            readonly CreatedRoomObject owner;
            internal readonly RoomModelGeometry Settings;
            ImportedCollisionGeometry collision;
            internal ModelGeometryPreparation(CreatedRoomObject owner,RoomModelGeometry settings){this.owner=owner;Settings=settings.Copy();if(settings.meshCollision)collision=new ImportedCollisionGeometry(owner.Model,settings.walkable,owner.collisionOwner);}
            internal ImportedCollisionGeometry Take(){var value=collision;collision=null;return value;}
            public void Dispose(){collision?.Dispose();collision=null;if(owner&&owner.preparedGeometry==this)owner.preparedGeometry=null;}
        }
        internal ModelGeometryPreparation PrepareModelGeometry(RoomModelGeometry settings)
        {
            if(preparedGeometry!=null)throw new ModelImportException("Another geometry edit is being prepared");
            return preparedGeometry=new ModelGeometryPreparation(this,settings);
        }
        internal bool ModelGeometryReady=>modelGeometryReady&&Model&&Model.Ready;
        internal string ModelGeometryIssue {get;private set;}="Wait for the imported geometry to load";
        internal bool CanConfigureModelGeometry(RoomModelGeometry settings,out string error)
        {
            error="Choose a loaded imported room object";
            if(!importedObject||!Model||!Model.Ready)return false;
            if(Model.IsPlaying){error="Stop the model's animation before changing geometry";return false;}
            return Model.CanApplyGeometry(settings,out _,out error);
        }
        internal void ApplyModelGeometry(RoomModelGeometry settings)
        {
            if(!importedObject)return;
            requestedModelGeometry=settings.Copy();
            if(!Model||!Model.Ready)return;
            if(modelGeometryReady&&appliedModelGeometry?.Same(settings)==true)return;
            ImportedCollisionGeometry candidate=null;
            try
            {
                if(!Model.CanApplyGeometry(settings,out _,out var error))throw new ModelImportException(error);
                if(settings.meshCollision){var body=GetComponent<Rigidbody>();if(body&&!body.isKinematic){GetComponent<RigidRoomItem>().StopVelocity();body.isKinematic=true;}}
                candidate=preparedGeometry!=null&&preparedGeometry.Settings.Same(settings)?preparedGeometry.Take():settings.meshCollision?new ImportedCollisionGeometry(Model,settings.walkable,collisionOwner):null;
                if(!Model.ApplyGeometry(settings,out error))throw new ModelImportException(error);
                var old=importedCollision;importedCollision=candidate;candidate=null;old?.SetActive(false);
                var box=(BoxCollider)originalCollider;box.transform.localScale=Vector3.one;box.center=Model.LocalBounds.center;box.size=Model.LocalBounds.size+Vector3.one*.02f;
                box.GetComponent<Renderer>().enabled=false;geometryBounds=Model.LocalBounds;
                bool selected=selection&&selection.activeSelf;if(selection){selection.SetActive(false);Destroy(selection);}BuildSelection(geometryBounds);SetSelected(selected);
                appliedModelGeometry=settings.Copy();modelGeometryReady=true;ModelGeometryIssue="";
                SetCollisionShape(collisionShape,true);old?.Dispose();
                GetComponent<RigidRoomItem>().SetGeometryReady(true);ModelStatus="Model ready";
                ApplyColor(tint);GetComponent<RoomAppearanceView>()?.Refresh();
            }
            catch(System.Exception error)
            {
                candidate?.Dispose();modelGeometryReady=false;ModelGeometryIssue=error is ModelImportException?error.Message:"Imported collision geometry could not be prepared";
                ModelStatus=ModelGeometryIssue;SetCollisionShape(collisionShape,true);GetComponent<RigidRoomItem>().SetGeometryReady(false);
            }
        }
    }
}
