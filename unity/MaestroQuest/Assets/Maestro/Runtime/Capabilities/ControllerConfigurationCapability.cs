// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ControllerConfigurationCapability:CapabilityModule
    {
        public override string Id=>"controller.configure";
        public override string Label=>"Configure controller bindings";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"controls.available","controls.configuration.current","storage.writable"};
        public override string Description=>"Edit the same saved controller preferences as the physical movement tray. Read controller.settings and supply its exact configurationId; every successful manual/shared save issues a new ID, as does a new room runtime. movement changes the two independent stick bindings, user's speed and dead zone together, preserving buttons. button changes only one of X, A or the two stick clicks, preserving other settings. program binds an exact existing readable saved program ID, never runs it; missing references are visible and can be replaced or cleared. B/Y, system controls and palm Recall are reserved and cannot be reassigned. A successful save resets input gates: sticks/buttons must return to neutral before acting. It never enables movement, enters Virtual view or starts a program. Existing enabled modes remain enabled unless their stick becomes none. Preferences save immediately even in temporary rooms; room Undo/Discard does not revert controller settings. To revert, read the new identity and explicitly restore the prior values. Duplicate receipts do not apply again. Saved bindings survive restart but movement remains opt-in; a receipt ID is not a current configuration ID.";
        static JObject Variant(string operation,string title,JObject fields)
        {
            var props=new JObject {["operation"]=Choice(operation),["configurationId"]=Text("^[a-f0-9]{32}$",32)};props["operation"]["x-static"]=true;
            foreach(var p in fields.Properties())props[p.Name]=p.Value.DeepClone();var schema=Object(props);schema["title"]=title;schema["x-features"]=new JArray("controllerConfiguration.v1");return CurrentInputs(schema,"controller.settings","configurationId",null,operation.StartsWith("movement.",StringComparison.Ordinal)?new[]{"deadZone","userSpeed"}:System.Array.Empty<string>());
        }
        static JObject Movement(string stick,string title,params string[] other)
        {
            return Variant("movement."+stick,title,new JObject {["userStick"]=Choice(other),["deadZone"]=Number(.1,.4),["userSpeed"]=Number(.2,1.2)});
        }
        static JObject Button(string command,string title)
        {
            var fields=new JObject {["button"]=Choice(MovementControls.ButtonIds)};
            if(command=="program")fields["programId"]=Text("^[a-fA-F0-9]{32}$",32);
            return Variant("button."+command,title,fields);
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Controller settings",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Movement("right","Maestro on right stick","left","none"),Movement("left","Maestro on left stick","right","none"),Movement("none","Maestro stick disabled","left","right","none"),
            Button("program","Bind a saved program"),Button("none","Clear a button"),Button("snapLeft","Bind left snap turn"),Button("snapRight","Bind right snap turn"))};
        public override JObject OutputSchema=>Object(new JObject {["configurationId"]=Text("^[a-f0-9]{32}$",32),["saved"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="movement.right",["configurationId"]=new string('0',32),["userStick"]="left",["deadZone"]=.2,["userSpeed"]=.65};
        static MovementControls Owner(CapabilityContext context)=>context.Editor?context.Editor.GetComponent<MovementControls>():null;
        static MovementStick Stick(string value)=>value=="left"?MovementStick.Left:value=="right"?MovementStick.Right:MovementStick.None;
        static ControllerPreferences Next(MovementControls controls,JObject args)
        {
            var next=controls.Preferences;
            string operation=(string)args["operation"];
            if(operation.StartsWith("movement.",StringComparison.Ordinal)){
                next.avatarStick=Stick(operation[9..]);next.userStick=Stick((string)args["userStick"]);next.deadZone=(float)args["deadZone"];next.userSpeed=(float)args["userSpeed"];
            }else{
                var command=operation[7..];next.buttons[Array.IndexOf(MovementControls.ButtonIds,(string)args["button"])]=new ControllerBinding {command=command=="program"?ControllerCommand.Sequence:command=="snapLeft"?ControllerCommand.SnapLeft:command=="snapRight"?ControllerCommand.SnapRight:ControllerCommand.None,sequenceId=command=="program"?(string)args["programId"]:null};
            }
            return next;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var owner=Owner(context);error="Controller settings are unavailable";return owner&&owner.ConfigurationInitialized&&owner.CanConfigure((string)args["configurationId"],Next(owner,args),out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var owner=Owner(context);
            if(!owner.Configure((string)args["configurationId"],Next(owner,args),out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"configurationId\":\"text\",\"storageReady\":\"boolean\",\"avatarStick\":\"text\",\"userStick\":\"text\",\"deadZone\":\"number\",\"userSpeed\":\"number\",\"buttons\":{\"list\":{\"record\":{\"button\":\"text\",\"command\":\"text\",\"programId\":\"text\",\"available\":\"boolean\"}}},\"live\":{\"record\":{\"avatarEnabled\":\"boolean\",\"userEnabled\":\"boolean\",\"virtualView\":\"boolean\"}}}}"));
            return new("controller.settings",type,"Controller settings and bindings","Accepted stick preferences and all four configurable buttons, plus live opt-in flags. storageReady false means the preserved preferences file is unavailable for editing; displayed preferences can be startup fallback defaults. Other runtime holds may also temporarily prevent writes. configurationId binds an edit to this exact accepted configuration/runtime; saves rotate it, rejected writes keep it, and restart issues a new ID. Read again before each edit. programId is an exact saved program ID; available reports readable program presence, not that all targets/assets are currently ready. Snap turns require user movement enabled and complete-frame clearance at any backdrop opacity; this read never enables anything. Settings persist immediately even during temporary room mode. System buttons, B/Y and palm Recall stay reserved.",null,null,(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<MovementControls>():null;var value=owner?owner.ObserveConfiguration():null;return value==null?null:ProgramValue.Literal(value,type);});
        }
    }
}
