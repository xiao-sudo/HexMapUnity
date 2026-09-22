using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// Covers the two decoration seams that do not need to render: the quad geometry a Sprite
    /// produces, and the Material a (texture, queue) pair derives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this seam cannot cover.</b> The reason decorations must pass <c>Sprite.uv</c> through
    /// rather than synthesise a 0..1 quad is Sprite Atlas packing, and that case is not testable
    /// here. A procedurally created Sprite always reports UVs covering the whole 0..1 square: its UV
    /// space is normalised to the rect it occupies in whatever texture backs it, so neither the
    /// texture size nor the pivot can push those UVs outside the unit square. Only atlas packing
    /// remaps them into a sub-region of a larger page, and a procedural Sprite has no <c>.meta</c>,
    /// so no Sprite Atlas can pack it.
    /// </para>
    /// <para>
    /// So the passthrough test below asserts the contract (mesh UVs equal <c>Sprite.uv</c> element by
    /// element) without being able to falsify it against a hard-coded quad. The atlas case is left to
    /// manual verification: put an atlas-packed Sprite on a decoration and confirm it draws the
    /// Sprite rather than a slice of the atlas page.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DecorationGeometryTests
    {
        private const float Tolerance = 0.0001f;

        private const string TestAssetFolder = "Assets/Tests/EditMode/HexMap/UnityRuntime/TestAssets";
        private const string TestAssetPath = TestAssetFolder + "/TightDecorationSource.png";

        private readonly List<Object> m_Created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();

            for (var index = 0; index < m_Created.Count; index++)
            {
                if (m_Created[index] != null)
                {
                    Object.DestroyImmediate(m_Created[index]);
                }
            }

            m_Created.Clear();

            // The Tight-outline fixture writes a real asset through the import pipeline; leaving it
            // behind would put a generated PNG in the project after every test run.
            if (AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.DeleteAsset(TestAssetFolder);
            }
        }

        [Test]
        public void SpriteGeometryProducesAQuadCentredOnItsBounds()
        {
            // 4x2 pixels at 2 pixels per unit and a top-left anchored pivot, so the Sprite is not
            // centred on its own origin.
            var sprite = CreateSprite(4, 2, 2f, new Vector2(0f, 1f));
            var expectedSize = sprite.bounds.size;

            var mesh = DecorationMeshFactory.GetOrCreateMesh(sprite);

            Assert.That(mesh, Is.Not.Null, "a full-rect Sprite must produce a mesh");
            Assert.That(mesh.vertices.Length, Is.EqualTo(4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(6));

            var centre = Vector3.zero;
            for (var index = 0; index < mesh.vertices.Length; index++)
            {
                centre += mesh.vertices[index];
            }

            centre /= mesh.vertices.Length;

            Assert.That(centre.x, Is.EqualTo(0f).Within(Tolerance), "quad must be centred on x");
            Assert.That(centre.y, Is.EqualTo(0f).Within(Tolerance), "quad must be centred on y");
            Assert.That(mesh.bounds.size.x, Is.EqualTo(expectedSize.x).Within(Tolerance));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(expectedSize.y).Within(Tolerance));
        }

        [Test]
        public void SpriteGeometryPassesTheSpriteUvsThrough()
        {
            var sprite = CreateSprite(3, 2, 1f, new Vector2(0.25f, 0.75f));
            var expectedUvs = sprite.uv;

            var mesh = DecorationMeshFactory.GetOrCreateMesh(sprite);

            Assert.That(mesh.uv.Length, Is.EqualTo(expectedUvs.Length));
            for (var index = 0; index < expectedUvs.Length; index++)
            {
                Assert.That(mesh.uv[index].x, Is.EqualTo(expectedUvs[index].x).Within(Tolerance));
                Assert.That(mesh.uv[index].y, Is.EqualTo(expectedUvs[index].y).Within(Tolerance));
            }
        }

        [Test]
        public void SameSpriteReusesOneMeshAndDifferentSpritesDoNot()
        {
            var first = CreateSprite(4, 4, 1f, new Vector2(0.5f, 0.5f));
            var second = CreateSprite(4, 4, 1f, new Vector2(0.5f, 0.5f));

            var a = DecorationMeshFactory.GetOrCreateMesh(first);
            var b = DecorationMeshFactory.GetOrCreateMesh(first);
            var c = DecorationMeshFactory.GetOrCreateMesh(second);

            Assert.That(b, Is.SameAs(a), "the same Sprite must share one mesh");
            Assert.That(c, Is.Not.SameAs(a), "a different Sprite must not share the mesh");
        }

        [Test]
        public void NullSpriteYieldsNoMeshInsteadOfThrowing()
        {
            Assert.That(DecorationMeshFactory.GetOrCreateMesh(null), Is.Null);
        }

        [Test]
        public void ClearDropsBothCaches()
        {
            var shader = FindDecorationShader();
            var sprite = CreateSprite(4, 4, 1f, new Vector2(0.5f, 0.5f));
            var texture = sprite.texture;

            var firstMesh = DecorationMeshFactory.GetOrCreateMesh(sprite);
            var firstMaterial = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Decoration);

            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();

            Assert.That(
                DecorationMeshFactory.GetOrCreateMesh(sprite),
                Is.Not.SameAs(firstMesh),
                "clearing the mesh cache must force a rebuild");
            Assert.That(
                DecorationMaterialCache.GetOrCreateMaterial(texture, shader, DecorationQueue.Decoration),
                Is.Not.SameAs(firstMaterial),
                "clearing the material cache must force a rebuild");
        }

        [Test]
        public void TightImportedSpriteKeepsItsOutlineMeshInsteadOfBecomingAQuad()
        {
            var sprite = CreateImportedTightSprite();
            var spriteVertices = sprite.vertices;
            Assert.That(
                spriteVertices.Length,
                Is.GreaterThan(4),
                "test fixture is not discriminating: this asset must import as a Tight outline, " +
                "not as a four-vertex rect");

            var mesh = DecorationMeshFactory.GetOrCreateMesh(sprite);

            Assert.That(mesh, Is.Not.Null, "a Tight outline mesh is supported geometry, not a rejection");
            Assert.That(
                mesh.vertices.Length,
                Is.EqualTo(spriteVertices.Length),
                "the outline must survive, not be flattened to one quad");
            Assert.That(mesh.triangles.Length, Is.EqualTo(sprite.triangles.Length));
            Assert.That(mesh.triangles.Length % 3, Is.EqualTo(0));
        }

        [Test]
        public void SameTextureAtDifferentQueuesYieldsIndependentMaterials()
        {
            var shader = FindDecorationShader();
            var texture = CreateTexture(4, 4);

            var decoration = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Decoration);
            var overlay = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Overlay);

            Assert.That(decoration, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(
                overlay,
                Is.Not.SameAs(decoration),
                "a shared Material would let the overlay overwrite the decoration's queue");
            Assert.That(decoration.renderQueue, Is.EqualTo(DecorationQueue.Decoration));
            Assert.That(overlay.renderQueue, Is.EqualTo(DecorationQueue.Overlay));
        }

        [Test]
        public void SameTextureAtTheSameQueueReusesOneMaterial()
        {
            var shader = FindDecorationShader();
            var texture = CreateTexture(4, 4);

            var first = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Decoration);
            var second = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Decoration);

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void MaterialCarriesTheTextureAsItsBaseMap()
        {
            var shader = FindDecorationShader();
            var texture = CreateTexture(4, 4);

            var material = DecorationMaterialCache.GetOrCreateMaterial(
                texture, shader, DecorationQueue.Decoration);

            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(texture));
        }

        [Test]
        public void NullTextureOrShaderYieldsNoMaterialInsteadOfThrowing()
        {
            var shader = FindDecorationShader();

            // Returning null silently would leave an invisible decoration with no explanation, so
            // the cache reports the reason. LogAssert.Expect is what turns that report into an
            // assertion instead of an unexpected-log failure.
            LogAssert.Expect(
                LogType.Error, "A decoration needs a Sprite backed by a Texture2D.");
            Assert.That(
                DecorationMaterialCache.GetOrCreateMaterial(null, shader, DecorationQueue.Decoration),
                Is.Null);

            LogAssert.Expect(
                LogType.Error,
                "The decoration shader is unavailable. It is probably stripped from the build: " +
                "assign it on the DecorationView component so the build keeps a reference to it.");
            Assert.That(
                DecorationMaterialCache.GetOrCreateMaterial(CreateTexture(4, 4), null, DecorationQueue.Decoration),
                Is.Null);
        }

        private static Shader FindDecorationShader()
        {
            var shader = Shader.Find("HexMap/Decoration");
            Assert.That(shader, Is.Not.Null, "HexMap/Decoration must exist for decoration rendering");
            return shader;
        }

        /// <summary>
        /// Imports an L-shaped PNG through the asset pipeline so Unity generates a Tight outline
        /// mesh for it, then returns the Sprite.
        /// </summary>
        /// <remarks>
        /// The import pipeline is the only thing that can produce a Tight outline.
        /// <c>Sprite.Create(..., SpriteMeshType.Tight)</c> does not: it hands back a four-vertex
        /// rect, because the outline is generated while importing, from the alpha region and the
        /// importer's tessellation detail. So this asset is written to disk and imported for real.
        /// Pixels are laid out from the bottom-left, so the top-right block is the transparent one.
        /// </remarks>
        private static Sprite CreateImportedTightSprite()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var transparentCorner = x >= size / 2 && y >= size / 2;
                    pixels[y * size + x] = transparentCorner
                        ? Color.clear
                        : new Color(1f, 1f, 1f, 1f);
                }
            }

            var source = new Texture2D(size, size, TextureFormat.RGBA32, false);
            source.SetPixels(pixels);
            source.Apply();
            var png = source.EncodeToPNG();
            Object.DestroyImmediate(source);

            Directory.CreateDirectory(TestAssetFolder);
            File.WriteAllBytes(TestAssetPath, png);
            AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TestAssetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteTessellationDetail = 0.01f;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestAssetPath);
            Assert.That(sprite, Is.Not.Null, "the imported test asset must yield a Sprite");
            return sprite;
        }

        private Sprite CreateSprite(int width, int height, float pixelsPerUnit, Vector2 pivot)
        {
            var texture = CreateTexture(width, height);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            m_Created.Add(sprite);
            return sprite;
        }

        private Texture2D CreateTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            m_Created.Add(texture);
            return texture;
        }
    }
}
