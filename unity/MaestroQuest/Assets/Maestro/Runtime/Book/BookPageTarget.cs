// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    public sealed class BookPageTarget : MonoBehaviour
    {
        public PageSide Side { get; set; }
        public Vector2 PageUV(Vector2 textureUV) => new((textureUV.x - (Side == PageSide.Right ? .5f : 0)) * 2, textureUV.y);
    }
}
