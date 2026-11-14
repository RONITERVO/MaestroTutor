// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;

namespace Maestro.Quest.Book
{
    public enum PageSide { Left, Right }

    /// <summary>Browser texture is a single session: left half chat, right half artifact.</summary>
    public static class BookCoordinates
    {
        public static (int x, int y) ToBrowserPixel(PageSide side, double pageU, double pageV, int width, int height)
        {
            if (width < 2 || width % 2 != 0 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (double.IsNaN(pageU) || double.IsInfinity(pageU) || double.IsNaN(pageV) || double.IsInfinity(pageV))
                throw new ArgumentOutOfRangeException(nameof(pageU));
            int half = width / 2;
            int x = Math.Min(half - 1, (int)(Math.Clamp(pageU, 0, 1) * half));
            int y = Math.Min(height - 1, (int)((1 - Math.Clamp(pageV, 0, 1)) * height));
            return (x + (side == PageSide.Right ? half : 0), y);
        }
    }
}
