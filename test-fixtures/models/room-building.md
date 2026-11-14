# Original rigid building fixture

`room-building.glb` is original synthetic geometry under this repository's
Apache-2.0 license. It has five boxes in one triangle mesh: a 6 m × 6 m floor,
a roof, and a dividing wall with a 1.2 m wide, 2.2 m tall doorway. The floor top
is at y=0; the complete mesh extends from y=-0.1 to y=3 m. There are no textures,
external resources, skins, animations or user-provided data.

The explicit `ModelGeometry` provider scenario and `SyntheticModel` book fixture
seed it through the actual private model library and native import path. It is
not included as a shipped asset or used as evidence of Quest performance. Native
PlayMode tests construct the same five-box layout in `BuildingModelFixture.cs`.
