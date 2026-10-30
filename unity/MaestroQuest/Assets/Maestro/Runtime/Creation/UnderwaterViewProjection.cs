// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Bounded per-workspace shader projection. The saved/live water and actual
    // terrain mesh remain authoritative; this owns no quantity or contacts.
    internal sealed class UnderwaterViewProjection:IDisposable {
        internal const int Maximum=RoomContainer.MaximumPerRoom;
        internal readonly LiquidMediumGeometry[] Media=new LiquidMediumGeometry[Maximum];
        readonly Matrix4x4[] inverse=new Matrix4x4[Maximum];
        readonly Vector4[] shape=new Vector4[Maximum],plane=new Vector4[Maximum],optics=new Vector4[Maximum];
        readonly TerrainLiquidGeometry[] beds=new TerrainLiquidGeometry[Maximum];
        readonly float[] heights=new float[Maximum*17*17];
        readonly Texture2D texture;
        internal int Count {get;private set;}
        internal bool Supported=>texture;
        internal UnderwaterViewProjection(){
            if(!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))return;
            texture=new Texture2D(17,17*Maximum,TextureFormat.RFloat,false,true){name="Finite underwater terrain",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            texture.SetPixelData(heights,0);texture.Apply(false,false);
        }
        internal void Upload(int count){
            Count=texture?Mathf.Clamp(count,0,Maximum):0;bool dirty=false;
            for(int i=Count;i<Maximum;i++){Media[i]=default;beds[i]=null;}
            if(Count==0)return;
            for(int i=0;i<Count;i++){
                var m=Media[i];var c=m.Contents;var view=UnderwaterViewSettings.Effective(c.underwater);
                inverse[i]=m.ToLocal;plane[i]=m.LocalSurface;
                shape[i]=new Vector4(c.IsRectangular?c.rectangle.width*.5f:c.radius,c.IsRectangular?c.rectangle.depth*.5f:c.radius,c.height,m.Terrain?.OpticalCells??(c.IsRectangular?1:0));
                var color=view.LinearTint;float opacity=m.Item.GetComponent<RoomAppearanceView>()?.OpticalOpacity??1;
                optics[i]=new Vector4(color.r,color.g,color.b,view.enabled?view.densityPerMetre*opacity:0);
                if(ReferenceEquals(beds[i],m.Terrain))continue;
                beds[i]=m.Terrain;if(beds[i]!=null)beds[i].CopyOpticalHeights(heights,i*17*17);else Array.Clear(heights,i*17*17,17*17);dirty=true;
            }
            if(dirty){texture.SetPixelData(heights,0);texture.Apply(false,false);}
            Shader.SetGlobalMatrixArray("_MaestroWaterInverse",inverse);Shader.SetGlobalVectorArray("_MaestroWaterShape",shape);
            Shader.SetGlobalVectorArray("_MaestroWaterPlane",plane);Shader.SetGlobalVectorArray("_MaestroWaterOptics",optics);
            Shader.SetGlobalTexture("_MaestroWaterHeights",texture);
        }
        public void Dispose(){ArtResources.Release(texture);Count=0;}
    }
    public sealed partial class LiquidPouring {
        internal bool CaptureUnderwater(UnderwaterViewProjection projection){
            bool enabled=false;foreach(var v in vessels.Values){var settings=UnderwaterViewSettings.Effective(v.Live.underwater);if(settings.enabled&&settings.densityPerMetre>0){enabled=true;break;}}
            if(!enabled){projection.Upload(0);return true;}
            CaptureMedia(true);if(mediumQueryIssue!=null||mediaCount>UnderwaterViewProjection.Maximum)return false;
            int count=0;
            for(int i=0;i<mediaCount;i++){
                var m=media[i];if((m.Item.GetComponent<RoomAppearanceView>()?.OpticalOpacity??1)<=0)continue;
                // Disabled inner bodies still contain the viewpoint and must not
                // silently borrow an enabled outer body's view.
                int at=count;while(at>0&&Preferred(m,projection.Media[at-1])){projection.Media[at]=projection.Media[at-1];at--;}
                projection.Media[at]=m;count++;
            }
            projection.Upload(count);return true;
        }
        internal bool UnderwaterCameraReady(Camera camera){
            var bounds=new Bounds(camera.transform.position,Vector3.zero);
            if(camera.stereoEnabled){
                bounds.Encapsulate((Vector3)camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3));
                bounds.Encapsulate((Vector3)camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3));
            }
            return CapturedMediumAvailability(bounds).Ready;
        }
    }
}
