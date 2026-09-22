using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HexMap.UnityRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HexMap.Editor.Tests
{
    /// <summary>
    /// Covers the two decisions the container policy makes: which band a queue belongs to, and where a
    /// band's container is found or created under a given parent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The band's name is asserted as a literal on purpose.</b> The container name is written into
    /// scene and prefab assets, so renaming a <see cref="DecorationBand"/> member breaks every asset
    /// that already has a container -- and a rename is not a compile error anywhere. These two strings
    /// are the only thing that turns that rename into a failure.
    /// </para>
    /// <para>
    /// <b>"Is this a container" is asserted from both sides.</b> A same-named decoration must be
    /// refused (that is the case that would silently nest decorations), and a container carrying some
    /// unrelated component must still be accepted (that is the case someone would "tighten" the rule
    /// into breaking).
    /// </para>
    /// <para>
    /// <b>Every parent is a test-owned object, so nothing here depends on what scene is open.</b> The
    /// one exception is the null-parent case, which is asserted as a contract: the open scene may
    /// legitimately already hold that container, which is the reuse path rather than the create path.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DecorationContainerPolicyTests
    {
        private readonly List<GameObject> m_Created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Reverse order so children go before their parents; DestroyImmediate on a parent would
            // otherwise leave the tracked child entries pointing at destroyed objects.
            for (var index = m_Created.Count - 1; index >= 0; index--)
            {
                if (m_Created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Created[index]);
                }
            }

            m_Created.Clear();
        }

        [Test]
        public void BandForMapsEachBandsQueue()
        {
            Assert.That(
                DecorationBands.BandFor(DecorationQueue.Decoration),
                Is.EqualTo(DecorationBand.Decoration));
            Assert.That(
                DecorationBands.BandFor(DecorationQueue.Overlay),
                Is.EqualTo(DecorationBand.Overlay));
        }

        [Test]
        public void BandForRejectsAnUnknownQueue()
        {
            // A silent default here would put a decoration in the overlay container, whose only
            // symptom is that the asset is organised wrongly -- nothing renders differently.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationBands.BandFor(DecorationQueue.Decoration + 1); });
        }

        [Test]
        public void BandForIsTheInverseOfQueueForEveryDeclaredBand()
        {
            // Two switches that each know half of the band table are what the inverse assertion is
            // for: adding a band to one of them only is otherwise invisible.
            foreach (DecorationBand band in Enum.GetValues(typeof(DecorationBand)))
            {
                Assert.That(
                    DecorationBands.BandFor(DecorationBands.Queue(band)),
                    Is.EqualTo(band),
                    "queue and band must round-trip for " + band);
            }
        }

        [Test]
        public void TheContainerNameIsTheBandName()
        {
            Assert.That(
                DecorationContainerPolicy.ContainerName(DecorationBand.Decoration),
                Is.EqualTo("Decoration"),
                "the asset-side name of the decoration container is part of the contract");
            Assert.That(
                DecorationContainerPolicy.ContainerName(DecorationBand.Overlay),
                Is.EqualTo("Overlay"),
                "the asset-side name of the overlay container is part of the contract");
        }

        [Test]
        public void AnUndefinedBandHasNoContainerName()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationContainerPolicy.ContainerName((DecorationBand)7); });
        }

        [Test]
        public void IsContainerAcceptsAnEmptyObjectNamedAfterTheBand()
        {
            var candidate = Create("Decoration");

            Assert.That(
                DecorationContainerPolicy.IsContainer(candidate, DecorationBand.Decoration),
                Is.True);
        }

        [Test]
        public void IsContainerRejectsAnotherBandsName()
        {
            var candidate = Create("Overlay");

            Assert.That(
                DecorationContainerPolicy.IsContainer(candidate, DecorationBand.Decoration),
                Is.False);
        }

        [Test]
        public void IsContainerRejectsADecorationThatSharesTheName()
        {
            // The real case: a decoration prefab called "Decoration" produces instances called
            // "Decoration". With a name-only rule this object becomes the container and the next
            // instance is filed under another decoration.
            var candidate = CreateDecoration("Decoration");

            Assert.That(
                DecorationContainerPolicy.IsContainer(candidate, DecorationBand.Decoration),
                Is.False);
        }

        [Test]
        public void IsContainerIgnoresComponentsThatAreNotDecorations()
        {
            // Pins the deliberate looseness: the rule is "named after the band and not a decoration",
            // not "carries nothing but a Transform".
            var candidate = Create("Decoration");
            candidate.AddComponent<BoxCollider>();

            Assert.That(
                DecorationContainerPolicy.IsContainer(candidate, DecorationBand.Decoration),
                Is.True);
        }

        [Test]
        public void ResolveReusesAnExistingContainerUnderTheParent()
        {
            var parent = Create("Parent");
            var container = CreateUnder(parent, "Decoration");
            var before = parent.transform.childCount;

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(resolved, Is.SameAs(container.transform));
            Assert.That(
                parent.transform.childCount,
                Is.EqualTo(before),
                "reusing a container must not add a second one");
        }

        [Test]
        public void ResolveCreatesTheContainerUnderTheGivenParent()
        {
            var parent = Create("Parent");

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(resolved.name, Is.EqualTo("Decoration"));
            Assert.That(resolved.parent, Is.SameAs(parent.transform));
            Assert.That(resolved.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(resolved.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(resolved.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void ResolveIgnoresASameNamedSiblingThatOnlyStartsWithTheName()
        {
            var parent = Create("Parent");
            CreateUnder(parent, "Decoration 1");

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(
                resolved.name,
                Is.EqualTo("Decoration"),
                "a prefix match would adopt 'Decoration 1' as the container");
            Assert.That(resolved.parent, Is.SameAs(parent.transform));
        }

        [Test]
        public void ResolveDoesNotSearchDeeperThanDirectChildren()
        {
            var parent = Create("Parent");
            var group = CreateUnder(parent, "Group");
            var nested = CreateUnder(group, "Decoration");

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(resolved.parent, Is.SameAs(parent.transform));
            Assert.That(
                resolved,
                Is.Not.SameAs(nested.transform),
                "a container one level deeper is not this parent's container");
        }

        [Test]
        public void ResolvePassesOverASameNamedDecorationAndSaysSo()
        {
            var parent = Create("Parent");
            var decoration = CreateDecoration("Decoration");
            decoration.transform.SetParent(parent.transform, false);

            LogAssert.Expect(
                LogType.Warning,
                new Regex("named 'Decoration' were passed over because they are decorations"));

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(resolved, Is.Not.SameAs(decoration.transform));
            Assert.That(resolved.parent, Is.SameAs(parent.transform));
        }

        [Test]
        public void ResolveUsesTheFirstOfTwoUsableContainersAndSaysSo()
        {
            var parent = Create("Parent");
            var first = CreateUnder(parent, "Decoration");
            CreateUnder(parent, "Decoration");

            LogAssert.Expect(
                LogType.Warning,
                new Regex("More than one GameObject named 'Decoration' can hold this band"));

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(resolved, Is.SameAs(first.transform));
        }

        [Test]
        public void ResolveUsesTheOnlyUsableOneOfTwoSameNamedObjects()
        {
            var parent = Create("Parent");
            var decoration = CreateDecoration("Decoration");
            decoration.transform.SetParent(parent.transform, false);
            var container = CreateUnder(parent, "Decoration");

            var resolved = DecorationContainerPolicy.Resolve(
                parent.transform,
                parent.scene,
                DecorationBand.Decoration);

            Assert.That(
                resolved,
                Is.SameAs(container.transform),
                "one usable candidate is not an ambiguity, so nothing is warned about either");
        }

        [Test]
        public void ResolveCreatesAtTheSceneRootWhenTheParentIsNull()
        {
            var instance = Create("_Decoration_XZ");
            var existedBefore = FindRootContainer(instance.scene, DecorationBand.Decoration);

            var resolved = DecorationContainerPolicy.Resolve(
                null,
                instance.scene,
                DecorationBand.Decoration);

            if (existedBefore == null)
            {
                // This test created it at the root of whatever scene is open, so it owns it: leaving it
                // behind would litter the user's scene with an empty container.
                m_Created.Add(resolved.gameObject);
            }

            Assert.That(resolved.name, Is.EqualTo("Decoration"));
            Assert.That(resolved.parent, Is.Null);
            Assert.That(
                resolved.gameObject.scene,
                Is.EqualTo(instance.scene),
                "the container must land in the scene it was asked for");
            Assert.That(
                DecorationContainerPolicy.Resolve(null, instance.scene, DecorationBand.Decoration),
                Is.SameAs(resolved),
                "resolving twice must not produce a second container");
        }

        /// <summary>The scene-root container for a band, or null when the open scene has none.</summary>
        private static GameObject FindRootContainer(Scene scene, DecorationBand band)
        {
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (DecorationContainerPolicy.IsContainer(roots[index], band))
                {
                    return roots[index];
                }
            }

            return null;
        }

        [Test]
        public void ResolveRejectsAnUndefinedBandBeforeCreatingAnything()
        {
            var parent = Create("Parent");
            var childrenBefore = parent.transform.childCount;

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => { DecorationContainerPolicy.Resolve(parent.transform, parent.scene, (DecorationBand)7); });

            Assert.That(
                parent.transform.childCount,
                Is.EqualTo(childrenBefore),
                "a rejected band must not leave a half-built container behind");
        }

        /// <summary>Creates a tracked object at the scene root; the fixture destroys it afterwards.</summary>
        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            m_Created.Add(created);
            return created;
        }

        private GameObject CreateUnder(GameObject parent, string name)
        {
            var created = Create(name);
            created.transform.SetParent(parent.transform, false);
            return created;
        }

        /// <summary>
        /// Creates a tracked decoration in the shape the prefab builder produces: a root carrying
        /// <see cref="DecorationView"/> with a child carrying the geometry components. The child comes
        /// first because adding the component runs <c>Apply</c> through <c>[ExecuteAlways]</c>, and a
        /// decoration with no renderer below it reports an error.
        /// </summary>
        private GameObject CreateDecoration(string name)
        {
            var root = Create(name);
            var rendererObject = CreateUnder(root, "Renderer");
            rendererObject.AddComponent<MeshFilter>();
            rendererObject.AddComponent<MeshRenderer>();
            root.AddComponent<DecorationView>();
            return root;
        }
    }
}
