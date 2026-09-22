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
    /// <b>How far this seam reaches.</b> A Sprite covering a sub-rect of a larger texture reports
    /// UVs that address only a corner of that texture, which is the same situation as a packed
    /// atlas page. The UV assertion builds exactly that Sprite, so a hard-coded 0..1 quad fails it.
    /// It first asserts that the fixture is discriminating, so a Sprite whose UVs happened to span
    /// the whole texture cannot make the test pass vacuously.
    /// </para>
    /// <para>
    /// A real Sprite Atlas asset is still not used, because a procedural <c>Texture2D</c> has no
    /// <c>.meta</c> and cannot be packed. What that leaves unverified is the packing step itself,
    /// not the UV handling — which is why the manual check remains: put an atlas-packed Sprite on a
    /// decoration and confirm it draws the Sprite rather than a slice of the page.
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

            // Centring alone cannot falsify a degenerate quad: one built from the texture's corners
            // and then re-centred would still average to zero. Its extent gives it away, because it
            // would be a whole texture in size rather than this Sprite's bounds.
            var min = mesh.vertices[0];
            var max = mesh.vertices[0];
            for (var index = 1; index < mesh.vertices.Length; index++)
            {
                min = Vector3.Min(min, mesh.vertices[index]);
                max = Vector3.Max(max, mesh.vertices[index]);
            }

            Assert.That(
                max.x - min.x,
                Is.EqualTo(expectedSize.x).Within(Tolerance),
                "the quad must span the Sprite, not the whole texture");
            Assert.That(
                max.y - min.y,
                Is.EqualTo(expectedSize.y).Within(Tolerance),
                "the quad must span the Sprite, not the whole texture");
        }

        /// <summary>
        /// Pins the contract that lets one mesh serve both render planes.
        /// </summary>
        /// <remarks>
        /// The render plane is carried by the <c>Renderer</c> child's rotation in the prefab, never by
        /// the mesh. Building a mesh per plane would double the mesh cache and break "one Sprite, one
        /// mesh" — and it would still look correct to whoever did it, because they would be looking at
        /// one of the two planes. <c>DecorationPrefabBuilderTests</c> guards the rotation half; this
        /// guards the half that would make the rotation unnecessary.
        /// </remarks>
        [Test]
        public void TheDecorationMeshLiesInTheLocalXyPlane()
        {
            var sprite = CreateSprite(4, 4, 4f, new Vector2(0.5f, 0.5f));

            var mesh = DecorationMeshFactory.GetOrCreateMesh(sprite);

            Assert.That(mesh, Is.Not.Null, "a full-rect Sprite must produce a mesh");

            var min = mesh.vertices[0];
            var max = mesh.vertices[0];
            for (var index = 0; index < mesh.vertices.Length; index++)
            {
                var vertex = mesh.vertices[index];

                Assert.That(
                    vertex.z,
                    Is.EqualTo(0f).Within(Tolerance),
                    "every decoration mesh vertex stays in the local XY plane");

                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            // Guard the guard: an empty mesh, or one collapsed to a point, would satisfy the loop
            // above without asserting anything at all.
            Assert.That(max.x - min.x, Is.GreaterThan(0f), "the mesh must span the Sprite in x");
            Assert.That(max.y - min.y, Is.GreaterThan(0f), "the mesh must span the Sprite in y");
        }

        [Test]
        public void SubRectSpritePassesItsUvsThrough()
        {
            // A Sprite is a sub-rect of a texture in exactly this way, whether the reason is a
            // packed atlas page or a Sprite that genuinely shares its texture with others. Its UVs
            // therefore address a corner of the texture rather than the whole 0..1 square.
            var sprite = CreateSubRectSprite();
            var expectedUvs = sprite.uv;

            var minU = float.MaxValue;
            var minV = float.MaxValue;
            var maxU = float.MinValue;
            var maxV = float.MinValue;
            for (var index = 0; index < expectedUvs.Length; index++)
            {
                minU = Mathf.Min(minU, expectedUvs[index].x);
                minV = Mathf.Min(minV, expectedUvs[index].y);
                maxU = Mathf.Max(maxU, expectedUvs[index].x);
                maxV = Mathf.Max(maxV, expectedUvs[index].y);
            }

            Assert.That(
                maxU - minU < 1f - Tolerance || maxV - minV < 1f - Tolerance,
                Is.True,
                "test fixture is not discriminating: this Sprite's UVs must not span the whole " +
                "texture, or a hard-coded unit quad would pass this test");

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

        /// <summary>
        /// Builds a Sprite covering a sub-rect of a larger texture, the way a packed-atlas Sprite
        /// does: it is backed by a texture several times its size, and its own region starts at an
        /// offset. That is what makes its UVs address a corner of the texture instead of the whole
        /// 0..1 square, and therefore what lets the UV assertion falsify a hard-coded unit quad.
        /// </summary>
        private Sprite CreateSubRectSprite()
        {
            var texture = CreateTexture(4, 4);
            var sprite = Sprite.Create(
                texture,
                new Rect(1f, 1f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                4f,
                0,
                SpriteMeshType.FullRect);
            m_Created.Add(sprite);
            return sprite;
        }

        private Sprite CreateSprite(int width, int height, float pixelsPerUnit, Vector2 pivot)        {
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
