// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        // The exact authored blueprint from the full native learner journey.
        // Primitive-only fixtures missed the small recipe body's endpoint.
        [UnityTest]public IEnumerator WaterFollowingRecipeShoreEndpointAfterPublicRoundTrip(){
            editor.Liquids.enabled=false;
            var ex=new RoomAgentExecutor(editor);
            var create=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/shore-following-recipe.json")));
            yield return ContainerAction(ex,create);
            var ids=(JArray)ex.Executions.Observe()["selected"]["output"]["objectIds"];
            string pool=(string)ids[0],id=(string)ids[1],leader=(string)ids[2];
            var c=new RoomContainer{version=3,height=.5f,amountMl=225000,capacityMl=1};
            yield return ContainerAction(ex,ContainerCall(pool,c));
            Assert.That(editor.ConfigureWaterTraversal(id,editor.ObjectRevision(id),new(){mode="avoid",maxDepthMetres=.02f},out var error),Is.True,error);
            root.GetComponent<RoomInteraction>().Viewer=leftAnchor.transform;leftAnchor.transform.position=new(0,1.6f,3);
            Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic aligned terrain room");physics.StartPhysics();
            var actor=editor.Find(id);
            foreach(var point in new[]{new Vector3(-4.75f,0,2),new Vector3(-3.25f,.55f,2)}){
                var call=ShoreCall(id,pool,point,point.y==0);call["arguments"]["speed"]=.4;
                yield return ShoreCompleted(ex,ContainerRequest(call),actor);
            }
            var args=WaterFollowArgs(id,pool,leader,"shore",7);args["speed"]=.4;
            var followCall=new JObject{["id"]="object.follow",["version"]=1,["arguments"]=args};
            var request=ContainerRequest(followCall);Assert.That(ex.Execute(request,out error,out _),Is.True,error);
            var previous=actor.transform.position;float until=Time.realtimeSinceStartup+15;bool observed=false;
            while(Time.realtimeSinceStartup<until){
                var receipt=ex.Executions.Observe()["selected"];
                var fact=ShoreMovement.Observe(editor,actor);if(fact!=null){observed=true;Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));}
                Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThan(.022f),"The .4 m/s actor keeps its step budget on this continuous slope");previous=actor.transform.position;
                Assert.That((string)receipt["phase"],Is.Not.EqualTo("failed"),receipt+" position="+actor.transform.position.ToString("F6"));
                if((string)receipt["phase"]=="completed"){
                    Assert.That(observed,Is.True);Assert.That((string)receipt["output"]["state"],Is.EqualTo("keepingDistance"));
                    Assert.That((float)receipt["output"]["distance"],Is.GreaterThan(1));
                    Assert.That(actor.transform.position.x,Is.EqualTo(-4.65f).Within(.06f));Assert.That(actor.transform.position.z,Is.EqualTo(2).Within(.01f));
                    Assert.That(editor.Read(id).waterTraversal.mode,Is.EqualTo("avoid"));
                    var held=actor.transform.position;Assert.That(ex.Execute(request,out error,out _),Is.True,error);yield return null;
                    Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(RoomLocomotionBudget.Count,Is.Zero);yield break;
                }
                yield return null;
            }
            Assert.Fail(ex.Executions.Observe().ToString());
        }
    }
}
