// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Imports;
namespace Maestro.Quest.Creation
{
    // Admission owns prepared resources until the exact journal candidate has
    // been persisted/accepted and reconciliation takes them. Failed preparation
    // or storage releases all candidates and preserves the active scene.
    // Current consumers are loaded imported geometry; compound/terrain source
    // limits are validated by the candidate document. Regional activation and
    // allocation limits across other resource kinds remain separate work.
    internal sealed class RoomEditPreparation:IDisposable
    {
        readonly List<IDisposable> resources=new();
        internal static RoomEditPreparation TryCreate(RoomEditor editor,RoomJournal.PreparedEdit edit,out string error)
        {
            var prepared=new RoomEditPreparation();error=null;
            try {
                var changed=edit.ChangedObjects;
                foreach(var data in edit.Snapshot().objects) {
                    if(data.kind!=RoomObjectKind.ImportedModel||!changed.Contains(data.id))continue;
                    var before=editor.Read(data.id);
                    // New/unloaded model instances keep the established asynchronous
                    // import/readiness path. An edit of existing geometry must be
                    // prepared now; it cannot advance history and fail afterwards.
                    if(before==null||before.modelGeometry.Same(data.modelGeometry))continue;
                    if(editor.PhysicsWorld&&editor.PhysicsWorld.Running)throw new ModelImportException("Pause physics before changing imported geometry");
                    var item=editor.Find(data.id);var view=item?item.GetComponent<CreatedRoomObject>():null;
                    if(!view)throw new ModelImportException("Wait for the imported geometry to load");
                    if(!view.CanConfigureModelGeometry(data.modelGeometry,out error))throw new ModelImportException(error);
                    prepared.resources.Add(view.PrepareModelGeometry(data.modelGeometry));
                }
                return prepared;
            }catch(Exception exception){prepared.Dispose();error=exception is ModelImportException?exception.Message:"The room edit could not prepare its geometry; inspect the objects before retrying";return null;}
        }
        public void Dispose(){for(int i=resources.Count-1;i>=0;i--)resources[i].Dispose();resources.Clear();}
    }
}
