using System.Collections.Generic;
using System.Text.RegularExpressions;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexMap.Editor.Tests
{
    /// <summary>
    /// Covers the save-time pass: every decoration under a root ends up in the container its band
    /// names, and running the pass again changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every case normalizes a test-owned root</b> (<see cref="DecorationNormalizer.NormalizePrefab"/>
    /// or the <see cref="DecorationNormalizer.NormalizeRoots"/> seam), never the whole open scene, so a
    /// test cannot rearrange whatever the user happens to be editing.
    /// </para>
    /// <para>
    /// <b>The band is written through <see cref="SerializedObject"/> using the field name as a string,
    /// the same way <c>DecorationPrefabBuilder.ConfigureBand</c> does it</b>, because
    /// <see cref="DecorationView.Queue"/> is deliberately read-only.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DecorationNormalizerTests
    {
        private const string TestAssetParent = "Assets/Tests/EditMode/HexMap/Editor";
        private const string TestAssetFolder = TestAssetParent + "/NormalizerTestAssets";

        private readonly List<GameObject> m_Created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.CreateFolder(TestAssetParent, "NormalizerTestAssets");
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = m_Created.Count - 1; index >= 0; index--)
            {
                if (m_Created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Created[index]);
                }
            }

            m_Created.Clear();

            if (AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.DeleteAsset(TestAssetFolder);
            }
        }

        [Test]
        public void MovesADecorationIntoTheContainerNamedAfterItsBand()
        {
            var root = Create("Root");
            var decoration = CreateDecoration(root, "_Decoration_XZ", DecorationQueue.Decoration);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(decoration.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(decoration.transform.parent.parent, Is.SameAs(root.transform));
        }

        [Test]
        public void MovesAnOverlayIntoTheOverlayContainer()
        {
            var root = Create("Root");
            var overlay = CreateDecoration(root, "_Overlay_XZ", DecorationQueue.Overlay);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(overlay.transform.parent.name, Is.EqualTo("Overlay"));
        }

        [Test]
        public void ReusesTheContainerThatIsAlreadyThere()
        {
            var root = Create("Root");
            var container = Create("Decoration");
            container.transform.SetParent(root.transform, false);
            var decoration = CreateDecoration(root, "_Decoration_XZ", DecorationQueue.Decoration);

            DecorationNormalizer.NormalizePrefab(root);

            Assert.That(decoration.transform.parent, Is.SameAs(container.transform));
            Assert.That(
                CountContainers(root, DecorationBand.Decoration),
                Is.EqualTo(1),
                "reusing a container must not add a second one");
            Assert.That(container.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void RunningItTwiceChangesNothing()
        {
            var root = Create("Root");
            CreateDecoration(root, "_Decoration_XZ", DecorationQueue.Decoration);
            CreateDecoration(root, "_Overlay_XZ", DecorationQueue.Overlay);

            var first = DecorationNormalizer.NormalizePrefab(root);
            var second = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(first, Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(0), "the pass has to be idempotent; it runs on every save");
        }

        [Test]
        public void CorrectsAnInstanceSittingUnderTheWrongContainer()
        {
            var root = Create("Root");
            var decorationContainer = Create("Decoration");
            decorationContainer.transform.SetParent(root.transform, false);
            var overlayContainer = Create("Overlay");
            overlayContainer.transform.SetParent(root.transform, false);

            // An overlay-band instance filed under Decoration: the shape is wrong, nothing looks wrong.
            var overlay = CreateDecoration(decorationContainer, "_Overlay_XZ", DecorationQueue.Overlay);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(overlay.transform.parent, Is.SameAs(overlayContainer.transform));
        }

        [Test]
        public void MovesANestedInstanceUpToTheRootsContainer()
        {
            var root = Create("Root");
            var group = Create("Group");
            group.transform.SetParent(root.transform, false);
            var decoration = CreateDecoration(group, "_Decoration_XZ", DecorationQueue.Decoration);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(decoration.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(decoration.transform.parent.parent, Is.SameAs(root.transform));
        }

        [Test]
        public void KeepsTheWorldPositionOfWhatItMoves()
        {
            var root = Create("Root");
            var decoration = CreateDecoration(root, "_Decoration_XZ", DecorationQueue.Decoration);
            var position = new Vector3(3f, 1.5f, -2f);
            decoration.transform.position = position;

            DecorationNormalizer.NormalizePrefab(root);

            Assert.That(decoration.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void LeavesAnInstanceWhoseQueueBelongsToNoBandWhereItIs()
        {
            var root = Create("Root");
            var decoration = CreateDecoration(root, "_Unknown", 1234);

            LogAssert.Expect(
                LogType.Warning,
                new Regex("belongs to no known band"));

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(0));
            Assert.That(decoration.transform.parent, Is.SameAs(root.transform));
        }

        [Test]
        public void LeavesADecorationThatSitsInsideAnotherPrefabInstanceAlone()
        {
            // The decoration must be a *child* of the nested prefab's root, not that root itself: a
            // decoration that is a nested instance root is a free agent and does get filed. Moving this
            // one would write an override into the nested prefab, which a save-time pass may not do.
            var nestedRoot = Create("NestedRoot");
            CreateDecoration(nestedRoot, "Inner", DecorationQueue.Decoration);

            var prefabPath = TestAssetFolder + "/NestedDecoration.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(nestedRoot, prefabPath);
            Assert.That(prefab, Is.Not.Null, "the fixture needs a real prefab asset for this case");

            var root = Create("Root");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            var nested = instance.GetComponentInChildren<DecorationView>(true);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(0));
            Assert.That(nested.transform.parent, Is.SameAs(instance.transform));
        }

        [Test]
        public void NormalizesEveryInstanceInOnePass()
        {
            var root = Create("Root");
            var first = CreateDecoration(root, "_Decoration_XZ", DecorationQueue.Decoration);
            var second = CreateDecoration(root, "_Overlay_XZ", DecorationQueue.Overlay);
            var third = CreateDecoration(root, "_Decoration_XZ (1)", DecorationQueue.Decoration);

            var moved = DecorationNormalizer.NormalizePrefab(root);

            Assert.That(moved, Is.EqualTo(3));
            Assert.That(first.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(second.transform.parent.name, Is.EqualTo("Overlay"));
            Assert.That(third.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(first.transform.parent, Is.SameAs(third.transform.parent));
        }

        [Test]
        public void NormalizePrefabRejectsANullRoot()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => { DecorationNormalizer.NormalizePrefab(null); });
        }

        [Test]
        public void FilesALooseDecorationIntoAContainerThatAPrefabInstanceProvides()
        {
            // The collection-prefab workflow: the prefab instance in the scene owns the containers, so a
            // decoration dropped in the scene belongs inside it, not at the scene root.
            var instance = InstantiateCollection();
            var loose = CreateDecoration(null, "_Decoration_XZ", DecorationQueue.Decoration);
            var roots = new List<GameObject> { instance, loose };

            var moved = DecorationNormalizer.NormalizeRoots(roots, null, loose.scene);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(loose.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(loose.transform.parent.parent, Is.SameAs(instance.transform));
        }

        [Test]
        public void MovesADecorationDroppedOntoAPrefabInstanceIntoThatInstancesContainer()
        {
            // Dropped onto the instance in the Hierarchy: it is already inside the prefab's boundary, so
            // it is normalized within that instance rather than pulled out to the scene root.
            var instance = InstantiateCollection();
            var leaf = CreateDecoration(instance, "_Decoration_XZ", DecorationQueue.Decoration);

            var moved = DecorationNormalizer.NormalizeRoots(
                new List<GameObject> { instance },
                null,
                leaf.scene);

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(leaf.transform.parent.name, Is.EqualTo("Decoration"));
            Assert.That(leaf.transform.parent.parent, Is.SameAs(instance.transform));
        }

        [Test]
        public void APrefabInstanceThatProvidesContainersIsPreferredOverTheSceneRoot()
        {
            var instance = InstantiateCollection();
            var loose = CreateDecoration(null, "_Decoration_XZ", DecorationQueue.Decoration);

            var parent = DecorationNormalizer.PreferredContainerParent(
                new List<GameObject> { instance, loose },
                loose,
                DecorationBand.Decoration);

            Assert.That(parent, Is.SameAs(instance.transform));
        }

        /// <summary>
        /// Instantiates a collection prefab (a root carrying <c>Decoration</c> and <c>Overlay</c>
        /// children) at the scene root, tracked for teardown.
        /// </summary>
        private GameObject InstantiateCollection()
        {
            var contents = Create("Collection");
            var decoration = Create("Decoration");
            decoration.transform.SetParent(contents.transform, false);
            var overlay = Create("Overlay");
            overlay.transform.SetParent(contents.transform, false);

            var assetPath = TestAssetFolder + "/Collection.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
            Assert.That(prefab, Is.Not.Null, "the fixture needs a real collection prefab");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            m_Created.Add(instance);
            return instance;
        }

        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            m_Created.Add(created);
            return created;
        }

        /// <summary>How many direct children of <paramref name="parent"/> are containers for a band.</summary>
        private static int CountContainers(GameObject parent, DecorationBand band)
        {
            var count = 0;
            for (var index = 0; index < parent.transform.childCount; index++)
            {
                if (DecorationContainerPolicy.IsContainer(parent.transform.GetChild(index).gameObject, band))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Creates a decoration in the shape the prefab builder produces and writes its band's queue,
        /// because <see cref="DecorationView.Queue"/> is read-only by design.
        /// </summary>
        private GameObject CreateDecoration(GameObject parent, string name, int queue)
        {
            var root = Create(name);
            if (parent != null)
            {
                root.transform.SetParent(parent.transform, false);
            }

            var rendererObject = Create("Renderer");
            rendererObject.transform.SetParent(root.transform, false);
            rendererObject.AddComponent<MeshFilter>();
            rendererObject.AddComponent<MeshRenderer>();

            var view = root.AddComponent<DecorationView>();
            var serialized = new SerializedObject(view);
            var property = serialized.FindProperty("m_Queue");
            Assert.That(
                property,
                Is.Not.Null,
                "DecorationView must keep a serialized field named 'm_Queue'; the fixtures write the band through it");
            property.intValue = queue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
