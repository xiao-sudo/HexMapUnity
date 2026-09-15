# GVG Map Export

This directory is generated from the Unity ScriptableObject authoring asset.

## Files

- `Map.csv`: map identity and layout settings.
- `Plots.csv`: Plot topology and PlotType values.
- `Cells.csv`: Cell coordinates and their owning PlotId.

CSV files are UTF-8 with BOM for Excel compatibility. `HexIds` uses a quoted JSON-like array with no spaces, for example `"[1,2,3]"`.

## PlotType

- 0 = Camp
- 1 = Normal
- 2 = Grass
- 3 = SmallCity
- 4 = BigCity
- 5 = Capital
- 6 = Obstacle

## PlotId Rules

- Single-cell PlotId equals its only HexId.
- Multi-cell PlotId is negative and allocated from -1 downward.
- PlotId uniqueness is required before export.

## Runtime Defaults

- PlotState = Open
- OwnerFaction = Neutral
- OwnershipMode = Capturable
- BlockingState = Passable
- PlotType.Obstacle overrides BlockingState to Blocked.
- PlotType.Camp overrides OwnershipMode to Fixed.

CSV import is not implemented in this issue. These files are export review artifacts and a future import source.
