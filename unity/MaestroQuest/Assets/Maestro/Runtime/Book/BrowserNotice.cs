// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Maestro.Quest.Interaction;

namespace Maestro.Quest.Book
{
    public sealed class BrowserNotice : PhysicalAction
    {
        public NativeBookBrowser Browser;
        protected override void OnActivate() => Browser?.ClearError();
        public static string Wrap(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var result = new StringBuilder(); int column = 0;
            foreach (var word in value.Split(' '))
            {
                if (column + word.Length > 32 && column > 0) { result.Append('\n'); column = 0; }
                if (column > 0) { result.Append(' '); column++; }
                result.Append(word); column += word.Length;
                if (result.Length >= 240) break;
            }
            return result + "\n\nTap this token to dismiss";
        }
    }
}
