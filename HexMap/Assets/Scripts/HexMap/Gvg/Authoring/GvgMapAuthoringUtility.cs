#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;

namespace HexMap.Gvg.Authoring
{
    public static class GvgMapAuthoringUtility
    {
        public static List<GvgPlotAuthoringData> CreateDefaultPlots(RuntimeHexMap map)
        {
            ValidateMap(map);
            var plots = new List<GvgPlotAuthoringData>(map.Count);
            for (var index = 0; index < map.Cells.Count; index++)
            {
                var cell = map.Cells[index];
                plots.Add(new GvgPlotAuthoringData(cell.Id, new[] { cell.Id }, PlotType.Normal, 0, -1));
            }

            return plots;
        }

        public static int CalculateTimedSinglePlotIdBase(int maxHexId)
        {
            if (maxHexId < 0) throw new ArgumentOutOfRangeException(nameof(maxHexId));
            return ((maxHexId / 100) + 1) * 100;
        }

        public static int CalculateMultiPlotIdBase(PlotType plotType)
        {
            if (!Enum.IsDefined(typeof(PlotType), plotType))
                throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "PlotType is not defined.");
            return 10000 + 1000 * (int)plotType;
        }

        public static bool IsValidMultiPlotId(int plotId, PlotType plotType)
        {
            var baseId = CalculateMultiPlotIdBase(plotType);
            return plotId >= baseId && plotId < baseId + 1000;
        }

        public static void ResetToDefaultPlots(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            asset.ReplacePlots(CreateDefaultPlots(map));
        }

