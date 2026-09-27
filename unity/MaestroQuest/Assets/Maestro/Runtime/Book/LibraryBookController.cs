// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Book
{
    [Serializable] public sealed class LibraryBookRequest
    {
        public int version, sequence, offset, stepIndex, sourceIndex, termsPage, role, weight, usagePage;
        public float speed,cooldown;
        public string session, action, query, motionId, name, ruleId, modelHash;
        public bool compatibleOnly, favouritesOnly, includeShort, favourite, loop, archivedOnly;
        public string[] tags;
        public bool Valid()
        {
            bool Text(string text,int maximum) => text != null && text.Length <= maximum && !text.Any(char.IsControl);
            if (version != 1 || sequence < 1 || !Guid.TryParseExact(session,"N",out _) || offset < 0 || offset > 1024 ||
                sourceIndex < 0 || sourceIndex >= 1024 || termsPage < 0 || termsPage > 64 || stepIndex < 0 || stepIndex >= 16 || usagePage < 0 || usagePage > 512) return false;
            if (action == "query") return Text(query,80);
            if (action == "stop" || action == "close") return true;
            if (action == "roleUndo" || action == "roleRedo") return ModelLibrary.ValidHash(modelHash);
            if (action == "roleAssign" || action == "roleRemove" || action == "roleClear")
                return ModelLibrary.ValidHash(modelHash) && role >= 0 && role < 4 && (action == "roleClear" || Guid.TryParseExact(motionId,"N",out _)) &&
                    (action != "roleAssign" || new TutorMotionChoice { motionId=motionId,weight=weight,speed=speed,cooldown=cooldown,loop=loop }.Valid());
            if (!Guid.TryParseExact(motionId,"N",out _)) return false;
            if (action == "save") return Text(name,100) && !string.IsNullOrWhiteSpace(name) && tags != null && tags.Length <= 16 && tags.All(x => Text(x,32) && !string.IsNullOrWhiteSpace(x));
            if (action == "rule") return Guid.TryParseExact(ruleId,"N",out _);
            return action == "select" || action == "preview" || action == "walk" || action == "archive" || action == "restore" || action == "removeDownload" || action == "forgetMotion";
        }
    }
    [Serializable] public sealed class LibraryBookEntry
    {
        public string id,name;
        public string[] tags;
        public float duration;
        public bool favourite,compatible,shortClip,archived,removed,downloaded;
        public int bytes;
    }
    [Serializable] public sealed class ActivityChoiceView
    {
        public string motionId,name;
        public int weight;
        public float speed,cooldown;
        public bool loop,available;
    }
    [Serializable] public sealed class ActivityRoleView
    {
        public int role;
        public ActivityChoiceView[] choices=Array.Empty<ActivityChoiceView>();
    }
    [Serializable] public sealed class ActivityProfileView
    {
        public string modelHash,status;
        public bool canAssign,readOnly,canUndo,canRedo;
        public ActivityRoleView[] roles=Array.Empty<ActivityRoleView>();
    }
    [Serializable] public sealed class LibraryBookState
    {
        public int version = 1, revision, ack, offset, total, pageSize = MotionLibrary.SearchPageSize, stepIndex, sourceIndex, sourceCount, termsPage, termsPages;
        public string session, query, status, ruleId, ruleName, sourceName, attribution;
        public bool visible, busy, readOnly, compatibleOnly, favouritesOnly, includeShort, canPreview, canWalk, canAssign;
        public LibraryBookEntry[] entries = Array.Empty<LibraryBookEntry>();
        public LibraryBookEntry selected;
        public ActivityProfileView activityProfile;
        public MotionUsageView usage;
        public bool archivedOnly,canRemoveDownload,canForgetMotion;
    }
    // Native remains authoritative for metadata, compatibility, rule targets and
    // playback. A bounded top-document snapshot carries one queued request;
    // session/sequence checks prevent stale or repeated polling from replaying it.
    public sealed class LibraryBookController : MonoBehaviour
    {
        const int TermsSize = 1500;
        RoomEditor editor; ImportWorkshop imports; RuleWorkshop rules; NativeBookBrowser browser; MaestroAvatar avatar;
        string session = Guid.NewGuid().ToString("N"), selectedId, query = "", message = "Choose a saved motion";
        int revision, ack, lastSequence, offset, sourceIndex, termsPage, operation,usagePage;
        bool visible,busy,suspended,compatibleOnly = true,favouritesOnly,includeShort,disposed,archivedOnly;
        bool paused,focused = true;
        float nextPublish,nextRetention;
        bool checkingRetention;
        string retainedId; MotionRetention retained;
        string acknowledgedSession, trayMotionId; int acknowledgedRevision;
        public LibraryBookState State { get; private set; }
        public bool UsagePending => checkingRetention;
        public void Initialize(RoomEditor editor,ImportWorkshop imports,RuleWorkshop rules,NativeBookBrowser browser = null)
        {
            this.editor = editor; this.imports = imports; this.rules = rules; this.browser = browser;
            avatar = editor.Find("maestro").GetComponent<MaestroAvatar>();
            imports.BrowseRequested += Toggle; imports.Changed += Refresh; rules.Changed += Refresh; editor.Changed += Refresh;
            editor.ActivityProfiles.Changed+=Refresh;
            if (avatar) { avatar.ModelChanged += Refresh; avatar.ActivityMotionChanged+=Refresh; }
            if (browser) browser.SnapshotChanged += Observe;
            Refresh();
        }
        public void Toggle() => SetVisible(!visible);
        public void SetVisible(bool value)
        {
            if (visible == value) return;
            if (value && !imports.LibraryMode) imports.ToggleLibrary(); else imports.StopPreview();
            visible = value; avatar?.SetActivityLibraryOpen(value); operation++; busy = false;
            session = Guid.NewGuid().ToString("N"); ack = lastSequence = 0;
            message = value ? "Choose a saved motion. Preview never starts automatically." : "Returned to conversation";
            Refresh();
        }
        void Observe(BookSnapshot snapshot)
        {
            acknowledgedSession = snapshot.librarySession; acknowledgedRevision = snapshot.libraryRevision;
            if (snapshot.libraryRequest != null) _ = HandleAsync(snapshot.libraryRequest);
        }
        public async Task HandleAsync(LibraryBookRequest request)
        {
            if (disposed || suspended || !visible || request == null || request.session != session || request.sequence <= lastSequence) return;
            lastSequence = request.sequence;
            if (!request.Valid()) { ack = request.sequence; message = "The library request is invalid. Review its fields and try again."; Refresh(); return; }
            if (request.action == "stop" || request.action == "close")
            {
                operation++; busy = false; imports.StopPreview(); ack = request.sequence;
                if (request.action == "close") { visible = false; avatar?.SetActivityLibraryOpen(false); }
                message = request.action == "close" ? "Returned to conversation" : "Motion stopped"; Refresh(); return;
            }
            if (busy) { ack = request.sequence; message = "Please wait for the current library operation"; Refresh(); return; }
            int current = ++operation; string ownerSession = session;
            try
            {
                if (request.action == "query")
                {
                    query = request.query.Trim(); compatibleOnly = request.compatibleOnly; favouritesOnly = request.favouritesOnly; includeShort = request.includeShort; archivedOnly=request.archivedOnly; offset = request.offset; message = "Search updated. Choose an animation to view its details.";
                }
                else if (request.action.StartsWith("role",StringComparison.Ordinal))
                {
                    if (!avatar || avatar.ModelBusy || !avatar.CustomModel || avatar.ModelHash != request.modelHash) throw new ModelImportException("Maestro changed. Review its state assignments and try again.");
                    string error; bool accepted;
                    var role=(TutorMotionRole)request.role;
                    if (request.action == "roleUndo") accepted=editor.ActivityProfiles.Undo(request.modelHash,out error);
                    else if (request.action == "roleRedo") accepted=editor.ActivityProfiles.Redo(request.modelHash,out error);
                    else if (request.action == "roleClear" || request.action == "roleRemove") accepted=editor.ActivityProfiles.Remove(avatar.ModelHash,role,request.action == "roleClear" ? null : request.motionId,out error);
                    else accepted=editor.ActivityProfiles.Assign(avatar.ModelHash,avatar.CustomModel.MotionRigHash,role,new TutorMotionChoice { motionId=request.motionId,loop=request.loop,speed=request.speed,weight=request.weight,cooldown=request.cooldown },editor.Motions,out error);
                    if (!accepted) throw new ModelImportException(error);
                    message="Tutor-state assignments saved. Automatic motions resume with the conversation.";
                }
                else
                {
                    var entry = editor.Motions.Inspect(request.motionId) ?? throw new ModelImportException("This motion is missing. Import its original file again.");
                    selectedId = entry.id;
                    if (request.action != "archive" && request.action != "restore" && request.action != "removeDownload" && request.action != "forgetMotion") imports.SelectLibraryMotion(entry.id);
                    if (request.action == "select") { sourceIndex = request.sourceIndex; termsPage = request.termsPage; usagePage=request.usagePage; message = "Selected "+entry.name; }
                    else if (request.action == "archive" || request.action == "restore" || request.action == "removeDownload" || request.action == "forgetMotion")
                    {
                        busy=true; message="Updating local motion storage…"; Refresh();
                        async Task<string> Protection() {
                            var saved=await Task.Run(() => MotionUsage.ReadSaved(editor,rules,entry.id,true));
                            if (!this || disposed || suspended || current != operation || ownerSession != session) return "The library changed. Review this motion before removing its download.";
                            return MotionUsage.Read(editor,rules,entry.id,retained:saved).protection;
                        }
                        if (request.action == "removeDownload") await editor.Motions.RemoveDownloadAsync(entry.id,Protection);
                        else if (request.action == "forgetMotion") { await editor.Motions.ForgetAsync(entry.id,Protection); selectedId=null; }
                        else await editor.Motions.ArchiveAsync(entry.id,request.action == "archive");
                        if (!this || disposed || current != operation || ownerSession != session) return;
                        imports.RefreshLibraryDetails();
                        message=request.action == "archive" ? "Archived. Existing assignments still work; find it with the Archived filter." :
                            request.action == "restore" ? "Returned to the main library. Playback has not started." : request.action == "forgetMotion" ? "Removed motion details. A later import will create a new library identity." : "Local download removed. Its identity and details are kept; import the original export to restore it.";
                    }
                    else if (request.action == "save")
                    {
                        busy = true; message = "Saving motion details…"; Refresh();
                        await editor.Motions.UpdateAsync(entry.id,request.name.Trim(),request.tags.Select(x => x.Trim()).ToArray(),request.favourite);
                        if (!this || disposed || current != operation || ownerSession != session) return;
                        imports.RefreshLibraryDetails();
                        message = "Motion details saved; existing assignments are retained";
                    }
                    else if (request.action == "preview")
                    {
                        busy = true; message = "Loading motion…"; Refresh();
                        bool played = await imports.PlayLibraryAsync(entry.id,request.loop);
                        if (!this || disposed || current != operation || ownerSession != session) return;
                        message = played ? "Playing "+entry.name : imports.Status;
                    }
                    else if (request.action == "walk")
                    {
                        if (entry.removed) throw new ModelImportException("Import the original export again before assigning this motion.");
                        if (!editor.SetAvatarWalkMotion(entry.id)) throw new ModelImportException(editor.Status);
                        message = "Walking motion assigned. Use Preview walk or Follow to play it.";
                    }
                    else if (request.action == "rule")
                    {
                        if (entry.removed) throw new ModelImportException("Import the original export again before assigning this motion.");
                        var sequence = rules.Selected;
                        if (sequence == null || sequence.id != request.ruleId || rules.SelectedStepIndex != request.stepIndex) throw new ModelImportException("The selected action changed. Review it and assign again.");
                        var selectedStep = rules.SelectedStep;
                        if (selectedStep == null) throw new ModelImportException("Edit this program's animation blocks in the book before assigning a motion.");
                        var target = editor.Find(selectedStep.targetId);
                        var model = RoomRuleActions.ClipModel(target); var targetAvatar = target ? target.GetComponent<MaestroAvatar>() : null;
                        if (!model || !model.Ready || targetAvatar && targetAvatar.ModelBusy || model.MotionRigHash != entry.rigHash) throw new ModelImportException("This motion is incompatible with the selected action's target.");
                        rules.AssignLibraryMotion(entry.id);
                        if (rules.SelectedStep?.motionId != entry.id) throw new ModelImportException(rules.Status);
                        message = "Assigned to "+sequence.name+", step "+(request.stepIndex+1)+". Its triggers are retained.";
                    }
                }
            }
            catch (Exception error) { if (current == operation) message = error is ModelImportException ? error.Message : "This library operation could not finish. Try again."; }
            finally
            {
                if (this && !disposed && current == operation && ownerSession == session) { busy = false; ack = request.sequence; Refresh(); }
            }
        }
        LibraryBookEntry View(MotionEntry entry,string rig) => entry == null ? null : new LibraryBookEntry { id = entry.id,name = entry.name,tags = entry.tags,duration = entry.duration,favourite = entry.favourite,shortClip = entry.Short,compatible = rig != null && entry.rigHash == rig,archived=entry.archived,removed=entry.removed,downloaded=editor.Motions.Downloaded(entry.id),bytes=entry.bytes };
        public void Refresh()
        {
            if (!editor || disposed) return;
            if (imports.LibraryMode && imports.SelectedLibraryMotionId != trayMotionId) { trayMotionId = imports.SelectedLibraryMotionId; selectedId = trayMotionId; }
            string rig = avatar && !avatar.ModelBusy && avatar.CustomModel ? avatar.CustomModel.MotionRigHash : null;
            var page = editor.Motions.Search(query,compatibleOnly ? rig ?? "" : null,includeShort,favouritesOnly,archivedOnly,offset);
            offset = page.Offset;
            var selected = editor.Motions.Inspect(selectedId); var sequence = rules.Selected; int step = rules.SelectedStepIndex;
            var selectedStep = rules.SelectedStep;
            var target = selectedStep == null ? null : editor.Find(selectedStep.targetId);
            var model = RoomRuleActions.ClipModel(target); var targetAvatar = target ? target.GetComponent<MaestroAvatar>() : null;
            var sources = selected == null ? Array.Empty<MotionSource>() : editor.Motions.Sources().Where(x => selected.origins.Any(y => y.sourceHash == x.hash)).ToArray();
            sourceIndex = sources.Length == 0 ? 0 : Mathf.Clamp(sourceIndex,0,sources.Length-1);
            var source = sources.Length == 0 ? null : sources[sourceIndex]; string terms = source?.attribution ?? "";
            int pages = Math.Max(1,(terms.Length+TermsSize-1)/TermsSize); termsPage = Mathf.Clamp(termsPage,0,pages-1);
            bool compatible = selected != null && rig != null && selected.rigHash == rig && editor.Motions.Downloaded(selected.id);
            var usage=selected == null ? null : MotionUsage.Read(editor,rules,selected.id,usagePage,retainedId == selected.id ? retained : null);
            if (selected != null && !checkingRetention && (retainedId != selected.id || Time.unscaledTime >= nextRetention)) _=CheckRetention(selected.id);
            State = new LibraryBookState {
                revision = ++revision,ack = ack,session = session,visible = visible,busy = busy,readOnly = editor.Motions.ReadOnly,
                query = query,offset = offset,total = page.Total,compatibleOnly = compatibleOnly,favouritesOnly = favouritesOnly,includeShort = includeShort,archivedOnly=archivedOnly,usage=usage,
                canRemoveDownload=selected != null && selected.archived && (!selected.removed || editor.Motions.PayloadPresent(selected.id)) && usage.protection == null && !editor.Motions.ReadOnly,
                canForgetMotion=selected != null && selected.removed && !editor.Motions.PayloadPresent(selected.id) && usage.protection == null && !editor.Motions.ReadOnly,
                entries = page.Entries.Select(x => View(x,rig)).ToArray(),selected = View(selected,rig),
                canPreview = compatible,canWalk = compatible && !selected.Short,canAssign = selected != null && editor.Motions.Downloaded(selected.id) && model && model.Ready && (!targetAvatar || !targetAvatar.ModelBusy) && model.MotionRigHash == selected.rigHash,
                ruleId = sequence?.id,ruleName = sequence?.name,stepIndex = step,sourceIndex = sourceIndex,sourceCount = sources.Length,sourceName = source?.name,
                attribution = terms.Substring(termsPage*TermsSize,Math.Min(TermsSize,terms.Length-termsPage*TermsSize)),termsPage = termsPage,termsPages = pages,
                activityProfile = new ActivityProfileView {
                    modelHash=avatar && !avatar.ModelBusy ? avatar.ModelHash : "",readOnly=editor.ActivityProfiles.ReadOnly,
                    canAssign=compatible && !selected.Short && !editor.ActivityProfiles.ReadOnly,canUndo=editor.ActivityProfiles.CanUndo(avatar ? avatar.ModelHash : ""),canRedo=editor.ActivityProfiles.CanRedo(avatar ? avatar.ModelHash : ""),
                    status=editor.ActivityProfiles.Notice ?? avatar?.ActivityMotionStatus ?? "Uses included animations where no state motion is assigned",
                    roles=Enumerable.Range(0,4).Select(role => new ActivityRoleView { role=role,choices=(editor.ActivityProfiles.Find(avatar ? avatar.ModelHash : "")?.roles.FirstOrDefault(x => (int)x.role == role)?.choices ?? Array.Empty<TutorMotionChoice>())
                        .Select(choice => { var entry=editor.Motions.Find(choice.motionId); return new ActivityChoiceView { motionId=choice.motionId,name=entry?.name ?? "Missing saved motion",loop=choice.loop,speed=choice.speed,weight=choice.weight,cooldown=choice.cooldown,available=entry != null && editor.Motions.Downloaded(entry.id) && !entry.Short && entry.rigHash == rig }; }).ToArray() }).ToArray()
                },
                status = editor.Motions.Notice ?? message
            };
        }
        async Task CheckRetention(string id)
        {
            checkingRetention=true;
            try { var result=await Task.Run(() => MotionUsage.ReadSaved(editor,rules,id)); if (this && !disposed) { retained=result; retainedId=id; } }
            catch { if (this && !disposed) { retained=new MotionRetention { Uncertain=true }; retainedId=id; } }
            finally { if (this && !disposed) { checkingRetention=false; nextRetention=Time.unscaledTime+1; Refresh(); } }
        }
        void Update()
        {
            if (visible && !suspended && State?.selected != null && !checkingRetention && Time.unscaledTime >= nextRetention) Refresh();
            if (!browser || suspended || State == null || Time.unscaledTime < nextPublish) return;
            nextPublish = Time.unscaledTime+.3f;
            if (acknowledgedSession != State.session || acknowledgedRevision != State.revision) browser.PublishLibraryState(Newtonsoft.Json.JsonConvert.SerializeObject(State));
        }
        void Lifecycle()
        {
            suspended = paused || !focused; operation++; busy = false;
            if (suspended) imports?.StopPreview();
            session = Guid.NewGuid().ToString("N"); ack = lastSequence = 0; message = "Library ready. Interrupted actions are not replayed."; Refresh();
        }
        void OnApplicationPause(bool value) { paused = value; Lifecycle(); }
        void OnApplicationFocus(bool value) { focused = value; Lifecycle(); }
        void OnDisable() { if (imports) { suspended = true; operation++; imports.StopPreview(); } }
        void OnDestroy()
        {
            disposed = true; operation++;
            if (imports) { imports.BrowseRequested -= Toggle; imports.Changed -= Refresh; }
            if (rules) rules.Changed -= Refresh; if (editor) editor.Changed -= Refresh;
            if (editor) editor.ActivityProfiles.Changed-=Refresh;
            if (avatar) { avatar.ModelChanged -= Refresh; avatar.ActivityMotionChanged-=Refresh; avatar.SetActivityLibraryOpen(false); } if (browser) browser.SnapshotChanged -= Observe;
        }
    }
}
