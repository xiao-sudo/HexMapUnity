# Remove redundant HexMapView editor Debug Draw

Status: ready-for-agent

## Goal

Remove the generic editor Debug Draw owned by HexMapView now that GVG Map Authoring is the authoritative SceneView visualization for GVG maps.

## Requirements

- Remove HexMapView's Debug Draw-only serialized settings and public editor properties.
- Remove the HexMapDebugDrawEditor editor gizmo implementation and its Unity metadata.
- Remove tests that only verify the deleted Debug Draw settings.
- Preserve HexMapView topology, layout, Transform, snapshot, and runtime rendering responsibilities.
- Do not change runtime behavior or the GVG Map Authoring visualization.

## Verification

- No ShowDebug*, LabelColor, or HexMapDebugDrawEditor references remain.
- Relevant EditMode projects build successfully.
- git diff --check passes.

## Comments
