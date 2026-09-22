using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor.Tests
{
    /// <summary>
    /// Covers the one decision the decoration prefab builder makes: which plane the Sprite renders
    /// on, and where that decision is stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The plane is a rotation on the child, not a property of the mesh.</b> These tests exist
    /// because the alternative — building the mesh in XZ for XZ decorations — is both tempting and
    /// invisible: it would double the mesh cache, break "one Sprite, one mesh", and still look
    /// correct to whoever tried it, as long as they only ever opened one of the two prefabs.
    /// <c>DecorationGeometryTests.TheDecorationMeshLiesInTheLocalXyPlane</c> guards the other half
    /// of the same contract.
    /// </para>
    /// <para>
    /// <b>The fixtures here build Sprite-less prefabs on purpose.</b> The structure and the rotation
    /// do not depend on a Sprite, and leaving it out keeps this fixture clear of the mesh factory,
    /// the material cache and <c>Shader.Find</c> — so it asserts one thing and cannot fail for a
    /// reason that belongs to a different seam.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DecorationPrefabBuilderTests
    {
        private const string TestAssetParent = "Assets/Tests/EditMode/HexMap/Editor";
        private const string TestAssetFolder = TestAssetParent + "/TestAssets";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.CreateFolder(TestAssetParent, "TestAssets");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // The prefabs are real assets, so the folder is the thing to remove; destroying the
            // loaded objects directly would leave the files behind.
            if (AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.DeleteAsset(TestAssetFolder);
            }
        }

        [Test]
        public void XyLeavesTheRendererChildUnrotated()
        {
            var prefab = CreatePrefab(HexPlane.XY);

            AssertRotation(prefab, Quaternion.identity, "XY keeps the mesh's own plane");
        }

        [Test]
        public void XzTurnsTheRendererChildNinetyDegreesAboutX()
        {
            var prefab = CreatePrefab(HexPlane.XZ);

            // +90° about X maps the Sprite's local up onto world +Z, which is what the top-down
            // camera in map.unity has at the top of its frame. -90° would render the image upside
            // down, and nothing else in the pipeline would notice.
            AssertRotation(prefab, Quaternion.Euler(90f, 0f, 0f), "XZ lays the Sprite on the ground");
        }

        [Test]
        public void BothPlanesDifferOnlyByTheRendererChildsRotation()
        {
            var xy = CreatePrefab(HexPlane.XY);
            var xz = CreatePrefab(HexPlane.XZ);

            var xyChild = FindRendererChild(xy);
            var xzChild = FindRendererChild(xz);

            // The root's name is not compared: SaveAsPrefabAsset names the saved root after the file,
            // so the two skeletons legitimately differ there. What must not differ is everything that
            // makes the shape a decoration.
            Assert.That(xzChild.name, Is.EqualTo(xyChild.name), "the plane is a rotation, not a rename");
            Assert.That(xzChild.localPosition, Is.EqualTo(xyChild.localPosition));
            Assert.That(xzChild.localScale, Is.EqualTo(xyChild.localScale));
            Assert.That(
                xz.GetComponent<DecorationView>(),
                Is.Not.Null,
                "both planes carry the authoring component on the root");
            Assert.That(
                Quaternion.Angle(xyChild.localRotation, xzChild.localRotation),
                Is.EqualTo(90f).Within(0.01f),
                "the planes must be exactly a quarter turn apart");
        }

        [Test]
        public void TheRendererChildCarriesTheGeometryComponents()
        {
            var prefab = CreatePrefab(HexPlane.XZ);
            var child = FindRendererChild(prefab);

            Assert.That(child.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(child.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(
                prefab.GetComponent<DecorationView>(),
                Is.Not.Null,
                "the root is the authoring surface and must carry the component");
        }

        [Test]
        public void AnUndefinedPlaneIsRejectedRatherThanTreatedAsXy()
        {
            // HexLayout validates its enum the same way. Silently defaulting would turn a typo in a
            // future caller into "some decorations are upright for no visible reason".
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationPrefabBuilder.RotationFor((HexPlane)7); });
        }

        [Test]
        public void TheAssetNameCarriesThePlaneSoBothPlanesCanCoexist()
        {
            Assert.That(
                DecorationPrefabBuilder.BuildAssetName("Tree", HexPlane.XY),
                Is.EqualTo("Tree_Decoration_XY"));
            Assert.That(
                DecorationPrefabBuilder.BuildAssetName("Tree", HexPlane.XZ),
                Is.EqualTo("Tree_Decoration_XZ"));
        }

        private static GameObject CreatePrefab(HexPlane plane)
        {
            var assetPath = TestAssetFolder + "/Skeleton_" + plane + ".prefab";
            var prefab = DecorationPrefabBuilder.CreateAsset(assetPath, plane, null);

            Assert.That(prefab, Is.Not.Null, "the builder must hand back the saved asset");
            Assert.That(
                AssetDatabase.GetAssetPath(prefab),
                Is.EqualTo(assetPath),
                "the prefab must land at the requested path");
            return prefab;
        }

        private static Transform FindRendererChild(GameObject prefab)
        {
            var child = prefab.transform.Find(DecorationPrefabBuilder.RendererObjectName);
            Assert.That(
                child,
                Is.Not.Null,
                "the prefab must have a child named '" + DecorationPrefabBuilder.RendererObjectName + "'");
            return child;
        }

        /// <summary>
        /// Asserts the child's rotation by angle rather than by component equality.
        /// </summary>
        /// <remarks>
        /// The prefab is written to disk and read back, so the quaternion survives a float round trip
        /// through YAML; comparing components exactly would fail on the last bit rather than on the
        /// rotation. A wrong rotation — 45°, or the sign flipped to -90° — is 45° or 180° away, which
        /// this catches with room to spare.
        /// </remarks>
        private static void AssertRotation(GameObject prefab, Quaternion expected, string because)
        {
            var actual = FindRendererChild(prefab).localRotation;

            Assert.That(
                Quaternion.Angle(actual, expected),
                Is.LessThan(0.01f),
                because + ": expected " + expected.eulerAngles + " but found " + actual.eulerAngles);
        }
    }
}
