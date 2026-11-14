// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation {
    // Fractions of one finite segment. Disconnected wet spans are summed rather
    // than treating the first-to-last envelope as continuously filled water.
    internal readonly struct LiquidSegment {
        internal readonly float Enter,Exit;
        internal readonly double WetFraction;
        internal readonly int Cells;
        internal LiquidSegment(float enter,float exit,double wetFraction,int cells=0){
            Enter=enter;Exit=exit;WetFraction=wetFraction;Cells=cells;
        }
    }
}
