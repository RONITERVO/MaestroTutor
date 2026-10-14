// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Prepare without retiring a single view. The existing synchronous
        // transaction adopts the complete closure only after every guard holds.
        internal async Task<string> RetireNativeArea(string area, CancellationToken cancellation = default, Func<bool> admission = null)
        {
            if (!NativeTransitionAvailable(out var error)) return error;
            if(admission?.Invoke()==false)return "Area retirement admission changed";
            var graph = ReadRetention(); var members = graph?.Members(area ?? "");
            if (members == null) return "The authored area is unavailable";
            var targets = graph.Closure(members);
            if (targets.Any(id => graph.Reasons(id) != RoomRetentionReason.None)) return "The area or a connected area is still required by the running room";
            var currentInput = CaptureNativeClosureInput(targets);
            var live = targets.Where(id => !NativeEntityDormant(id)).ToDictionary(id => id, Find, StringComparer.Ordinal);
            if (live.Values.Any(item => !item || !item.isActiveAndEnabled)) return "The area's native placement is unavailable";
            var poses = live.ToDictionary(pair => pair.Key, pair => pair.Value.transform.localToWorldMatrix, StringComparer.Ordinal);
            var owner = GetComponentInParent<RoomAcoustics>();
            var prepared = new Dictionary<string,RoomAcoustics.AcousticResidency>(StringComparer.Ordinal);
            using var write = WriteGate.TryWrite(out error); if (write == null) return error;
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            void RuntimeChanged() { if (RuntimeGate.Held) cancel.Cancel(); }
            RuntimeGate.Changed += RuntimeChanged;
            bool adopted = false;
            void Check()
            {
                cancel.Token.ThrowIfCancellationRequested();
                if (admission?.Invoke()==false || !currentInput() || !DisplayReady || !NativeTransitionAvailable(out _)
                    || !ReferenceEquals(owner, GetComponentInParent<RoomAcoustics>())) throw new OperationCanceledException();
                foreach (var pair in live)
                    if (!pair.Value || NativeEntityDormant(pair.Key) || Find(pair.Key) != pair.Value
                        || !pair.Value.isActiveAndEnabled || !poses[pair.Key].Equals(pair.Value.transform.localToWorldMatrix)) throw new OperationCanceledException();
                var current = ReadRetention();
                if (current == null || targets.Any(id => current.Reasons(id) != RoomRetentionReason.None)) throw new OperationCanceledException();
            }
            try
            {
                Check(); var budget = new RoomPreparationBudget(Check, nativePreparationWindow);
                foreach (var pair in live)
                {
                    await budget.Step(); Check();
                    prepared.Add(pair.Key, owner ? await owner.PrepareResidencyAsync(pair.Value.gameObject, transform, budget, cancel.Token) : null);
                }
                Check();
                adopted = RetirePreparedNativeArea(area, prepared, out error); return adopted ? null : error;
            }
            catch (OperationCanceledException) { return "Area retirement was cancelled or its input changed"; }
            catch (InvalidOperationException failure) { return failure.Message; }
            catch (Exception) { return "The area's acoustic geometry could not be prepared"; }
            finally
            {
                RuntimeGate.Changed -= RuntimeChanged;
                if (!adopted) foreach (var item in prepared.Values)
                    if (item != null && !dormantNative.Values.Any(entry => ReferenceEquals(entry.Acoustics, item))) item.Dispose();
            }
        }
    }
}
