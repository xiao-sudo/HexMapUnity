using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HexMap.Gvg.Authoring
{
    public static class GvgMapAuthoringCsv
    {
        public const string MapFileName = "Map.csv";
        public const string PlotsFileName = "Plots.csv";
        public const string CellsFileName = "Cells.csv";
        public const string ReadmeFileName = "README.md";

        public static string CreateMapCsv(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var builder = new StringBuilder();
            builder.AppendLine("MapId,Radius,Orientation,Plane,OuterRadius");
            builder.Append(Escape(asset.MapId)).Append(',')
                .Append(asset.Radius.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(((int)asset.Orientation).ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(((int)asset.Plane).ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(asset.OuterRadius.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            return builder.ToString();
        }

        public static string CreatePlotsCsv(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var sortedPlots = new List<GvgPlotAuthoringData>();
            for (var index = 0; index < asset.Plots.Count; index++)
            {
                sortedPlots.Add(asset.Plots[index]);
            }

            sortedPlots.Sort((left, right) => left.PlotId.CompareTo(right.PlotId));
            var builder = new StringBuilder();
            builder.AppendLine("PlotId,HexIds,PlotType");
            for (var index = 0; index < sortedPlots.Count; index++)
            {
                var plot = sortedPlots[index];
                builder.Append(plot.PlotId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(FormatHexIds(plot.HexIds))).Append(',')
                    .Append(((int)plot.PlotType).ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            return builder.ToString();
        }

        public static string CreateCellsCsv(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var map = asset.CreateRuntimeMap();
            var plotsByHexId = GvgMapAuthoringUtility.CreatePlotLookup(asset, true);
            var cells = new List<HexMap.Runtime.HexCell>(map.Cells);
            cells.Sort((left, right) => left.Id.CompareTo(right.Id));

            var builder = new StringBuilder();
            builder.AppendLine("HexId,Q,R,PlotId");
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                GvgPlotAuthoringData plot;
                if (!plotsByHexId.TryGetValue(cell.Id, out plot))
                {
                    throw new InvalidOperationException("HexId is not assigned to a Plot: " + cell.Id);
                }

                builder.Append(cell.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(cell.Coordinate.Q.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(cell.Coordinate.R.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(plot.PlotId.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            return builder.ToString();
        }

        public static string CreateReadme(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var builder = new StringBuilder();
            builder.AppendLine("# GVG Map Export");
            builder.AppendLine();
            builder.AppendLine("This directory is generated from the Unity ScriptableObject authoring asset.");
            builder.AppendLine();
            builder.AppendLine("## Files");
            builder.AppendLine();
            builder.AppendLine("- `Map.csv`: map identity and layout settings.");
            builder.AppendLine("- `Plots.csv`: Plot topology and PlotType values.");
            builder.AppendLine("- `Cells.csv`: Cell coordinates and their owning PlotId.");
            builder.AppendLine();
            builder.AppendLine("CSV files are UTF-8 with BOM for Excel compatibility. `HexIds` uses a quoted JSON-like array with no spaces, for example `\"[1,2,3]\"`.");
            builder.AppendLine();
            builder.AppendLine("## PlotType");
            builder.AppendLine();
            foreach (PlotType plotType in Enum.GetValues(typeof(PlotType)))
            {
                builder.Append("- ").Append(((int)plotType).ToString(CultureInfo.InvariantCulture))
                    .Append(" = ").Append(plotType).AppendLine();
            }
            builder.AppendLine();
            builder.AppendLine("## PlotId Rules");
            builder.AppendLine();
            builder.AppendLine("- Single-cell PlotId equals its only HexId.");
            builder.AppendLine("- Multi-cell PlotId is negative and allocated from -1 downward.");
            builder.AppendLine("- PlotId uniqueness is required before export.");
            builder.AppendLine();
            builder.AppendLine("## Runtime Defaults");
            builder.AppendLine();
            builder.AppendLine("- PlotState = Open");
            builder.AppendLine("- OwnerFaction = Neutral");
            builder.AppendLine("- OwnershipMode = Capturable");
            builder.AppendLine("- BlockingState = Passable");
            builder.AppendLine("- PlotType.Obstacle overrides BlockingState to Blocked.");
            builder.AppendLine("- PlotType.Camp overrides OwnershipMode to Fixed.");
            builder.AppendLine();
            builder.AppendLine("CSV import is not implemented in this issue. These files are export review artifacts and a future import source.");
            return builder.ToString();
        }

        public static Dictionary<string, string> CreateFiles(GvgMapAuthoringAsset asset)
        {
            return new Dictionary<string, string>
            {
                { MapFileName, CreateMapCsv(asset) },
                { PlotsFileName, CreatePlotsCsv(asset) },
                { CellsFileName, CreateCellsCsv(asset) },
                { ReadmeFileName, CreateReadme(asset) }
            };
        }


        private static string FormatHexIds(List<int> hexIds)
        {
            var sortedHexIds = new List<int>(hexIds);
            sortedHexIds.Sort();
            var values = new string[sortedHexIds.Count];
            for (var index = 0; index < sortedHexIds.Count; index++)
            {
                values[index] = sortedHexIds[index].ToString(CultureInfo.InvariantCulture);
            }

            return "[" + string.Join(",", values) + "]";
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            var requiresQuoting = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!requiresQuoting) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}