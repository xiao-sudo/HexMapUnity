using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.Editor.Tests
{
    /// <summary>
    /// Covers the rule that decides whether a texture is a decoration texture, and what it is set to.
    /// </summary>
    /// <remarks>
    /// The matching rules are pure string work on purpose, so most of this fixture needs no importer,
    /// no asset and no import. The two config cases go through
    /// <see cref="DecorationSpriteImportPolicy.PlanFor"/>, which is the same call the postprocessor
    /// makes, so what is asserted here is what an import does.
    ///
    /// The failure this guards against is silent: a wrong <c>spriteMeshType</c> or a missing
    /// <c>alphaIsTransparency</c> does not raise an error, it makes
    /// <c>DecorationMeshFactory</c>'s UV passthrough and atlas sampling quietly wrong.
    /// </remarks>
    [TestFixture]
    public sealed class DecorationSpriteImportTests
    {
        private static readonly string[] ResFolder = { DecorationSpriteImportPolicy.FallbackFolder };

        [Test]
        public void ATextureInAConfiguredFolderIsCovered()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/HexMap/Res/Tree.png", ResFolder),
                Is.True);
        }

        [Test]
        public void ATextureInASubfolderOfAConfiguredFolderIsCovered()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder(
                    "Assets/HexMap/Res/Ground/Road.png",
                    ResFolder),
                Is.True);
        }

        [Test]
        public void ATextureOutsideEveryConfiguredFolderIsNotCovered()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/HexMap/Other/Tree.png", ResFolder),
                Is.False);
        }

        [Test]
        public void AFolderThatMerelyStartsWithTheConfiguredNameIsNotCovered()
        {
            // The prefix trap: "Assets/HexMap/Resources" is not inside "Assets/HexMap/Res".
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder(
                    "Assets/HexMap/Resources/Tree.png",
                    ResFolder),
                Is.False);
        }

        [Test]
        public void TheFolderPathItselfIsNotAFile()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder(
                    DecorationSpriteImportPolicy.FallbackFolder,
                    ResFolder),
                Is.False);
        }

        [Test]
        public void AnyOneOfSeveralConfiguredFoldersCovers()
        {
            var folders = new[] { "Assets/HexMap/Res", "Assets/Art/Decorations" };

            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/HexMap/Res/Tree.png", folders),
                Is.True);
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/Art/Decorations/Tree.png", folders),
                Is.True);
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/Art/Other/Tree.png", folders),
                Is.False);
        }

        [Test]
        public void NoConfiguredFolderCoversNothing()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/HexMap/Res/Tree.png", new string[0]),
                Is.False);
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder("Assets/HexMap/Res/Tree.png", null),
                Is.False);
        }

        [Test]
        public void ATrailingSeparatorOnAConfiguredFolderChangesNothing()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder(
                    "Assets/HexMap/Res/Tree.png",
                    new[] { "Assets/HexMap/Res/" }),
                Is.True);
        }

        [Test]
        public void WindowsSeparatorsAreUnderstood()
        {
            Assert.That(
                DecorationSpriteImportRules.IsInAnyFolder(@"Assets\HexMap\Res\Tree.png", ResFolder),
                Is.True);
        }

        [Test]
        public void NoConfigAssetMeansTheFallbackFolderWithTheDefaultSettings()
        {
            var plan = DecorationSpriteImportPolicy.PlanFor(null);

            Assert.That(plan.IsFallback, Is.True, "a missing config asset is the fallback case");
            Assert.That(
                plan.Folders,
                Is.EqualTo(new[] { DecorationSpriteImportPolicy.FallbackFolder }));
            Assert.That(plan.Settings.MeshType, Is.EqualTo(DecorationSpriteImportSettings.Default.MeshType));
            Assert.That(
                plan.Settings.AlphaIsTransparency,
                Is.EqualTo(DecorationSpriteImportSettings.Default.AlphaIsTransparency));
        }

        [Test]
        public void AConfigWithNoFoldersIsNotAFallbackAndCoversNothing()
        {
            var config = ScriptableObject.CreateInstance<DecorationImportConfig>();
            try
            {
                var plan = DecorationSpriteImportPolicy.PlanFor(config);

                Assert.That(
                    plan.IsFallback,
                    Is.False,
                    "an explicit config replaces the fallback even when it lists nothing");
                Assert.That(plan.Folders, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void AFreshConfigCarriesTheDocumentedDefaults()
        {
            var config = ScriptableObject.CreateInstance<DecorationImportConfig>();
            try
            {
                var settings = DecorationSpriteImportPolicy.PlanFor(config).Settings;
                var expected = DecorationSpriteImportSettings.Default;

                Assert.That(settings.MeshType, Is.EqualTo(expected.MeshType));
                Assert.That(settings.AlphaIsTransparency, Is.EqualTo(expected.AlphaIsTransparency));
                Assert.That(settings.Mipmaps, Is.EqualTo(expected.Mipmaps));
                Assert.That(settings.WrapMode, Is.EqualTo(expected.WrapMode));
                Assert.That(settings.PixelsPerUnit, Is.EqualTo(expected.PixelsPerUnit));
                Assert.That(settings.ImportMode, Is.EqualTo(expected.ImportMode));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void AConfigDrivesTheSettingsRatherThanTheDefaults()
        {
            var config = ScriptableObject.CreateInstance<DecorationImportConfig>();
            try
            {
                var field = typeof(DecorationImportConfig).GetField(
                    "m_PixelsPerUnit",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "the serialized field is what the config editor writes to");
                field.SetValue(config, 32f);

                Assert.That(
                    DecorationSpriteImportPolicy.PlanFor(config).Settings.PixelsPerUnit,
                    Is.EqualTo(32f),
                    "the settings must come from the config, not from Default");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
