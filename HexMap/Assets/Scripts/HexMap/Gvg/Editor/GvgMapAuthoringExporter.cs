using System;
using System.IO;
using System.Text;
using HexMap.Gvg.Authoring;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEditor;

namespace HexMap.Gvg.Editor
{
    public static class GvgMapAuthoringExporter
    {
        public static string Export(GvgMapAuthoringAsset asset, RuntimeHexMap map)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (map == null) throw new ArgumentNullException(nameof(map));

            var validation = GvgMapAuthoringUtility.Validate(asset, map);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].Message);
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("GVG map authoring asset must be saved before export.");
            }

            var assetName = Path.GetFileNameWithoutExtension(assetPath);
            var exportDirectory = Path.Combine("Assets/HexMap/Gvg/Exports", assetName);
            GvgMapAuthoringExportWriter.WriteFiles(exportDirectory, GvgMapAuthoringCsv.CreateFiles(asset));
            AssetDatabase.Refresh();

            return Path.Combine(
                exportDirectory,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    GvgMapAuthoringCsv.FileNameFormat,
                    asset.MapId));
        }

        /// <summary>
        /// Writes the GVG CSV to an explicit file path chosen by the user (directory
        /// and file name). The CSV is UTF-8 with BOM for Excel compatibility.
        /// </summary>
        public static string ExportToFile(GvgMapAuthoringAsset asset, RuntimeHexMap map, string fullPath)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentException("Export file path is required.", nameof(fullPath));

            var validation = GvgMapAuthoringUtility.Validate(asset, map);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].Message);
            }

            var exportDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
            }

            File.WriteAllText(fullPath, GvgMapAuthoringCsv.CreateGvgMapCsv(asset), new UTF8Encoding(true));
            return fullPath;
        }
    }
}
