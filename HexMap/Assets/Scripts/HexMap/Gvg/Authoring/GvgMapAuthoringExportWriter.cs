using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HexMap.Gvg.Authoring
{
    public static class GvgMapAuthoringExportWriter
    {
        public static void WriteFiles(string directory, IDictionary<string, string> files)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Export directory is required.", nameof(directory));
            if (files == null) throw new ArgumentNullException(nameof(files));

            Directory.CreateDirectory(directory);
            var encoding = new UTF8Encoding(true);
            foreach (var file in files)
            {
                File.WriteAllText(Path.Combine(directory, file.Key), file.Value, encoding);
            }
        }
    }
}