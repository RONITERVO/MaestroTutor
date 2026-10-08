// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Interaction
{
    /// <summary>Gravity uses the explicitly selected physical or authored environment and an explicit Start.</summary>
    public sealed partial class RoomPhysicsWorld : MonoBehaviour
    {
        public bool SurfacesReady { get; private set; }
        public bool Running { get; private set; }
        public string Status { get; private set; } = "Load or scan your room to use gravity";
        public event Action Changed;
        public Func<Vector3, bool> Contains;
        bool paused,focused=true;
        string stateId=Guid.NewGuid().ToString("N");
        bool Active=>!paused&&focused&&isActiveAndEnabled;
        void Notify(){observedReady=AnySimulationReady;stateId=Guid.NewGuid().ToString("N");Changed?.Invoke();}
        string IdleStatus=>AnySimulationReady?"Physics paused — Start resumes without old throw speeds":RealCollisions?"Load or scan your room to use gravity":"Add accepted virtual ground before starting physics";
        RoomRuntimeGate runtimeGate;
        internal bool RuntimeHeld=>runtimeGate?.Held==true;
        internal string RuntimeHoldReason=>runtimeGate?.Reason;
        internal void ConfigureRuntime(RoomRuntimeGate gate){if(runtimeGate==gate)return;if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;runtimeGate=gate;if(gate!=null)gate.Changed+=RuntimeChanged;RuntimeChanged();}
        void RuntimeChanged(){if(runtimeGate?.Held==true){Running=false;Status=runtimeGate.Reason;}else if(!Running)Status=IdleStatus;Notify();}
        public void SetSurfaces(bool ready, string message)
        {
            SurfacesReady = ready;
            if (!AnySimulationReady) Running = false;
            Status=Running?"Physics on — some objects may be waiting for their ground":SimulationReady?message:IdleStatus; Notify();
        }
        public void StartPhysics() => SetRunning(true,out _);
        public void PausePhysics() => SetRunning(false,out _);
        bool CanRun(bool running,out string error)
        {
            error=null;
            if(running&&runtimeGate?.Held==true)error=runtimeGate.Reason;
            else if(running&&!Active)error="Return to the active room before starting physics";
            else if(running&&!AnySimulationReady)error=RealCollisions?"Load the room scan and check its alignment first":"Add accepted virtual ground before starting physics";
            return error==null;
        }
        public bool SetRunning(bool running,out string status)
        {
            if(!CanRun(running,out status)){Status=status;Notify();return false;}
            Running=running;Status=running?"Physics on — grip to pick up, release to throw":IdleStatus;
            status=Status;Notify();return true;
        }
        internal JObject ObserveSimulation()
        {
            bool canStart=CanRun(true,out var reason);
            return new JObject {["stateId"]=stateId,["ready"]=AnySimulationReady,["running"]=Running,["active"]=Active,["held"]=runtimeGate?.Held==true,
                ["canStart"]=canStart,["status"]=ImportObservation.Text(Status),["reason"]=ImportObservation.Text(reason)};
        }
        internal bool CanSetSimulation(string expected,bool running,out string error)
        {
            error="Room physics changed; read physics.simulation again before changing it";
            return expected==stateId&&CanRun(running,out error);
        }
        internal bool SetSimulation(string expected,bool running,out JObject result,out string error)
        {
            result=null;if(!CanSetSimulation(expected,running,out error))return false;
            // An already satisfied shared request does not reset physical observations.
            // Manual Pause still invalidates earlier intents even when already paused.
            if(Running!=running&&!SetRunning(running,out error))return false;
            result=ObserveSimulation();return true;
        }
        internal bool EnvironmentActive=>runtimeGate?.Held!=true&&Active;
        internal bool SimulationActive=>EnvironmentActive&&Running;
        public bool CanSimulate(Vector3 position,RoomItem item=null) => SimulationActive&&ContainsSimulation(position,item);
        void OnApplicationPause(bool value) { paused=value;if (paused) PausePhysics();else Notify(); }
        void OnApplicationFocus(bool value) { focused=value;if (!focused) PausePhysics();else Notify(); }
        void OnEnable()=>Notify();
        void OnDisable() => PausePhysics();
        void OnDestroy(){if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;}
    }
}
