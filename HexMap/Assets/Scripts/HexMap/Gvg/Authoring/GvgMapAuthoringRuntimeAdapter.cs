#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexMap.Gvg.Authoring
{
    /// <summary>
    /// TEMPORARY mock provider (issue 05, 2026-09-15 grilling): bridges editor authoring
    /// data into the runtime plot table row format so the runtime composition pipeline can
    /// be exercised before the real runtime table data source is decided.
    /// Replace this with a real runtime table loader and delete this class when that
    /// data source lands (tracked by issue 06 migration).
    /// </summary>
    public static class GvgMapAuthoringRuntimeAdapter
    {
        public static IReadOnlyList<GvgPlotRuntimeData> ToRuntimeData(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var campPlotIds = new HashSet<int>();
            for (var index = 0; index < asset.Plots.Count; index++)
            {
                var plot = asset.Plots[index];
                if (plot != null && plot.PlotType == PlotType.Camp)
                {
                    campPlotIds.Add(plot.PlotId);
                }
            }

            var rows = new List<GvgPlotRuntimeData>(asset.Plots.Count);
            for (var index = 0; index < asset.Plots.Count; index++)
            {
                var plot = asset.Plots[index];
                if (plot == null)
                {
                    continue;
                }

                var affiliatedCampId = plot.AffiliatedCampId;
                if (plot.PlotType == PlotType.Camp && affiliatedCampId == Plot.NoAffiliatedCampId)
                {
                    affiliatedCampId = plot.PlotId;
                }

                if (affiliatedCampId != Plot.NoAffiliatedCampId && !campPlotIds.Contains(affiliatedCampId))
                {
                    Debug.LogError("Plot " + plot.PlotId + " references missing Camp PlotId " +
                        affiliatedCampId + ". It will be treated as a normal Plot.");
                    affiliatedCampId = Plot.NoAffiliatedCampId;
                }

                rows.Add(new GvgPlotRuntimeData(
                    plot.PlotId,
                    plot.HexIds,
                    plot.PlotType,
                    plot.Start == 0 ? 0 : 1,
                    plot.Start,
                    plot.End,
                    affiliatedCampId,
                    plot.OwnerFactionId));
            }

            return rows;
        }
    }
}
#endif