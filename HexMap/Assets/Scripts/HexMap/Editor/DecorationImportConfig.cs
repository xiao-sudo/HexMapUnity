#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Declares which folders hold decoration art, so a texture dropped into one arrives as a Sprite
    /// with the settings a decoration needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The folder is the contract.</b> Every texture under a listed folder is forced to the
    /// settings below on every import, including reimports, so a hand edit in the Inspector survives
    /// only until the next import. That is deliberate: <c>DecorationMeshFactory</c> passes
    /// <c>Sprite.uv</c> straight through, which is what makes an atlas page, a trimmed Sprite and a
    /// custom pivot all work without a single branch — and a wrong <c>spriteMeshType</c> or a missing
    /// <c>alphaIsTransparency</c> breaks that <b>silently</b> rather than raising an error. Only a
    /// folder-wide rule can hold those two still.
    /// </para>
    /// <para>
    /// <b>The asset is optional.</b> <see cref="DecorationSpriteImportPolicy"/> falls back to
    /// <see cref="DecorationSpriteImportPolicy.FallbackFolder"/> with
    /// <see cref="DecorationSpriteImportSettings.Default"/> when no config asset exists, which is why
    /// the repository does not ship one: a <c>.asset</c> file carries the script's guid, and nothing
    /// other than Unity can assign that.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "DecorationImportConfig", menuName = "Hex Map/Decoration Import Config")]
    public sealed class DecorationImportConfig : ScriptableObject
    {
        [Tooltip("Folders whose textures are imported as decoration Sprites. Subfolders are included.")]
        [SerializeField] private List<DefaultAsset> m_Folders = new List<DefaultAsset>();

        [Tooltip("Single for one Sprite per file, Multiple for a sheet.")]
        [SerializeField] private SpriteImportMode m_ImportMode = SpriteImportMode.Single;

        [Tooltip("Tight keeps the opaque outline; Full Rect keeps the whole rectangle.")]
        [SerializeField] private SpriteMeshType m_MeshType = SpriteMeshType.Tight;

        [SerializeField] private float m_PixelsPerUnit = 100f;
        [SerializeField] private bool m_AlphaIsTransparency = true;
        [SerializeField] private bool m_Mipmaps;
        [SerializeField] private TextureWrapMode m_WrapMode = TextureWrapMode.Clamp;

        /// <summary>
        /// The folder paths this config covers, in the order they were listed.
        /// </summary>
        /// <remarks>
        /// Entries that resolve to nothing are dropped rather than reported: an entry that is missing
        /// or empty covers no texture either way, and this runs during an import, where a log line
        /// per texture would be noise. An empty result is a legitimate configuration and means
        /// "cover nothing" — see <see cref="DecorationSpriteImportPolicy"/>.
        /// </remarks>
        public string[] GetFolderPaths()
        {
            var paths = new List<string>(m_Folders.Count);
            for (var index = 0; index < m_Folders.Count; index++)
            {
                var folder = m_Folders[index];
                if (folder == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(folder);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return paths.ToArray();
        }

        /// <summary>The import settings this config describes.</summary>
        public DecorationSpriteImportSettings GetSettings()
        {
            return new DecorationSpriteImportSettings(
                m_ImportMode,
                m_MeshType,
                m_PixelsPerUnit,
                m_AlphaIsTransparency,
                m_Mipmaps,
                m_WrapMode);
        }
    }

    /// <summary>
    /// The set of import settings a decoration texture gets, as a value rather than as a sequence of
    /// assignments.
    /// </summary>
    /// <remarks>
    /// Keeping this a value is what lets the decision be asserted without an importer: a test can
    /// compare two of these, whereas the assignments it replaces could only be observed by importing
    /// a real texture and re-reading the importer.
    /// </remarks>
    public readonly struct DecorationSpriteImportSettings
    {
        public DecorationSpriteImportSettings(
            SpriteImportMode importMode,
            SpriteMeshType meshType,
            float pixelsPerUnit,
            bool alphaIsTransparency,
            bool mipmaps,
            TextureWrapMode wrapMode)
        {
            ImportMode = importMode;
            MeshType = meshType;
            PixelsPerUnit = pixelsPerUnit;
            AlphaIsTransparency = alphaIsTransparency;
            Mipmaps = mipmaps;
            WrapMode = wrapMode;
        }

        /// <summary>
        /// What a decoration texture gets when no config asset says otherwise.
        /// </summary>
        /// <remarks>
        /// These are the values the hand-authored <c>Assets/HexMap/Res/*.tga.meta</c> files already
        /// carry, so making them the default changes nothing about the art that is in the project
        /// today. <c>Tight</c> and <c>alphaIsTransparency</c> are the two that cannot be relaxed:
        /// the first is what <c>DecorationMeshFactory</c> relies on for cheap fragments, the second
        /// is what stops a cut-out texture from drawing a black box.
        /// </remarks>
        public static DecorationSpriteImportSettings Default
        {
            get
            {
                return new DecorationSpriteImportSettings(
                    SpriteImportMode.Single,
                    SpriteMeshType.Tight,
                    100f,
                    true,
                    false,
                    TextureWrapMode.Clamp);
            }
        }

        public SpriteImportMode ImportMode { get; }
        public SpriteMeshType MeshType { get; }
        public float PixelsPerUnit { get; }
        public bool AlphaIsTransparency { get; }
        public bool Mipmaps { get; }
        public TextureWrapMode WrapMode { get; }

        /// <summary>
        /// Writes these settings onto <paramref name="importer"/>, overwriting whatever it holds.
        /// </summary>
        public void ApplyTo(TextureImporter importer)
        {
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = ImportMode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = AlphaIsTransparency;
            importer.mipmapEnabled = Mipmaps;
            importer.wrapMode = WrapMode;

            // spriteMeshType has no TextureImporter property of its own; it exists only on
            // TextureImporterSettings, so the current settings have to be read, changed and written
            // back rather than assigned directly.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = MeshType;
            importer.SetTextureSettings(settings);
        }

        /// <summary>
        /// Whether <paramref name="importer"/> already carries these settings.
        /// </summary>
        /// <remarks>
        /// Used to leave untouched textures alone on a sweep. Reimporting a texture rewrites its
        /// meta and re-packs the atlas it belongs to, so "reimport everything just in case" costs
        /// far more than the comparison does.
        /// </remarks>
        public bool Matches(TextureImporter importer)
        {
            if (importer == null || importer.textureType != TextureImporterType.Sprite)
            {
                return false;
            }

            if (importer.spriteImportMode != ImportMode ||
                importer.alphaIsTransparency != AlphaIsTransparency ||
                importer.mipmapEnabled != Mipmaps ||
                importer.wrapMode != WrapMode ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
            {
                return false;
            }

            var current = new TextureImporterSettings();
            importer.ReadTextureSettings(current);
            return current.spriteMeshType == MeshType;
        }
    }
}
#endif
