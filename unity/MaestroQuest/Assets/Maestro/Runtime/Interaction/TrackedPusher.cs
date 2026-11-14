// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Maestro.Quest.Interaction
{
    /// <summary>A small kinematic controller/hand volume pushes loose items without a rendered UI surface.</summary>
    public sealed class TrackedPusher : MonoBehaviour
    {
        public Transform Source;
        public XRRayInteractor Interactor;
        public BookControllerInput Input;
        Rigidbody body;
        SphereCollider sphere;
        readonly HashSet<Collider> ignored = new();
        readonly HashSet<Collider> next = new();
        readonly Collider[] overlaps=new Collider[64];
        RoomEditor editor;RoomContactInputs inputs;bool bound,observed;RoomPhysicsWorld world;Vector3 previous;float previousAt;
        string issue;
        bool Active=>isActiveAndEnabled&&Source&&Source.gameObject.activeInHierarchy&&Interactor&&Input&&Input.PhysicsWorld&&Input.PhysicsWorld.Running;
        bool Owned=>Active&&Input.Editor==editor&&editor&&editor.isActiveAndEnabled;
        internal JObject ObserveInputs(){
            if(!Active)return RoomContactInputs.State("inactive",false,"Tracking or physics is inactive");
            var result=inputs?.Observe()??RoomContactInputs.State("unavailable",false,issue);
            if(!sphere.enabled){result["ready"]=false;if((string)result["state"]!="pending")result["state"]="unavailable";if(issue!=null)result["reason"]=Imports.ImportObservation.Text(issue);}
            return result;
        }

        void Awake()
        {
            gameObject.layer = RoomPhysicsLayers.Controller;
            body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            sphere = gameObject.AddComponent<SphereCollider>(); sphere.radius = .04f; sphere.enabled = false;
        }
        void FixedUpdate()
        {
            var currentWorld=Input?Input.PhysicsWorld:null;
            if(currentWorld!=world){if(world)world.Changed-=PhysicsChanged;ResetContact();world=currentWorld;if(world)world.Changed+=PhysicsChanged;}
            if(!Active||!RoomRecipe.Finite(Source.position)||!Maestro.Quest.Creation.MotionFrame.ValidRotation(Source.rotation)){ResetContact();return;}
            var current=Input.Editor;
            if(current!=editor||inputs?.SourceChanged==true){ResetContact();editor=current;bound|=editor!=null;}
            if(bound&&!editor){ResetContact();issue="The contact world is unavailable";return;}
            UpdateIgnored();
            var position=Source.position;float now=Time.realtimeSinceStartup;
            bool discontinuity=!observed||now-previousAt>.25f||(position-previous).sqrMagnitude>.25f;
            var start=observed?previous:position;previous=position;previousAt=now;observed=true;
            if(discontinuity){inputs?.Dispose();inputs=null;DisableAtSource();issue="Waiting for a fresh continuous tracking pose";return;}
            float radius=sphere.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z));
            var sweep=new Bounds(start,Vector3.one*(radius*2+.004f));sweep.Encapsulate(new Bounds(position,Vector3.one*(radius*2+.004f)));
            if(editor){
                inputs??=new RoomContactInputs(editor,null,()=>Owned);
                if(!inputs.Require(sweep,out issue)){DisableAtSource();return;}
            }
            if(!Input.PhysicsWorld.SimulationActive){DisableAtSource();issue="The physics environment is unavailable";return;}
            if(!sphere.enabled){
                // A resumed or jumped contact must leave existing objects before
                // becoming solid; it cannot materialize inside and fling them.
                Physics.SyncTransforms();int count=Physics.OverlapSphereNonAlloc(position,radius,overlaps,1<<RoomPhysicsLayers.Item,QueryTriggerInteraction.Ignore);
                bool clear=count<overlaps.Length;
                for(int i=0;i<count;i++)if(overlaps[i]&&!next.Contains(overlaps[i]))clear=false;
                if(!clear){DisableAtSource();issue="Move the hand clear of objects to resume contact";return;}
                body.position=position;body.rotation=Source.rotation;sphere.enabled=true;UpdateIgnored();
            }else{body.MovePosition(position);body.MoveRotation(Source.rotation);}
            issue=null;
        }
        void DisableAtSource(){ResetIgnored();sphere.enabled=false;body.position=Source.position;body.rotation=Source.rotation;}
        void UpdateIgnored(){
            next.Clear();
            foreach(var selected in Interactor.interactablesSelected)foreach(var collider in selected.colliders)if(collider)next.Add(collider);
            if(!sphere.enabled){ResetIgnored();return;}
            foreach(var collider in ignored)if(collider&&!next.Contains(collider))Physics.IgnoreCollision(sphere,collider,false);
            foreach(var collider in next)if(!ignored.Contains(collider))Physics.IgnoreCollision(sphere,collider,true);
            ignored.Clear();ignored.UnionWith(next);
        }
        void ResetContact(){inputs?.Dispose();inputs=null;observed=false;issue=null;ResetIgnored();if(sphere)sphere.enabled=false;}
        internal void CancelTrackedInput()=>ResetContact();
        void OnDisable()=>ResetContact();
        void PhysicsChanged(){if(!world||!world.Running)ResetContact();}
        void OnDestroy(){ResetContact();if(world)world.Changed-=PhysicsChanged;}
        void ResetIgnored()
        {
            foreach (var collider in ignored) if (collider) Physics.IgnoreCollision(sphere,collider,false);
            ignored.Clear();
        }
    }
}
