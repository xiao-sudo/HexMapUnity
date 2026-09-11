using System;
using System.Collections.Generic;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg.Authoring
{
    public static class GvgMapAuthoringUtility
    {
        public static List<GvgPlotAuthoringData> CreateDefaultPlots(int radius)
        {
            var map = new RuntimeHexMap(new HexMapDefinition(radius));
            var plots = new List<GvgPlotAuthoringData>(map.Count);
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                plots.Add(new GvgPlotAuthoringData(cell.Id, new[] { cell.Id }, PlotType.Normal));
            }

            return plots;
        }

        public static void ResetToDefaultPlots(GvgMapAuthoringAsset asset)
        {
            ValidateAsset(asset);
            asset.ReplacePlots(CreateDefaultPlots(asset.Radius));
        }

        public static void RebuildRadius(GvgMapAuthoringAsset asset, int radius)
        {
            ValidateAsset(asset);
            asset.Radius = radius;
            RepairForCurrentRadius(asset);
        }

        public static bool TryPaintAdd(GvgMapAuthoringAsset asset, int plotId, int hexId)
        {
            ValidateAsset(asset);
            var map = asset.CreateRuntimeMap();
            if (!map.TryGetCell(hexId, out _)) return false;

            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null) return false;

            RemoveHexFromOtherPlots(asset.MutablePlots, plot, hexId);
            if (!plot.HexIds.Contains(hexId))
            {
                plot.HexIds.Add(hexId);
            }

            RemoveEmptyPlots(asset.MutablePlots);
            NormalizePlotIds(asset.MutablePlots);
            return true;
        }

        public static bool TryPaintRemove(GvgMapAuthoringAsset asset, int plotId, int hexId)
        {
            ValidateAsset(asset);
            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null || !plot.HexIds.Contains(hexId) || plot.HexIds.Count <= 1) return false;

            plot.HexIds.Remove(hexId);
            asset.MutablePlots.Add(new GvgPlotAuthoringData(hexId, new[] { hexId }, PlotType.Normal));
            NormalizePlotIds(asset.MutablePlots);
            return true;
        }

        public static bool TryDeletePlot(GvgMapAuthoringAsset asset, int plotId)
        {
            ValidateAsset(asset);
            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null) return false;

            asset.MutablePlots.Remove(plot);
            foreach (var hexId in plot.HexIds)
            {
                asset.MutablePlots.Add(new GvgPlotAuthoringData(hexId, new[] { hexId }, PlotType.Normal));
            }

            NormalizePlotIds(asset.MutablePlots);
            return true;
        }

        public static bool TryMergeToMultiPlot(GvgMapAuthoringAsset asset, int primaryPlotId, IEnumerable<int> hexIds)
        {
            ValidateAsset(asset);
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));
            var primary = FindPlot(asset.MutablePlots, primaryPlotId);
            if (primary == null) return false;

            var mergedHexIds = new HashSet<int>(primary.HexIds);
            foreach (var hexId in hexIds)
            {
                var plot = FindPlotContainingHex(asset.MutablePlots, hexId);
                if (plot != null)
                {
                    for (var index = 0; index < plot.HexIds.Count; index++)
                    {
                        mergedHexIds.Add(plot.HexIds[index]);
                    }
                }
                else
                {
                    mergedHexIds.Add(hexId);
                }
            }

            var map = asset.CreateRuntimeMap();
            primary.HexIds.Clear();
            foreach (var hexId in mergedHexIds)
            {
                if (map.TryGetCell(hexId, out _))
                {
                    primary.HexIds.Add(hexId);
                }
            }

            for (var index = asset.MutablePlots.Count - 1; index >= 0; index--)
            {
                var plot = asset.MutablePlots[index];
                if (plot == primary) continue;
                for (var hexIndex = plot.HexIds.Count - 1; hexIndex >= 0; hexIndex--)
                {
                    if (mergedHexIds.Contains(plot.HexIds[hexIndex]))
                    {
                        plot.HexIds.RemoveAt(hexIndex);
                    }
                }
            }

            RemoveEmptyPlots(asset.MutablePlots);
            NormalizePlotIds(asset.MutablePlots);
            return primary.HexIds.Count > 0;
        }

        public static bool TryPasteHexIdsToPlot(GvgMapAuthoringAsset asset, int primaryPlotId, string text)
        {
            return TryMergeToMultiPlot(asset, primaryPlotId, ParseHexIds(text));
        }

        public static IEnumerable<int> ParseHexIds(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var parts = text.Replace("[", string.Empty).Replace("]", string.Empty)
                .Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                int hexId;
                if (int.TryParse(parts[index], out hexId))
                {
                    yield return hexId;
                }
            }
        }

        public static List<Plot> CreateRuntimePlots(GvgMapAuthoringAsset asset)
        {
            ValidateAsset(asset);
            var validation = Validate(asset);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].Message);
            }

            var map = asset.CreateRuntimeMap();
            var plots = new List<Plot>(asset.Plots.Count);
            for (var index = 0; index < asset.Plots.Count; index++)
            {
                var authoredPlot = asset.Plots[index];
                var cells = new List<HexCell>(authoredPlot.HexIds.Count);
                for (var hexIndex = 0; hexIndex < authoredPlot.HexIds.Count; hexIndex++)
                {
                    cells.Add(map.Query(authoredPlot.HexIds[hexIndex]).Cell);
                }

                var ownershipMode = authoredPlot.PlotType == PlotType.Camp
                    ? OwnershipMode.Fixed
                    : OwnershipMode.Capturable;
                var blockingState = authoredPlot.PlotType == PlotType.Obstacle
                    ? BlockingState.Blocked
                    : BlockingState.Passable;
                plots.Add(new Plot(
                    authoredPlot.PlotId,
                    cells,
                    authoredPlot.PlotType,
                    PlotState.Open,
                    FactionId.Neutral,
                    ownershipMode,
                    blockingState));
            }

            return plots;
        }

        public static GvgMapAuthoringValidationResult Validate(GvgMapAuthoringAsset asset)
        {
            ValidateAsset(asset);
            var issues = new List<GvgMapAuthoringValidationIssue>();
            RuntimeHexMap map;
            try
            {
                map = asset.CreateRuntimeMap();
            }
            catch (Exception exception)
            {
                issues.Add(new GvgMapAuthoringValidationIssue(
                    GvgMapAuthoringValidationSeverity.Error,
                    exception.Message));
                return new GvgMapAuthoringValidationResult(issues);
            }

            var plotIds = new HashSet<int>();
            var assignedHexIds = new HashSet<int>();
            for (var plotIndex = 0; plotIndex < asset.Plots.Count; plotIndex++)
            {
                var plot = asset.Plots[plotIndex];
                if (plot == null)
                {
                    AddIssue(issues, "Plot at index " + plotIndex + " is null.");
                    continue;
                }

                if (!plotIds.Add(plot.PlotId))
                {
                    AddIssue(issues, "Duplicate PlotId: " + plot.PlotId + ".");
                }

                if (plot.HexIds == null || plot.HexIds.Count == 0)
                {
                    AddIssue(issues, "Plot " + plot.PlotId + " has no HexIds.");
                    continue;
                }

                for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                {
                    var hexId = plot.HexIds[hexIndex];
                    if (!map.TryGetCell(hexId, out _))
                    {
                        AddIssue(issues, "Plot " + plot.PlotId + " references HexId outside radius: " + hexId + ".");
                    }

                    if (!assignedHexIds.Add(hexId))
                    {
                        AddIssue(issues, "HexId belongs to multiple Plots: " + hexId + ".");
                    }
                }

                if (plot.HexIds.Count == 1)
                {
                    if (plot.PlotId != plot.HexIds[0])
                    {
                        AddIssue(issues, "Single-cell Plot must use its HexId as PlotId: " + plot.PlotId + ".");
                    }
                }
                else if (plot.PlotId >= 0)
                {
                    AddIssue(issues, "Multi-cell Plot must use a negative PlotId: " + plot.PlotId + ".");
                }
            }

            var missingHexIds = new List<int>();
            for (var index = 0; index < map.Cells.Count; index++)
            {
                if (!assignedHexIds.Contains(map.Cells[index].Id))
                {
                    missingHexIds.Add(map.Cells[index].Id);
                }
            }

            if (missingHexIds.Count > 0)
            {
                AddIssue(issues, "Map has " + missingHexIds.Count + " unassigned HexIds: " + FormatFirstHexIds(missingHexIds) + ".");
            }

            return new GvgMapAuthoringValidationResult(issues);
        }

        public static void RepairForCurrentRadius(GvgMapAuthoringAsset asset)
        {
            ValidateAsset(asset);
            var map = asset.CreateRuntimeMap();
            var assignedHexIds = new HashSet<int>();
            for (var plotIndex = asset.MutablePlots.Count - 1; plotIndex >= 0; plotIndex--)
            {
                var plot = asset.MutablePlots[plotIndex];
                if (plot == null)
                {
                    asset.MutablePlots.RemoveAt(plotIndex);
                    continue;
                }

                var keptHexIds = new List<int>();
                for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                {
                    var hexId = plot.HexIds[hexIndex];
                    if (map.TryGetCell(hexId, out _) && assignedHexIds.Add(hexId))
                    {
                        keptHexIds.Add(hexId);
                    }
                }

                plot.HexIds.Clear();
                plot.HexIds.AddRange(keptHexIds);
                if (plot.HexIds.Count == 0)
                {
                    asset.MutablePlots.RemoveAt(plotIndex);
                }
            }

            for (var cellIndex = 0; cellIndex < map.Cells.Count; cellIndex++)
            {
                var cell = map.Cells[cellIndex];
                if (!assignedHexIds.Contains(cell.Id))
                {
                    asset.MutablePlots.Add(new GvgPlotAuthoringData(cell.Id, new[] { cell.Id }, PlotType.Normal));
                }
            }

            NormalizePlotIds(asset.MutablePlots);
        }

        public static Dictionary<int, GvgPlotAuthoringData> CreatePlotLookup(
            GvgMapAuthoringAsset asset,
            bool throwOnDuplicate)
        {
            ValidateAsset(asset);
            var lookup = new Dictionary<int, GvgPlotAuthoringData>();
            for (var plotIndex = 0; plotIndex < asset.Plots.Count; plotIndex++)
            {
                var plot = asset.Plots[plotIndex];
                if (plot == null || plot.HexIds == null) continue;
                for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                {
                    var hexId = plot.HexIds[hexIndex];
                    if (throwOnDuplicate)
                    {
                        lookup.Add(hexId, plot);
                    }
                    else
                    {
                        lookup[hexId] = plot;
                    }
                }
            }

            return lookup;
        }

        public static void NormalizePlotIds(IReadOnlyList<GvgPlotAuthoringData> plots)
        {
            if (plots == null) throw new ArgumentNullException(nameof(plots));
            var usedIds = new HashSet<int>();
            var preservedMultiPlots = new HashSet<GvgPlotAuthoringData>();
            var preservedMultiPlotIds = new HashSet<int>();
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.HexIds == null || plot.HexIds.Count != 1) continue;
                plot.PlotId = plot.HexIds[0];
                usedIds.Add(plot.PlotId);
            }

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.HexIds == null || plot.HexIds.Count <= 1 || plot.PlotId >= 0) continue;
                if (!preservedMultiPlotIds.Add(plot.PlotId)) continue;
                preservedMultiPlots.Add(plot);
            }

            foreach (var plotId in preservedMultiPlotIds)
            {
                usedIds.Add(plotId);
            }

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.HexIds == null || plot.HexIds.Count <= 1) continue;
                if (preservedMultiPlots.Contains(plot)) continue;

                plot.PlotId = AllocateNegativePlotId(usedIds);
                usedIds.Add(plot.PlotId);
            }
        }

        private static void RemoveHexFromOtherPlots(
            List<GvgPlotAuthoringData> plots,
            GvgPlotAuthoringData target,
            int hexId)
        {
            for (var index = plots.Count - 1; index >= 0; index--)
            {
                var plot = plots[index];
                if (plot == target) continue;
                plot.HexIds.Remove(hexId);
            }
        }

        private static void RemoveEmptyPlots(List<GvgPlotAuthoringData> plots)
        {
            for (var index = plots.Count - 1; index >= 0; index--)
            {
                if (plots[index] == null || plots[index].HexIds == null || plots[index].HexIds.Count == 0)
                {
                    plots.RemoveAt(index);
                }
            }
        }

        private static GvgPlotAuthoringData FindPlot(List<GvgPlotAuthoringData> plots, int plotId)
        {
            for (var index = 0; index < plots.Count; index++)
            {
                if (plots[index] != null && plots[index].PlotId == plotId) return plots[index];
            }

            return null;
        }

        private static GvgPlotAuthoringData FindPlotContainingHex(List<GvgPlotAuthoringData> plots, int hexId)
        {
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot != null && plot.HexIds != null && plot.HexIds.Contains(hexId)) return plot;
            }

            return null;
        }

        private static int AllocateNegativePlotId(HashSet<int> usedIds)
        {
            var plotId = -1;
            while (usedIds.Contains(plotId))
            {
                plotId--;
            }

            return plotId;
        }

        private static void AddIssue(List<GvgMapAuthoringValidationIssue> issues, string message)
        {
            issues.Add(new GvgMapAuthoringValidationIssue(GvgMapAuthoringValidationSeverity.Error, message));
        }

        private static string FormatFirstHexIds(List<int> hexIds)
        {
            var limit = Math.Min(hexIds.Count, 8);
            var values = new string[limit];
            for (var index = 0; index < limit; index++)
            {
                values[index] = hexIds[index].ToString();
            }

            return string.Join(",", values);
        }

        private static void ValidateAsset(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
        }
    }
}
