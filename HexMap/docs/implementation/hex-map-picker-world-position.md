# World-position map picking boundary

`HexMapPicker` is the input-independent map query seam. It accepts a finite Unity world position through `PickWorldPosition` and returns one of `Found`, `NoMap`, `NotOnMapPlane`, `OutsideMap` or `Missing`. Successful results expose the matching `HexView` and `HexCell`; failed results expose neither.

`HexMapView` owns the scene geometry boundary. `WorldToMapLocal` applies the map Transform's translation, rotation and uniform scale without checking map bounds or plane membership. `WorldPlane` is derived from the active `HexLayout`, includes `Layout.Origin`, and uses local `z` for XY layouts or local `y` for XZ layouts. Non-uniform and zero scales are rejected by both seams.

`TryGetHexViewAtWorldPoint` remains a convenience query with intentionally weaker semantics: it converts the world point and performs the map lookup while ignoring the perpendicular coordinate. Callers that require a physical map-plane check must use `HexMapPicker`.

The sample-only `HexMapScreenPointAdapter` owns Camera lookup, screen-point conversion, ray/plane intersection, maximum ray distance, optional left-mouse polling, logging and Sample-level failures. It calls `HexMapPicker.PickWorldPosition` only after obtaining a world point from `HexMapView.WorldPlane`. The UnityRuntime assembly does not depend on the Sample assembly, and the adapter never uses `Physics.Raycast` or generated colliders.

Future touch, editor and scripted integrations should convert their input into a world position and call the same Picker API. Input policy and presentation must remain outside the map query service.