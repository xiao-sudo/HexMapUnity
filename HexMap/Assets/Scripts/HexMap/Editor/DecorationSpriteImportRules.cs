#if UNITY_EDITOR
namespace HexMap.Editor
{
    /// <summary>
    /// The path half of "is this texture a decoration texture", written so that it can be asserted
    /// without an importer, an asset or an editor session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Subfolders are covered.</b> A rule that stopped at the top level would go quiet the first
    /// time an artist tidied art into a subfolder, and the symptom — "that texture did not become a
    /// Sprite" — would have no visible cause at the place the art was dropped.
    /// </para>
    /// <para>
    /// <b>The prefix test requires the separator.</b> <c>Assets/HexMap/Res</c> must not claim
    /// <c>Assets/HexMap/Resources</c>. That single character is the whole reason this is a function
    /// instead of a <c>StartsWith</c> call at the call site, and it is the case most likely to be
    /// broken by a later "simplification".
    /// </para>
    /// </remarks>
    public static class DecorationSpriteImportRules
    {
        /// <summary>
        /// Whether <paramref name="assetPath"/> sits inside any of <paramref name="folders"/>, at any
        /// depth. A null or empty folder list covers nothing.
        /// </summary>
        public static bool IsInAnyFolder(string assetPath, string[] folders)
        {
            if (string.IsNullOrEmpty(assetPath) || folders == null)
            {
                return false;
            }

            var path = Normalize(assetPath);

            for (var index = 0; index < folders.Length; index++)
            {
                var folder = Normalize(folders[index]);
                if (string.IsNullOrEmpty(folder) || path.Length <= folder.Length)
                {
                    continue;
                }

                if (path[folder.Length] == '/' &&
                    string.CompareOrdinal(path, 0, folder, 0, folder.Length) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Puts a path in the form the comparison expects: forward slashes and no trailing separator.
        /// </summary>
        /// <remarks>
        /// The replace is guarded by a search because this runs once per texture per import, and
        /// <c>string.Replace</c> allocates a copy whether or not it finds anything.
        /// </remarks>
        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var normalized = path.IndexOf('\\') < 0 ? path : path.Replace('\\', '/');
            var lastIndex = normalized.Length - 1;
            return lastIndex > 0 && normalized[lastIndex] == '/'
                ? normalized.Substring(0, lastIndex)
                : normalized;
        }
    }
}
#endif
