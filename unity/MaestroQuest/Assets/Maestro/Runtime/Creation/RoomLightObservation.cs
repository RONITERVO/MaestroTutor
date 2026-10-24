// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Lighting influence is not occupied geometry. Keep its conservative envelope
    // separate from the saved visual/collision bounds exposed by spatial facts.
    internal static class RoomLightObservation
    {
        internal static float AttachmentReach(RoomRecipe recipe,string part)
        {
            float reach=0;int remaining=32;
            while(!string.IsNullOrEmpty(part)){
                if(recipe?.parts==null||remaining--==0)return float.NaN;
                RecipePart found=null;foreach(var candidate in recipe.parts)if(candidate.id==part){found=candidate;break;}
                if(found==null)return float.NaN;reach+=found.position.magnitude;part=found.parent;
            }
            return reach;
        }
        internal static bool Intersects(RoomSpatialExtent influence,RoomSpatialExtent geometry)=>
            !influence.Known||!geometry.Known||influence.HasBounds&&geometry.HasBounds&&influence.Bounds.Intersects(geometry.Bounds);
    }
    public sealed partial class RoomEditor
    {
        readonly List<Renderer> lightingRenderers=new();
        RoomSpatialExtent LightInfluence(string target,RoomLightRuntime state)
        {
            if(!Frame.Valid)return default;
            var source=state.Source;var centre=source.position;float radius=source.range;
            if(source.part!=""){
                // Recipe attachment transforms rotate but never scale with mesh size.
                // Sum of ancestor offsets contains every saved/idle animated pose.
                centre=Vector3.zero;radius+=source.position.magnitude+state.AttachmentReach;
            }
            if(!float.IsFinite(radius))return default;
            var extent=RoomSpatialExtent.Empty;extent.Include(new Bounds(centre,Vector3.one*(radius*2)),Matrix4x4.identity);
            if(state.ScanPlacement!=null){
                // The physical anchor, not a retired or saved root pose, owns
                // this lamp. An unmatched layer is intentionally hidden.
                return state.ScanPlacement.TryProject(this,new RoomSpatialBounds{Visual=extent},out var projected)
                    ?projected.Visual.Transform(transform.localToWorldMatrix):RoomSpatialExtent.Empty;
            }
            Matrix4x4 placement;
            if(dormantNative.TryGetValue(target,out var dormant)){
                ReadDormantSpatial(target,dormant,out _,out _,out _);
                placement=transform.localToWorldMatrix*dormant.SpatialPlacement;
            }else{
                var item=Find(target);if(!item||!item.isActiveAndEnabled)return default;
                placement=item.transform.localToWorldMatrix;
            }
            return extent.Transform(placement);
        }
        RoomSpatialExtent LightCasterBounds(string target)
        {
            if(dormantNative.ContainsKey(target))return DormantCasterBounds(ReadDormantVisual(target));
            var item=Find(target);if(item&&item.TryGetComponent<ScannedDrawingView>(out var ink)&&!ink.Visible)return RoomSpatialExtent.Empty;
            if(!item||!item.isActiveAndEnabled)return default;
            // A loading import's small placeholder cannot prove that its final
            // shadow geometry is outside the light. Unknown keeps preparation honest.
            var created=item.GetComponent<CreatedRoomObject>();var avatar=item.GetComponent<Avatar.MaestroAvatar>();
            if(journal.ImportedEntity(target)&&(!created||!created.ModelGeometryReady)||avatar&&avatar.ModelBusy)return default;
            var result=RoomSpatialExtent.Empty;item.GetComponentsInChildren(false,lightingRenderers);
            foreach(var renderer in lightingRenderers){
                if(!renderer||!renderer.enabled||renderer.forceRenderingOff||renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off||renderer.gameObject.layer==Interaction.RoomPhysicsLayers.Scanned)continue;
                if(renderer is SkinnedMeshRenderer){
                    if(!renderer.TryGetComponent<Imports.ImportedSkinBounds>(out var skin)){result.Known=false;continue;}
                    var skinBounds=skin.Read(Matrix4x4.identity,includeRest:true);
                    if(!skinBounds.Known)result.Known=false;else if(skinBounds.HasBounds)result.Include(skinBounds.Bounds,Matrix4x4.identity);
                }else result.Include(renderer.bounds,Matrix4x4.identity);
            }
            lightingRenderers.Clear();return result;
        }
        RoomSpatialExtent DormantCasterBounds(DormantVisual candidate){
            if(candidate.Prepared&&!candidate.Placed)return RoomSpatialExtent.Empty;
            return (candidate.Spatial.VisualLayers&~(1<<Interaction.RoomPhysicsLayers.Scanned))==0
                ?RoomSpatialExtent.Empty:candidate.Spatial.Visual.Transform(transform.localToWorldMatrix);
        }
        // Same result feeds retention, predictive display admission and captures.
        // A spot uses its enclosing sphere: conservative demand, not a visibility
        // or shadow guarantee. Disabled controls do not demand lighting inputs.
        HashSet<string> LightViewDependencies(IReadOnlyList<RoomObservationView> views)
        {
            var required=new HashSet<string>(StringComparer.Ordinal);
            if(journal==null||views.Count==0||lightRuntime.Count==0)return required;
            var shadows=new List<RoomSpatialExtent>();
            foreach(var pair in lightRuntime){
                var state=pair.Value;if(!state.Enabled||state.Intensity<=0)continue;
                var influence=LightInfluence(pair.Key.target,state);
                var spatial=new RoomSpatialBounds{Visual=influence,VisualLayers=~(1<<Interaction.RoomPhysicsLayers.Scanned)};
                bool observed=false;foreach(var view in views)if(view.Includes(spatial)){observed=true;break;}
                if(!observed)continue;required.Add(pair.Key.target);
                if(state.Source.shadows=="hard")shadows.Add(influence);
            }
            if(shadows.Count>0){
                foreach(var candidate in QueryDormantShadows(shadows)){
                    var bounds=DormantCasterBounds(candidate);
                    foreach(var influence in shadows)if(RoomLightObservation.Intersects(influence,bounds)){required.Add(candidate.Id);break;}
                }
                // Live/skinned geometry, including unexpectedly missing roots,
                // remains fresh. A dormant snapshot is never a live shadow pose.
                foreach(var target in CurrentVisualTargets()){
                    var bounds=LightCasterBounds(target);
                    foreach(var influence in shadows)if(RoomLightObservation.Intersects(influence,bounds)){required.Add(target);break;}
                }
            }
            return required;
        }
        void VisitViewDependencies(IReadOnlyList<RoomObservationView> views,Action<string> retain)
        {
            VisitObservedNative(views,retain,includePreparedEnvelope:true);foreach(var target in LightViewDependencies(views))retain(target);
        }
        bool LightingViewLoading(Camera camera)
        {
            var needed=LightViewDependencies(new[]{new RoomObservationView(camera,sourceLayers:-1)});
            foreach(var target in needed){
                if(NativeEntityDormant(target))return true;
                var item=Find(target);if(!item||!item.isActiveAndEnabled)throw new InvalidOperationException("Required lighting or shadow geometry is unavailable");
                var created=item.GetComponent<CreatedRoomObject>();var avatar=item.GetComponent<Avatar.MaestroAvatar>();
                if(avatar&&avatar.ModelBusy)return true;
                if(journal.ImportedEntity(target)&&(!created||!created.ModelGeometryReady)){
                    if(!created)throw new InvalidOperationException("Required shadow model is unavailable");
                    if(!created.ModelLoading&&!string.IsNullOrEmpty(created.ModelGeometryIssue))throw new InvalidOperationException("Required shadow geometry could not load: "+created.ModelGeometryIssue);
                    return true;
                }
            }
            foreach(var pair in lightRuntime){
                var state=pair.Value;if(!needed.Contains(pair.Key.target)||!state.Enabled||state.Intensity<=0)continue;
                var item=Find(pair.Key.target);var lamp=item?item.GetComponent<RoomLightView>():null;
                if(!lamp||!lamp.Active(pair.Key.light))throw new InvalidOperationException("Required native lamp is unavailable");
            }
            return false;
        }
    }
}
