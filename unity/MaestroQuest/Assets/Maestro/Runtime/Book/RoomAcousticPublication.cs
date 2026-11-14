// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Book
{
    public sealed partial class RoomAcoustics
    {
        AcousticGeometryCatalog geometryCatalog;
        // Explicit control-thread read. Availability is room lifetime, not a
        // dependency on one SDK's context, upload success or reflection worker.
        internal AcousticGeometryCatalog DescribeGeometry()
        {
            if(!this||!isActiveAndEnabled||paused||!focused)return AcousticGeometryCatalog.Unavailable;
            int pending=0;foreach(bool boundary in preparingGeometry.Values)if(boundary)pending++;
            bool same=geometryCatalog!=null&&geometryCatalog.Count==entries.Count&&geometryCatalog.PendingBoundaries==pending;
            if(same)foreach(var pair in entries)
                if(!geometryCatalog.Matches(pair.Value.GeometryKey,pair.Key)){same=false;break;}
            if(same)return geometryCatalog;
            var bindings=new AcousticGeometryBinding[entries.Count];int index=0;
            foreach(var pair in entries)bindings[index++]=new AcousticGeometryBinding(pair.Value.GeometryKey,pair.Key);
            return geometryCatalog=new AcousticGeometryCatalog(true,pending,bindings);
        }
    }
}
