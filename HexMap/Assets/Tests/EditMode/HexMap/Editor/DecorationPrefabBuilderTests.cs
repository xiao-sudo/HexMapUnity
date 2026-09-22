using HexMap.Core;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexMap.Editor.Tests
{
    /// <summary>
    /// Covers the two decisions the decoration prefab builder makes: which band the prefab belongs
    /// to, which plane it renders on, and where both of those are stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The plane is a rotation on the child, not a property of the mesh.</b> These tests exist
    /// because the alternative -- building the mesh in XZ for XZ decorations -- is both tempting and
    /// invisible: it would double the mesh cache, break "one Sprite, one mesh", and still look
    /// correct to whoever tried it, as long as they only ever opened one of the two prefabs.
    /// <c>DecorationGeometryTests.TheDecorationMeshLiesInTheLocalXyPlane</c> guards the other half
    /// of the same contract.
    /// </para>
    /// <para>
    /// <b>Band is written through <see cref="SerializedObject"/> using the field names as strings,
    /// so the assertions read the values back out of the saved prefab.</b> A renamed field would
    /// make the lookup return null; asserting on <see cref="DecorationView.Queue"/> and
    /// <see cref="DecorationView.SortingOrder"/> is what turns that into a failure rather than into
    /// a prefab that quietly belongs to the wrong band.
    /// </para>
    /// <para>
    /// <b>The fixtures here build Sprite-less prefabs on purpose.</b> The structure, the rotation and
    /// the two band numbers do not depend on a Sprite, and leaving it out keeps this fixture clear of
    /// the mesh factory, the material cache and <c>Shader.Find</c> -- so it asserts one thing and
    /// cannot fail for a reason that belongs to a different seam. What that leaves uncovered is the
    /// order of <c>ConfigureBand</c> against <c>Apply</c>; it has no shipped consequence, because the
    /// band lives in serialized fields and both the material and the renderer's sorting order are
    /// re-derived on every enable.
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
            var prefab = CreatePrefab(DecorationBand.Decoration, HexPlane.XY);

            AssertRotation(prefab, Quaternion.identity, "XY keeps the mesh's own plane");
        }

        [Test]
        public void XzTurnsTheRendererChildNinetyDegreesAboutX()
        {
            var prefab = CreatePrefab(DecorationBand.Decoration, HexPlane.XZ);

            // +90° about X maps the Sprite's local up onto world +Z, which is what the top-down
            // camera in map.unity has at the top of its frame. -90° would render the image upside
            // down, and nothing else in the pipeline would notice.
            AssertRotation(prefab, Quaternion.Euler(90f, 0f, 0f), "XZ lays the Sprite on the ground");
        }

        [TestCase(DecorationBand.Decoration, HexPlane.XY)]
        [TestCase(DecorationBand.Decoration, HexPlane.XZ)]
        [TestCase(DecorationBand.Overlay, HexPlane.XY)]
        [TestCase(DecorationBand.Overlay, HexPlane.XZ)]
        public void TheSavedPrefabCarriesItsBandsQueueAndSortingOrder(DecorationBand band, HexPlane plane)
        {
            var prefab = CreatePrefab(band, plane);
            var view = prefab.GetComponent<DecorationView>();

            Assert.That(view, Is.Not.Null, "the root is the authoring surface and carries the component");
            Assert.That(view.Queue, Is.EqualTo(DecorationBands.Queue(band)));
            Assert.That(view.SortingOrder, Is.EqualTo(DecorationBands.SortingOrder(band)));

            // The rotation is asserted in the same pass so the four combinations are each pinned as a
            // whole: a prefab is only correct if its band and its plane both came out right.
            AssertRotation(prefab, ExpectedRotation(plane), band + " on " + plane);
        }

        [TestCase(DecorationBand.Decoration, HexPlane.XY, "Tree_Decoration_XY")]
        [TestCase(DecorationBand.Decoration, HexPlane.XZ, "Tree_Decoration_XZ")]
        [TestCase(DecorationBand.Overlay, HexPlane.XY, "Tree_Overlay_XY")]
        [TestCase(DecorationBand.Overlay, HexPlane.XZ, "Tree_Overlay_XZ")]
        public void TheAssetNameCarriesTheBandAndThePlane(
            DecorationBand band,
            HexPlane plane,
            string expected)
        {
            // All four have to be distinct: one Sprite can legitimately become four prefabs.
            Assert.That(DecorationPrefabBuilder.BuildAssetName("Tree", band, plane), Is.EqualTo(expected));
        }

        [Test]
        public void BothPlanesDifferOnlyByTheRendererChildsRotation()
        {
            var xy = CreatePrefab(DecorationBand.Decoration, HexPlane.XY);
            var xz = CreatePrefab(DecorationBand.Decoration, HexPlane.XZ);

            AssertSameStructure(xy, xz);

            var xyChild = FindRendererChild(xy);
            var xzChild = FindRendererChild(xz);

            Assert.That(
                Quaternion.Angle(xyChild.localRotation, xzChild.localRotation),
                Is.EqualTo(90f).Within(0.01f),
                "the planes must be exactly a quarter turn apart");
        }

        [Test]
        public void BothBandsDifferOnlyByTheirNumbers()
        {
            var decoration = CreatePrefab(DecorationBand.Decoration, HexPlane.XZ);
            var overlay = CreatePrefab(DecorationBand.Overlay, HexPlane.XZ);

            AssertSameStructure(decoration, overlay);

            Assert.That(
                FindRendererChild(decoration).localRotation,
                Is.EqualTo(FindRendererChild(overlay).localRotation),
                "the band is numbers, not a rotation");

            var decorationView = decoration.GetComponent<DecorationView>();
            var overlayView = overlay.GetComponent<DecorationView>();

            Assert.That(
                overlayView.Queue,
                Is.Not.EqualTo(decorationView.Queue),
                "each band keeps its own material identity");
            Assert.That(
                overlayView.SortingOrder,
                Is.GreaterThan(decorationView.SortingOrder),
                "the overlay band draws after the decoration band");
        }

        /// <summary>
        /// States numerically which side of the HexMap baseline each band is on.
        /// </summary>
        /// <remarks>
        /// <c>DecorationViewEditModeTests.TheBandsAreOrderedAroundTheHexMapBaseline</c> makes this
        /// assertion about <c>DecorationQueue</c>'s constants; this one makes it about the values the
        /// builder actually writes. Together they mean a change to either side has to be deliberate:
        /// the constants can be right while the tooling writes the wrong one, and that failure is
        /// invisible until someone notices the map is covered.
        /// </remarks>
        [Test]
        public void TheTwoBandsStraddleTheHexMapBaseline()
        {
            Assert.That(
                DecorationBands.SortingOrder(DecorationBand.Decoration),
                Is.LessThan(DecorationQueue.HexMapSortingOrder),
                "decorations must draw before the HexMap, which needs a smaller sorting order");
            Assert.That(
                DecorationBands.SortingOrder(DecorationBand.Overlay),
                Is.GreaterThan(DecorationQueue.HexMapSortingOrder),
                "overlays must draw after the HexMap, which needs a larger sorting order");
        }

        [Test]
        public void TheRendererChildCarriesTheGeometryComponents()
        {
            var prefab = CreatePrefab(DecorationBand.Decoration, HexPlane.XZ);
            var child = FindRendererChild(prefab);

            Assert.That(child.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(child.GetComponent<MeshRenderer>(), Is.Not.Null);
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
        public void AnUndefinedBandIsRejectedRatherThanTreatedAsADecoration()
        {
            // A silent default here is the dangerous direction: a decoration that covers the map
            // looks like a rendering problem, not like a wrong argument.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationBands.Queue((DecorationBand)7); });
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationBands.SortingOrder((DecorationBand)7); });
        }

        private static Quaternion ExpectedRotation(HexPlane plane)
        {
            return plane == HexPlane.XZ ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        }

        private static GameObject CreatePrefab(DecorationBand band, HexPlane plane)
        {
            var assetPath = TestAssetFolder + "/Skeleton_" + band + "_" + plane + ".prefab";
            var prefab = DecorationPrefabBuilder.CreateAsset(assetPath, band, plane, null);

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
        /// Asserts everything two prefabs must share, so a test that claims "these differ only by X"
        /// cannot pass while something else also differs.
        /// </summary>
        /// <remarks>
        /// The root's name is deliberately not compared: <c>SaveAsPrefabAsset</c> names the saved root
        /// after the file, so two prefabs legitimately differ there.
        /// </remarks>
        private static void AssertSameStructure(GameObject expected, GameObject actual)
        {
            var expectedChild = FindRendererChild(expected);
            var actualChild = FindRendererChild(actual);

            Assert.That(actualChild.name, Is.EqualTo(expectedChild.name));
            Assert.That(actualChild.localPosition, Is.EqualTo(expectedChild.localPosition));
            Assert.That(actualChild.localScale, Is.EqualTo(expectedChild.localScale));
            Assert.That(
                actual.GetComponent<DecorationView>(),
                Is.Not.Null,
                "both prefabs carry the authoring component on the root");
            Assert.That(actualChild.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(actualChild.GetComponent<MeshRenderer>(), Is.Not.Null);
        }

        /// <summary>
        /// Asserts the child's rotation by angle rather than by component equality.
        /// </summary>
        /// <remarks>
        /// The prefab is written to disk and read back, so the quaternion survives a float round trip
        /// through YAML; comparing components exactly would fail on the last bit rather than on the
        /// rotation. A wrong rotation -- 45°, or the sign flipped to -90° -- is 45° or 180° away, which
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
