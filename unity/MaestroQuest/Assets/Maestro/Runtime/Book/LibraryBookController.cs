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
        public int version, sequence, offset, stepIndex, sourceIndex, termsPage;
        public string session, action, query, motionId, name, ruleId;
        public bool compatibleOnly, favouritesOnly, includeShort, favourite, loop;
        public string[] tags;
        public bool Valid()
        {
            bool Text(string text,int maximum) => text != null && text.Length <= maximum && !text.Any(char.IsControl);
            if (version != 1 || sequence < 1 || !Guid.TryParseExact(session,"N",out _) || offset < 0 || offset > 1024 ||
                sourceIndex < 0 || sourceIndex >= 1024 || termsPage < 0 || termsPage > 64 || stepIndex < 0 || stepIndex >= 16) return false;
            if (action == "query") return Text(query,80);
            if (action == "stop" || action == "close") return true;
            if (!Guid.TryParseExact(motionId,"N",out _)) return false;
            if (action == "save") return Text(name,100) && !string.IsNullOrWhiteSpace(name) && tags != null && tags.Length <= 16 && tags.All(x => Text(x,32) && !string.IsNullOrWhiteSpace(x));
            if (action == "rule") return Guid.TryParseExact(ruleId,"N",out _);
            return action == "select" || action == "preview" || action == "walk";
        }
    }
    [Serializable] public sealed class LibraryBookEntry
    {
        public string id,name;
        public string[] tags;
        public float duration;
        public bool favourite,compatible,shortClip;
    }
    [Serializable] public sealed class LibraryBookState
    {
        public int version = 1, revision, ack, offset, total, pageSize = 12, stepIndex, sourceIndex, sourceCount, termsPage, termsPages;
        public string session, query, status, ruleId, ruleName, sourceName, attribution;
        public bool visible, busy, readOnly, compatibleOnly, favouritesOnly, includeShort, canPreview, canWalk, canAssign;
        public LibraryBookEntry[] entries = Array.Empty<LibraryBookEntry>();
        public LibraryBookEntry selected;
    }
    // Native remains authoritative for metadata, compatibility, rule targets and
    // playback. A bounded top-document snapshot carries one queued request;
    // session/sequence checks prevent stale or repeated polling from replaying it.
    public sealed class LibraryBookController : MonoBehaviour
    {
        const int PageSize = 12, TermsSize = 1500;
        RoomEditor editor; ImportWorkshop imports; RuleWorkshop rules; NativeBookBrowser browser; MaestroAvatar avatar;
        string session = Guid.NewGuid().ToString("N"), selectedId, query = "", message = "Choose a saved motion";
        int revision, ack, lastSequence, offset, sourceIndex, termsPage, operation;
        bool visible,busy,suspended,compatibleOnly = true,favouritesOnly,includeShort,disposed;
        bool paused,focused = true;
        float nextPublish;
        string acknowledgedSession, trayMotionId; int acknowledgedRevision;
        public LibraryBookState State { get; private set; }
        public void Initialize(RoomEditor editor,ImportWorkshop imports,RuleWorkshop rules,NativeBookBrowser browser = null)
        {
            this.editor = editor; this.imports = imports; this.rules = rules; this.browser = browser;
            avatar = editor.Find("maestro").GetComponent<MaestroAvatar>();
            imports.BrowseRequested += Toggle; imports.Changed += Refresh; rules.Changed += Refresh; editor.Changed += Refresh;
            if (avatar) avatar.ModelChanged += Refresh;
            if (browser) browser.SnapshotChanged += Observe;
            Refresh();
        }
        public void Toggle() => SetVisible(!visible);
        public void SetVisible(bool value)
        {
            if (visible == value) return;
            if (value && !imports.LibraryMode) imports.ToggleLibrary(); else imports.StopPreview();
            visible = value; operation++; busy = false;
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
                if (request.action == "close") visible = false;
                message = request.action == "close" ? "Returned to conversation" : "Motion stopped"; Refresh(); return;
            }
            if (busy) { ack = request.sequence; message = "Please wait for the current library operation"; Refresh(); return; }
            int current = ++operation; string ownerSession = session;
            try
            {
                if (request.action == "query")
                {
                    query = request.query.Trim(); compatibleOnly = request.compatibleOnly; favouritesOnly = request.favouritesOnly; includeShort = request.includeShort; offset = request.offset; message = "Search updated. Choose an animation to view its details.";
                }
                else
                {
                    var entry = editor.Motions.Find(request.motionId) ?? throw new ModelImportException("This motion is missing. Import its original file again.");
                    selectedId = entry.id; imports.SelectLibraryMotion(entry.id);
                    if (request.action == "select") { sourceIndex = request.sourceIndex; termsPage = request.termsPage; message = "Selected "+entry.name; }
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
                        if (!editor.SetAvatarWalkMotion(entry.id)) throw new ModelImportException(editor.Status);
                        message = "Walking motion assigned. Use Preview walk or Follow to play it.";
                    }
                    else if (request.action == "rule")
                    {
                        var sequence = rules.Selected;
                        if (sequence == null || sequence.id != request.ruleId || rules.SelectedStepIndex != request.stepIndex) throw new ModelImportException("The selected action changed. Review it and assign again.");
                        var target = editor.Find(sequence.steps[request.stepIndex].targetId);
                        var model = RoomRuleActions.ClipModel(target); var targetAvatar = target ? target.GetComponent<MaestroAvatar>() : null;
                        if (!model || !model.Ready || targetAvatar && targetAvatar.ModelBusy || model.MotionRigHash != entry.rigHash) throw new ModelImportException("This motion is incompatible with the selected action's target.");
                        rules.AssignLibraryMotion(entry.id);
                        if (rules.Selected.steps[request.stepIndex].motionId != entry.id) throw new ModelImportException(rules.Status);
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
        LibraryBookEntry View(MotionEntry entry,string rig) => entry == null ? null : new LibraryBookEntry { id = entry.id,name = entry.name,tags = entry.tags,duration = entry.duration,favourite = entry.favourite,shortClip = entry.Short,compatible = rig != null && entry.rigHash == rig };
        public void Refresh()
        {
            if (!editor || disposed) return;
            if (imports.LibraryMode && imports.SelectedLibraryMotionId != trayMotionId) { trayMotionId = imports.SelectedLibraryMotionId; selectedId = trayMotionId; }
            string rig = avatar && !avatar.ModelBusy && avatar.CustomModel ? avatar.CustomModel.MotionRigHash : null;
            var matches = editor.Motions.List(query,compatibleOnly ? rig ?? "" : null,includeShort,favouritesOnly);
            offset = matches.Length == 0 ? 0 : Math.Min(offset/PageSize,(matches.Length-1)/PageSize)*PageSize;
            var selected = editor.Motions.Find(selectedId); var sequence = rules.Selected; int step = rules.SelectedStepIndex;
            var target = sequence == null ? null : editor.Find(sequence.steps[step].targetId);
            var model = RoomRuleActions.ClipModel(target); var targetAvatar = target ? target.GetComponent<MaestroAvatar>() : null;
            var sources = selected == null ? Array.Empty<MotionSource>() : editor.Motions.Sources().Where(x => selected.origins.Any(y => y.sourceHash == x.hash)).ToArray();
            sourceIndex = sources.Length == 0 ? 0 : Mathf.Clamp(sourceIndex,0,sources.Length-1);
            var source = sources.Length == 0 ? null : sources[sourceIndex]; string terms = source?.attribution ?? "";
            int pages = Math.Max(1,(terms.Length+TermsSize-1)/TermsSize); termsPage = Mathf.Clamp(termsPage,0,pages-1);
            bool compatible = selected != null && rig != null && selected.rigHash == rig;
            State = new LibraryBookState {
                revision = ++revision,ack = ack,session = session,visible = visible,busy = busy,readOnly = editor.Motions.ReadOnly,
                query = query,offset = offset,total = matches.Length,compatibleOnly = compatibleOnly,favouritesOnly = favouritesOnly,includeShort = includeShort,
                entries = matches.Skip(offset).Take(PageSize).Select(x => View(x,rig)).ToArray(),selected = View(selected,rig),
                canPreview = compatible,canWalk = compatible && !selected.Short,canAssign = selected != null && model && model.Ready && (!targetAvatar || !targetAvatar.ModelBusy) && model.MotionRigHash == selected.rigHash,
                ruleId = sequence?.id,ruleName = sequence?.name,stepIndex = step,sourceIndex = sourceIndex,sourceCount = sources.Length,sourceName = source?.name,
                attribution = terms.Substring(termsPage*TermsSize,Math.Min(TermsSize,terms.Length-termsPage*TermsSize)),termsPage = termsPage,termsPages = pages,
                status = editor.Motions.Notice ?? message
            };
        }
        void Update()
        {
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
            if (avatar) avatar.ModelChanged -= Refresh; if (browser) browser.SnapshotChanged -= Observe;
        }
    }
}