        public static void RebuildRadius(GvgMapAuthoringAsset asset, RuntimeHexMap map, int radius)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            if (radius != map.Radius.Radius)
                throw new InvalidOperationException("Changing the scene map topology from Radius " + map.Radius.Radius + " is not supported by the current GVG authoring version.");
        }

        public static bool TryPaintAdd(GvgMapAuthoringAsset asset, RuntimeHexMap map, int plotId, int hexId)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            if (!map.TryGetCell(hexId, out _)) return false;

            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null) return false;

            RemoveHexFromOtherPlots(asset.MutablePlots, plot, hexId);
            if (!plot.HexIds.Contains(hexId))
            {
                plot.HexIds.Add(hexId);
            }

            if (plot.HexIds.Count > 1)
            {
                plot.Start = 0;
                plot.End = -1;
                RemoveOverlappingPlotsExceptTarget(asset.MutablePlots, plot);
            }

            RemoveEmptyPlots(asset.MutablePlots);
            NormalizePlotIds(asset, map);
            return true;
        }

        public static bool TryPaintRemove(GvgMapAuthoringAsset asset, RuntimeHexMap map, int plotId, int hexId)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null || !plot.HexIds.Contains(hexId) || plot.HexIds.Count <= 1) return false;

            plot.HexIds.Remove(hexId);
            if (!ContainsHex(asset.MutablePlots, hexId))
            {
                asset.MutablePlots.Add(new GvgPlotAuthoringData(hexId, new[] { hexId }, PlotType.Normal, 0, -1));
            }

            NormalizePlotIds(asset, map);
            return true;
        }

        public static bool TryDeletePlot(GvgMapAuthoringAsset asset, RuntimeHexMap map, int plotId)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            var plot = FindPlot(asset.MutablePlots, plotId);
            if (plot == null || IsPlotReferenced(asset.MutablePlots, plotId, plot)) return false;

            var removedHexIds = new List<int>(plot.HexIds);
            asset.MutablePlots.Remove(plot);
            for (var index = 0; index < removedHexIds.Count; index++)
            {
                var hexId = removedHexIds[index];
                if (!ContainsHex(asset.MutablePlots, hexId))
                {
                    asset.MutablePlots.Add(new GvgPlotAuthoringData(hexId, new[] { hexId }, PlotType.Normal, 0, -1));
                }
            }

            NormalizePlotIds(asset, map);
            return true;
        }

        public static bool TryMergeToMultiPlot(GvgMapAuthoringAsset asset, RuntimeHexMap map, int primaryPlotId, IEnumerable<int> hexIds)
        {
            ValidateAsset(asset);
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));

            var primary = FindPlot(asset.MutablePlots, primaryPlotId);
            if (primary == null) return false;

            var mergedHexIds = new HashSet<int>(primary.HexIds);
            var plotsToRemove = new HashSet<GvgPlotAuthoringData>();
            ValidateMap(map);

            foreach (var hexId in hexIds)
            {
                if (!map.TryGetCell(hexId, out _)) continue;
                mergedHexIds.Add(hexId);

                for (var index = 0; index < asset.MutablePlots.Count; index++)
                {
                    var candidate = asset.MutablePlots[index];
                    if (candidate != null && candidate.HexIds.Contains(hexId))
                    {
                        plotsToRemove.Add(candidate);
                        for (var candidateHexIndex = 0; candidateHexIndex < candidate.HexIds.Count; candidateHexIndex++)
                        {
                            mergedHexIds.Add(candidate.HexIds[candidateHexIndex]);
                        }
                    }
                }
            }

            for (var index = 0; index < asset.MutablePlots.Count; index++)
            {
                var candidate = asset.MutablePlots[index];
                if (candidate == null || candidate == primary || plotsToRemove.Contains(candidate)) continue;
                for (var hexIndex = 0; hexIndex < candidate.HexIds.Count; hexIndex++)
                {
                    if (mergedHexIds.Contains(candidate.HexIds[hexIndex]))
                    {
                        plotsToRemove.Add(candidate);
                        break;
                    }
                }
            }
            foreach (var candidate in plotsToRemove)
            {
                if (candidate != primary) asset.MutablePlots.Remove(candidate);
            }

            primary.HexIds.Clear();
            foreach (var hexId in mergedHexIds)
            {
                if (map.TryGetCell(hexId, out _)) primary.HexIds.Add(hexId);
            }

            primary.Start = 0;
            primary.End = -1;
            RemoveEmptyPlots(asset.MutablePlots);
            NormalizePlotIds(asset, map);
            return primary.HexIds.Count > 1;
        }

        public static bool TryPasteHexIdsToPlot(GvgMapAuthoringAsset asset, RuntimeHexMap map, int primaryPlotId, string text)
        {
            return TryMergeToMultiPlot(asset, map, primaryPlotId, ParseHexIds(text));
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

        public static List<Plot> CreateRuntimePlots(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            var validation = Validate(asset, map);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].Message);
            }

            var rows = GvgMapAuthoringRuntimeAdapter.ToRuntimeData(asset);
            PlotRegistry registry;
            string error;
            if (!GvgMapRuntimeComposer.TryCompose(map, rows, out registry, out error))
            {
                throw new InvalidOperationException(error);
            }

            return new List<Plot>(registry.Plots);
        }

        /// <summary>
        /// Fast topology check: verifies every authored HexId fits inside the given map.
        /// A map is compatible when its cell count covers the largest authored HexId.
        /// </summary>
        public static bool IsCompatibleWithMap(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            if (asset.Plots.Count == 0) return true;
            var mapMaxHexId = MaxHexId(map);
            var assetMaxHexId = MaxHexIdFromPlots(asset.Plots);
            return assetMaxHexId <= mapMaxHexId;
        }

        public static GvgMapAuthoringValidationResult Validate(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            var issues = new List<GvgMapAuthoringValidationIssue>();

            var plotIds = new HashSet<int>();
            var singlePlotsByHex = new Dictionary<int, List<GvgPlotAuthoringData>>();
            var multiPlotByHex = new Dictionary<int, GvgPlotAuthoringData>();

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

                if (!Enum.IsDefined(typeof(PlotType), plot.PlotType))
                {
                    AddIssue(issues, "Plot " + plot.PlotId + " has an undefined PlotType value: " + (int)plot.PlotType + ".");
                }

                if (plot.HexIds == null || plot.HexIds.Count == 0)
                {
                    AddIssue(issues, "Plot " + plot.PlotId + " has no HexIds.");
                    continue;
                }

                if (plot.Start < 0)
                {
                    AddIssue(issues, "Plot " + plot.PlotId + " has a negative Start.");
                }

                if (plot.End != -1 && plot.End <= plot.Start)
                {
                    AddIssue(issues, "Plot " + plot.PlotId + " must have End > Start or End = -1.");
                }

                var plotHexIds = new HashSet<int>();
                for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                {
                    var hexId = plot.HexIds[hexIndex];
                    if (!plotHexIds.Add(hexId))
                    {
                        AddIssue(issues, "Plot " + plot.PlotId + " contains duplicate HexId: " + hexId + ".");
                    }

                    if (!map.TryGetCell(hexId, out _))
                    {
                        AddIssue(issues, "Plot " + plot.PlotId + " references HexId outside radius: " + hexId + ".");
                    }
                }

                if (plot.IsMultiCell)
                {
                    if (plot.Start != 0 || plot.End != -1)
                    {
                        AddIssue(issues, "Multi-cell Plot " + plot.PlotId + " must use Start=0 and End=-1.");
                    }

                    if (Enum.IsDefined(typeof(PlotType), plot.PlotType) &&
                        !IsValidMultiPlotId(plot.PlotId, plot.PlotType))
                    {
                        AddIssue(issues, "Multi-cell Plot " + plot.PlotId + " does not match its PlotType ID range.");
                    }

                    for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                    {
                        var hexId = plot.HexIds[hexIndex];
                        GvgPlotAuthoringData existingMulti;
                        if (multiPlotByHex.TryGetValue(hexId, out existingMulti) && existingMulti != plot)
                        {
                            AddIssue(issues, "HexId belongs to multiple multi-cell Plots: " + hexId + ".");
                        }
                        else
                        {
                            multiPlotByHex[hexId] = plot;
                        }
                    }
                }
                else
                {
                    List<GvgPlotAuthoringData> layers;
                    if (!singlePlotsByHex.TryGetValue(plot.HexIds[0], out layers))
                    {
                        layers = new List<GvgPlotAuthoringData>();
                        singlePlotsByHex.Add(plot.HexIds[0], layers);
                    }

                    layers.Add(plot);
                }
            }

            for (var plotIndex = 0; plotIndex < asset.Plots.Count; plotIndex++)
            {
                var plot = asset.Plots[plotIndex];
                if (plot == null || plot.AffiliatedCampId == Plot.NoAffiliatedCampId || plot.IsMultiCell) continue;

                List<GvgPlotAuthoringData> layers;
                if (singlePlotsByHex.TryGetValue(plot.HexIds[0], out layers) && layers.Count > 1)
                {
                    AddIssue(issues, "Timed single-cell Plot " + plot.PlotId + " cannot have an AffiliatedCampId.");
                }
            }
            var timedBase = map.Cells.Count == 0
                ? 0
                : CalculateTimedSinglePlotIdBase(MaxHexId(map));

            foreach (var pair in singlePlotsByHex)
            {
                var hexId = pair.Key;
                var layers = pair.Value;
                layers.Sort(CompareLayers);

                var expectedType = layers[0].PlotType;
                for (var index = 0; index < layers.Count; index++)
                {
                    var layer = layers[index];
                    if (layer.PlotType != expectedType)
                    {
                        AddIssue(issues, "HexId " + hexId + " has multiple single-cell PlotTypes.");
                    }

                    if (index == 0)
                    {
                        if (layer.PlotId != hexId)
                        {
                            AddIssue(issues, "First single-cell layer for HexId " + hexId + " must use PlotId " + hexId + ".");
                        }
                    }
                    else
                    {
                        if (layer.PlotId < timedBase)
                        {
                            AddIssue(issues, "Later single-cell layer for HexId " + hexId + " must use PlotId >= " + timedBase + ".");
                        }

                        if (layers[index - 1].End != layer.Start)
                        {
                            AddIssue(issues, "Single-cell layers for HexId " + hexId + " must be contiguous.");
                        }
                    }

                    if (index == layers.Count - 1 && layer.End != -1)
                    {
                        AddIssue(issues, "Final single-cell layer for HexId " + hexId + " must use End=-1.");
                    }

                    if (layer.End == -1 && index != layers.Count - 1)
                    {
                        AddIssue(issues, "Only the final single-cell layer for HexId " + hexId + " may use End=-1.");
                    }
                }

                if (multiPlotByHex.ContainsKey(hexId))
                {
                    AddIssue(issues, "HexId " + hexId + " cannot belong to a multi-cell Plot and a single-cell schedule.");
                }
            }

            for (var index = 0; index < map.Cells.Count; index++)
            {
                var hexId = map.Cells[index].Id;
                if (!singlePlotsByHex.ContainsKey(hexId) && !multiPlotByHex.ContainsKey(hexId))
                {
                    AddIssue(issues, "Map HexId " + hexId + " is not assigned to a Plot or single-cell schedule.");
                }
            }

            return new GvgMapAuthoringValidationResult(issues);
        }

        public static void RepairForCurrentMap(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            throw new InvalidOperationException("Repairing coverage after a scene map topology change is not supported in the current GVG authoring version.");
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
                    GvgPlotAuthoringData existing;
                    if (lookup.TryGetValue(hexId, out existing))
                    {
                        if (throwOnDuplicate)
                            throw new InvalidOperationException("HexId has multiple authoring Plot layers: " + hexId + ".");
                        if (CompareLayers(plot, existing) < 0)
                            lookup[hexId] = plot;
                    }
                    else
                    {
                        lookup.Add(hexId, plot);
                    }
                }
            }

            return lookup;
        }

        /// <summary>
        /// Normalizes every PlotId and returns an old-to-new PlotId mapping. The
        /// mapping lets Excel redundancy rows follow a Plot when the editor merges,
        /// splits, or re-times layers. Excel redundancy rows are reconciled in place:
        /// rows re-key through the mapping, rows for deleted Plots are dropped, and
        /// rows for brand-new Plots are created with per-column defaults.
        /// </summary>
        public static Dictionary<int, int> NormalizePlotIds(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            ValidateAsset(asset);
            ValidateMap(map);
            var timedBase = map.Cells.Count == 0 ? 0 : CalculateTimedSinglePlotIdBase(MaxHexId(map));
            var usedIds = new HashSet<int>();
            var retainedPlots = new HashSet<GvgPlotAuthoringData>();
            var originalPlotIds = CaptureOriginalPlotIds(asset.MutablePlots);
            var originalIdCounts = new Dictionary<int, int>();

            for (var index = 0; index < asset.MutablePlots.Count; index++)
            {
                var plot = asset.MutablePlots[index];
                if (plot == null) continue;
                int count;
                originalIdCounts.TryGetValue(plot.PlotId, out count);
                originalIdCounts[plot.PlotId] = count + 1;
            }

            var singleGroups = new Dictionary<int, List<GvgPlotAuthoringData>>();
            for (var index = 0; index < asset.MutablePlots.Count; index++)
            {
                var plot = asset.MutablePlots[index];
                if (plot == null || plot.HexIds == null || plot.HexIds.Count != 1) continue;

                List<GvgPlotAuthoringData> layers;
                if (!singleGroups.TryGetValue(plot.HexIds[0], out layers))
                {
                    layers = new List<GvgPlotAuthoringData>();
                    singleGroups.Add(plot.HexIds[0], layers);
                }

                layers.Add(plot);
            }

            foreach (var pair in singleGroups)
            {
                pair.Value.Sort(CompareLayers);
                pair.Value[0].PlotId = pair.Key;
                usedIds.Add(pair.Key);
            }

            for (var index = 0; index < asset.MutablePlots.Count; index++)
            {
                var plot = asset.MutablePlots[index];
                if (plot == null || !plot.IsMultiCell) continue;
                if (Enum.IsDefined(typeof(PlotType), plot.PlotType) &&
                    IsValidMultiPlotId(plot.PlotId, plot.PlotType) &&
                    !usedIds.Contains(plot.PlotId) &&
                    originalIdCounts[plot.PlotId] == 1)
                {
                    usedIds.Add(plot.PlotId);
                    retainedPlots.Add(plot);
                }
            }

            foreach (var pair in singleGroups)
            {
                for (var index = 1; index < pair.Value.Count; index++)
                {
                    var plot = pair.Value[index];
                    if (plot.PlotId >= timedBase &&
                        !usedIds.Contains(plot.PlotId) &&
                        originalIdCounts[plot.PlotId] == 1)
                    {
                        usedIds.Add(plot.PlotId);
                        retainedPlots.Add(plot);
                    }
                }
            }

            var nextTimedId = timedBase;
            foreach (var pair in singleGroups)
            {
                for (var index = 1; index < pair.Value.Count; index++)
                {
                    var plot = pair.Value[index];
                    if (retainedPlots.Contains(plot)) continue;
                    while (usedIds.Contains(nextTimedId)) nextTimedId++;
                    plot.PlotId = nextTimedId++;
                    usedIds.Add(plot.PlotId);
                }
            }

            for (var index = 0; index < asset.MutablePlots.Count; index++)
            {
                var plot = asset.MutablePlots[index];
                if (plot == null || !plot.IsMultiCell || retainedPlots.Contains(plot)) continue;

                var nextId = CalculateMultiPlotIdBase(plot.PlotType);
                while (usedIds.Contains(nextId)) nextId++;
                plot.PlotId = nextId;
                usedIds.Add(nextId);
            }
            RemapAffiliatedCampIds(asset.MutablePlots, originalPlotIds);
            var mapping = BuildPlotIdMapping(asset.MutablePlots, originalPlotIds);
            ReconcileExcelRedundancy(asset, mapping);
            return mapping;
        }

        private static Dictionary<int, int> BuildPlotIdMapping(
            IReadOnlyList<GvgPlotAuthoringData> plots,
            IReadOnlyDictionary<GvgPlotAuthoringData, int> originalPlotIds)
        {
            var mapping = new Dictionary<int, int>();
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null) continue;

                int originalPlotId;
                if (!originalPlotIds.TryGetValue(plot, out originalPlotId)) continue;
                if (mapping.ContainsKey(originalPlotId)) continue;
                mapping.Add(originalPlotId, plot.PlotId);
            }

            return mapping;
        }

        private static void ReconcileExcelRedundancy(
            GvgMapAuthoringAsset asset,
            IReadOnlyDictionary<int, int> mapping)
        {
            var redundancy = asset.ExcelRedundancy;
            if (redundancy == null) return;

            redundancy.ReKey(mapping);
            redundancy.RetainOnly(asset.Plots.Select(plot => plot.PlotId));

            for (var index = 0; index < asset.Plots.Count; index++)
            {
                var plot = asset.Plots[index];
                if (redundancy.ContainsRow(plot.PlotId)) continue;

                var row = new GvgExcelRowData();
                row.PlotId = plot.PlotId;
                for (var columnIndex = 0; columnIndex < redundancy.Columns.Count; columnIndex++)
                {
                    var column = redundancy.Columns[columnIndex];
                    row.MutableColumns.Add(new GvgExcelColumnContent(
                        column.Name,
                        redundancy.DefaultContent(column.Kind)));
                }

                redundancy.AddRow(row);
            }
        }
        private static Dictionary<GvgPlotAuthoringData, int> CaptureOriginalPlotIds(
            IReadOnlyList<GvgPlotAuthoringData> plots)
        {
            var originalPlotIds = new Dictionary<GvgPlotAuthoringData, int>();
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot != null) originalPlotIds[plot] = plot.PlotId;
            }

            return originalPlotIds;
        }

        private static void RemapAffiliatedCampIds(
            IReadOnlyList<GvgPlotAuthoringData> plots,
            IReadOnlyDictionary<GvgPlotAuthoringData, int> originalPlotIds)
        {
            var normalizedCampIds = new Dictionary<int, int>();
            var ambiguousCampIds = new HashSet<int>();
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.PlotType != PlotType.Camp) continue;

                int originalPlotId;
                if (!originalPlotIds.TryGetValue(plot, out originalPlotId)) continue;

                int normalizedPlotId;
                if (normalizedCampIds.TryGetValue(originalPlotId, out normalizedPlotId))
                {
                    if (normalizedPlotId != plot.PlotId) ambiguousCampIds.Add(originalPlotId);
                }
                else
                {
                    normalizedCampIds.Add(originalPlotId, plot.PlotId);
                }
            }

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.AffiliatedCampId == Plot.NoAffiliatedCampId) continue;
                if (ambiguousCampIds.Contains(plot.AffiliatedCampId)) continue;

                int normalizedCampId;
                if (normalizedCampIds.TryGetValue(plot.AffiliatedCampId, out normalizedCampId))
                {
                    plot.AffiliatedCampId = normalizedCampId;
                }
            }
        }
        private static int MaxHexId(RuntimeHexMap map)
        {
            var maxHexId = 0;
            for (var index = 0; index < map.Cells.Count; index++)
            {
                if (map.Cells[index].Id > maxHexId) maxHexId = map.Cells[index].Id;
            }

            return maxHexId;
        }

        private static int CompareLayers(GvgPlotAuthoringData left, GvgPlotAuthoringData right)
        {
            var result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.PlotId.CompareTo(right.PlotId);
        }

        private static bool ContainsHex(List<GvgPlotAuthoringData> plots, int hexId)
        {
            for (var index = 0; index < plots.Count; index++)
            {
                if (plots[index] != null && plots[index].HexIds.Contains(hexId)) return true;
            }

            return false;
        }

        private static void RemoveOverlappingPlotsExceptTarget(
            List<GvgPlotAuthoringData> plots,
            GvgPlotAuthoringData target)
        {
            var targetHexIds = new HashSet<int>(target.HexIds);
            for (var index = plots.Count - 1; index >= 0; index--)
            {
                var plot = plots[index];
                if (plot == null || plot == target) continue;
                for (var hexIndex = plot.HexIds.Count - 1; hexIndex >= 0; hexIndex--)
                {
                    if (targetHexIds.Contains(plot.HexIds[hexIndex]))
                    {
                        plot.HexIds.RemoveAt(hexIndex);
                    }
                }

                if (plot.HexIds.Count == 0) plots.RemoveAt(index);
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
                if (plot.HexIds.Count == 0) plots.RemoveAt(index);
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

        private static bool IsPlotReferenced(
            IReadOnlyList<GvgPlotAuthoringData> plots,
            int plotId,
            GvgPlotAuthoringData excludedPlot)
        {
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot != null && plot != excludedPlot && plot.AffiliatedCampId == plotId)
                {
                    return true;
                }
            }

            return false;
        }
        private static GvgPlotAuthoringData FindPlot(List<GvgPlotAuthoringData> plots, int plotId)
        {
            for (var index = 0; index < plots.Count; index++)
            {
                if (plots[index] != null && plots[index].PlotId == plotId) return plots[index];
            }

            return null;
        }

        private static int MaxHexIdFromPlots(IReadOnlyList<GvgPlotAuthoringData> plots)
        {
            var maxHexId = 0;
            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || plot.HexIds == null) continue;
                for (var hexIndex = 0; hexIndex < plot.HexIds.Count; hexIndex++)
                {
                    maxHexId = Math.Max(maxHexId, plot.HexIds[hexIndex]);
                }
            }

            return maxHexId;
        }

        public static void NormalizePlotIds(IReadOnlyList<GvgPlotAuthoringData> plots)
        {
            if (plots == null) throw new ArgumentNullException(nameof(plots));
            var usedIds = new HashSet<int>();
            var retainedPlots = new HashSet<GvgPlotAuthoringData>();
            var originalPlotIds = CaptureOriginalPlotIds(plots);
            var maxHexId = MaxHexIdFromPlots(plots);
            var timedBase = CalculateTimedSinglePlotIdBase(maxHexId);
            var groups = new Dictionary<int, List<GvgPlotAuthoringData>>();

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || !plot.IsMultiCell)
                {
                    if (plot != null && plot.HexIds != null && plot.HexIds.Count == 1)
                    {
                        List<GvgPlotAuthoringData> layers;
                        if (!groups.TryGetValue(plot.HexIds[0], out layers))
                        {
                            layers = new List<GvgPlotAuthoringData>();
                            groups.Add(plot.HexIds[0], layers);
                        }

                        layers.Add(plot);
                    }
                }
            }

            foreach (var pair in groups)
            {
                pair.Value.Sort(CompareLayers);
                pair.Value[0].PlotId = pair.Key;
                usedIds.Add(pair.Key);
            }

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || !plot.IsMultiCell) continue;
                if (IsValidMultiPlotId(plot.PlotId, plot.PlotType) && !usedIds.Contains(plot.PlotId))
                {
                    usedIds.Add(plot.PlotId);
                    retainedPlots.Add(plot);
                }
            }

            foreach (var pair in groups)
            {
                for (var index = 1; index < pair.Value.Count; index++)
                {
                    var plot = pair.Value[index];
                    if (plot.PlotId >= timedBase && !usedIds.Contains(plot.PlotId))
                    {
                        usedIds.Add(plot.PlotId);
                        retainedPlots.Add(plot);
                    }
                }
            }

            var nextTimedId = timedBase;
            foreach (var pair in groups)
            {
                for (var index = 1; index < pair.Value.Count; index++)
                {
                    var plot = pair.Value[index];
                    if (retainedPlots.Contains(plot)) continue;
                    while (usedIds.Contains(nextTimedId)) nextTimedId++;
                    plot.PlotId = nextTimedId++;
                    usedIds.Add(plot.PlotId);
                }
            }

            for (var index = 0; index < plots.Count; index++)
            {
                var plot = plots[index];
                if (plot == null || !plot.IsMultiCell || retainedPlots.Contains(plot)) continue;
                var nextId = CalculateMultiPlotIdBase(plot.PlotType);
                while (usedIds.Contains(nextId)) nextId++;
                plot.PlotId = nextId;
                usedIds.Add(nextId);
            }
            RemapAffiliatedCampIds(plots, originalPlotIds);
        }
        private static void AddIssue(List<GvgMapAuthoringValidationIssue> issues, string message)
        {
            issues.Add(new GvgMapAuthoringValidationIssue(GvgMapAuthoringValidationSeverity.Error, message));
        }

        private static void ValidateAsset(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
        }
        private static void ValidateMap(RuntimeHexMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
        }
    }
}
#endif
