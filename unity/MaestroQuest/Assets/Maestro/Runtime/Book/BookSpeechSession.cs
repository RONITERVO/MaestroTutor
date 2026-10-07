// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Ephemeral PCM mailbox, fenced by Android document, browser owner,
    /// output revision and chunk sequence. No provider or persistent room actions.</summary>
    public sealed class BookSpeechSession : IDisposable
    {
        readonly string host = Guid.NewGuid().ToString("N");
        NativeSpeechOutput output;
        string document = "", session = "";
        long poll = -1, revision = -1, generation;
        double lastResponse = double.NegativeInfinity;
        double progressAt;
        long lastPlayed;
        bool open, failed;
        public string Document => document;
        public bool Active => open && !failed;
        public BookSpeechSession(NativeSpeechOutput output) { this.output = output; }
        public void Bind(NativeSpeechOutput next)
        {
            if (output == next) return;
            Fail(); output = next;
        }
        public void Suspend()
        {
            output?.Stop(); failed = true;
            // Keep the last browser revision retired even if JavaScript was
            // stalled during focus loss. Resuming cannot reopen its old voice.
            document = ""; poll = -1; lastResponse = double.NegativeInfinity;
        }
        public void Dispose() => Suspend();
        void Fail() { output?.Stop(); failed = true; }
        static bool Token(string value) => value != null && value.Length == 32
            && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-f0-9]+$");
        static long Number(JToken value)
        {
            if (value?.Type != JTokenType.Integer) throw new ArgumentException("Invalid speech counter");
            long result = (long)value;
            if (result < 0 || result > 9007199254740991L) throw new ArgumentException("Invalid speech counter");
            return result;
        }
        public void Receive(string json, double now)
        {
            if (string.IsNullOrEmpty(json)) { Tick(now); return; }
            if (json.Length > 110000) { Fail(); return; }
            try
            {
                var envelope = JObject.Parse(json);
                var nextDocument = (string)envelope["document"];
                var nextPoll = Number(envelope["poll"]);
                if (!Token(nextDocument)) throw new ArgumentException("Invalid speech document");
                if (document != nextDocument)
                {
                    output?.Stop(); document = nextDocument; poll = -1; failed = true;
                }
                if (nextPoll <= poll) { Tick(now); return; }
                poll = nextPoll; lastResponse = now;
                if (envelope["payload"] is not JObject payload) { Fail(); return; }
                var nextSession = (string)payload["session"];
                var nextRevision = Number(payload["revision"]);
                if ((int?)payload["version"] != 1 || !Token(nextSession)
                    || payload["open"]?.Type != JTokenType.Boolean || payload["chunks"] is not JArray chunks || chunks.Count > 8)
                    throw new ArgumentException("Invalid speech request");
                bool nextOpen = (bool)payload["open"];
                // Validate the whole batch before allowing even its first PCM chunk.
                var decoded = new List<(long Sequence, short[] Samples)>();
                long previous = 0;
                foreach (var token in chunks)
                {
                    var sequence = Number(token["sequence"]);
                    var encoded = (string)token["pcm"];
                    if (sequence <= previous || encoded == null || encoded.Length == 0 || encoded.Length > 12800
                        || !System.Text.RegularExpressions.Regex.IsMatch(encoded, "^[A-Za-z0-9+/]+={0,2}$"))
                        throw new ArgumentException("Invalid speech chunk");
                    var bytes = Convert.FromBase64String(encoded);
                    if (bytes.Length == 0 || bytes.Length % 2 != 0 || bytes.Length > 9600) throw new ArgumentException("Invalid PCM");
                    var samples = new short[bytes.Length / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = (short)(bytes[2*i] | bytes[2*i+1] << 8);
                    decoded.Add((sequence, samples)); previous = sequence;
                }
                if (!nextOpen && chunks.Count > 0) throw new ArgumentException("Closed speech includes audio");
                if (nextSession != session || nextRevision > revision)
                {
                    output?.Stop(); session = nextSession; revision = nextRevision; open = nextOpen; failed = false;
                    if (open)
                    {
                        if (!output) { Fail(); return; }
                        generation = output.Begin(24000);
                        progressAt = now; lastPlayed = 0;
                    }
                }
                else if (nextRevision < revision) return;
                else if (open != nextOpen) throw new ArgumentException("Speech revision was reused");
                if (!open || failed) return;
                if (!output || output.Read().generation != generation) { Fail(); return; }
                foreach (var chunk in decoded)
                {
                    var state = output.Read();
                    if (chunk.Sequence <= state.acceptedSequence) continue; // Receipt was lost: never replay PCM.
                    if (state.submittedSamples == state.playedSamples) progressAt = now;
                    if (!output.TryWrite(generation, chunk.Sequence, chunk.Samples, out _)) { Fail(); return; }
                }
            }
            catch (Exception) { Fail(); }
            Tick(now);
        }
        public void Tick(double now)
        {
            if (!open || failed) return;
            if (now - lastResponse > 1.5 || !output) { Fail(); return; }
            var state = output.Read();
            if (state.generation != generation) { Fail(); return; }
            if (state.playedSamples != lastPlayed) { lastPlayed = state.playedSamples; progressAt = now; }
            if (state.submittedSamples > state.playedSamples && now - progressAt > 3) Fail();
        }
        public string Status(double now)
        {
            Tick(now);
            var state = open && !failed && output ? output.Read() : null;
            return new JObject {
                ["version"] = 1, ["host"] = host, ["session"] = session, ["revision"] = Math.Max(0, revision),
                ["status"] = failed || !output || !output.isActiveAndEnabled ? "failed" : open ? "playing" : "ready",
                ["acceptedSequence"] = state?.acceptedSequence ?? 0,
                ["submittedSamples"] = state?.submittedSamples ?? 0,
                ["playedSamples"] = state?.playedSamples ?? 0
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
