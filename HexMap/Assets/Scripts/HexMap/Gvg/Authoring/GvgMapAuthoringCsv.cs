using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HexMap.Gvg.Authoring
{
    public static class GvgMapAuthoringCsv
    {
        public const string FileNameFormat = "GVGMap_{0}.csv";

        public static string CreateGvgMapCsv(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var sortedPlots = new List<GvgPlotAuthoringData>();
            for (var index = 0; index < asset.Plots.Count; index++)
            {
                sortedPlots.Add(asset.Plots[index]);
            }

            sortedPlots.Sort((left, right) => left.PlotId.CompareTo(right.PlotId));
            var builder = new StringBuilder();
            builder.AppendLine("PlotId,HexIds,PlotType,Start,End,AffiliatedCampId");

            for (var index = 0; index < sortedPlots.Count; index++)
            {
                var plot = sortedPlots[index];
                builder.Append(plot.PlotId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(FormatHexIds(plot.HexIds))).Append(',')
                    .Append(((int)plot.PlotType).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(plot.Start.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(plot.End.ToString(CultureInfo.InvariantCulture)).Append(",")
                    .Append(plot.AffiliatedCampId.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            return builder.ToString();
        }

        public static Dictionary<string, string> CreateFiles(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var fileName = string.Format(
                CultureInfo.InvariantCulture,
                FileNameFormat,
                asset.MapId);

            return new Dictionary<string, string>
            {
                { fileName, CreateGvgMapCsv(asset) }
            };
        }

        public static string CreateReadme(GvgMapAuthoringAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var builder = new StringBuilder();
            builder.AppendLine("# GVG Map Export");
            builder.AppendLine();
            builder.AppendLine("This directory is generated from the Unity ScriptableObject authoring asset.");
            builder.AppendLine();
            builder.AppendLine("## File");
            builder.AppendLine();
            builder.AppendLine("One GVGMap_<MapId>.csv file is generated with one row per Plot.");
            builder.AppendLine();
            builder.AppendLine("The CSV is UTF-8 with BOM for Excel compatibility.");
            builder.AppendLine("Header: PlotId,HexIds,PlotType,Start,End,AffiliatedCampId.");
            builder.AppendLine("HexIds uses a quoted JSON-like array with no spaces, for example [1,2,3].");
            builder.AppendLine("Start and End are seconds from GVG start; the interval is [Start, End), and End=-1 means forever.");
            builder.AppendLine("AffiliatedCampId is -1 for ordinary Plots or the stable PlotId of the Camp Plot.");
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
            builder.AppendLine("- A single-cell Plot with one or more layers uses HexId for the first layer.");
            builder.AppendLine("- Later single-cell layers use the global sequence beginning at the next whole hundred after MaxHexId.");
            builder.AppendLine("- Multi-cell PlotId = 10000 + 1000 * (int)PlotType + sequence.");
            builder.AppendLine("- Current PlotType ranges are Camp 11000+, Normal 12000+, Grass 13000+, SmallCity 14000+, BigCity 15000+, Capital 16000+, and Obstacle 17000+.");
            builder.AppendLine("- Existing IDs are retained when valid; new IDs are not reused after deletion.");
            builder.AppendLine();
            builder.AppendLine("## Authoring Validation");
            builder.AppendLine();
            builder.AppendLine("- Single-cell layers for one Hex must use one PlotType and contiguous, non-overlapping [Start, End) intervals.");
            builder.AppendLine("- The first layer may start after zero; End=-1 is allowed only on the final layer.");
            builder.AppendLine("- Multi-cell Plots must use Start=0 and End=-1.");
            builder.AppendLine("- PlotScheduleService and runtime time scheduling are outside this export.");
            return builder.ToString();
        }

        private static string FormatHexIds(List<int> hexIds)
        {
            if (hexIds == null) throw new ArgumentNullException(nameof(hexIds));

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
            if (value == null) return "\"\"";
            var quote = '"';
            return quote + value.Replace(quote.ToString(), new string(quote, 2)) + quote;
        }
    }
}
