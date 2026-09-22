using System;
using System.Collections.Generic;
using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap.Editor
{
    /// <summary>
    /// Moves every decoration instance under the container its band names, so that a saved prefab or
    /// scene always has the same shape no matter how the instances were placed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This runs when the work is being written, not when each instance appears.</b> Rearranging on
    /// every drop means racing Unity's creation event, its deferred callbacks and its undo groups; the
    /// undo part of that race is what made one Ctrl+Z put an already-adopted instance back at the scene
    /// root, which reads as "the tool did not work". Writing time has none of those problems: a save is
    /// not an undo step, it sees the finished state, and running the same pass again changes nothing.
    /// </para>
    /// <para>
    /// <b>Where an instance goes depends on what is already there.</b> Inside a prefab, the containers
    /// belong to that prefab's root. In a scene, a container that a prefab instance already provides
    /// wins over the scene root -- the collection prefab is where the decorations are meant to live, and
    /// filing them at the scene root would leave the prefab empty and the scene cluttered.
    /// </para>
    /// <para>
    /// <b>An instance never crosses a prefab boundary, but it may enter one.</b> A decoration that sits
    /// <i>inside</i> somebody's prefab instance is only moved within that instance; moving it out would
    /// tear overrides off that prefab. A decoration that is free (a plain object, or a prefab instance
    /// root of its own) may be filed into a prefab instance, which is an ordinary "added child"
    /// override -- the same thing the editor does when you drag an object onto an instance.
    /// </para>
    /// <para>
    /// <b>Nothing is registered for undo.</b> This is a structural invariant, not a user action, and the
    /// file that gets written is the truth. The consequence is deliberate: Ctrl+Z does not take an
    /// instance back out of its container; dragging it out and saving puts it back.
    /// </para>
    /// </remarks>
    public static class DecorationNormalizer
    {
        /// <summary>
        /// Normalizes every decoration below <paramref name="prefabRoot"/> into a container that is a
        /// direct child of it. Returns how many instances were moved.
        /// </summary>
        /// <remarks>
        /// <b>The root itself is not a candidate.</b> A prefab whose own root carries a
        /// <see cref="DecorationView"/> <i>is</i> a decoration, not a collection of them; moving it into
        /// a container inside itself is meaningless. That is also what makes it safe to hand this
        /// method any prefab: feeding it a decoration prefab is a no-op instead of a corruption.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="prefabRoot"/> is null.</exception>
        public static int NormalizePrefab(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabRoot));
            }

            var views = new List<DecorationView>();
            var transform = prefabRoot.transform;
            for (var index = 0; index < transform.childCount; index++)
            {
                views.AddRange(transform.GetChild(index).GetComponentsInChildren<DecorationView>(true));
            }

            var destinations = new Dictionary<DecorationBand, Transform>
            {
                { DecorationBand.Decoration, prefabRoot.transform },
                { DecorationBand.Overlay, prefabRoot.transform }
            };
            // reorganizeInsidePrefabInstances: false. A decoration sitting inside a *nested* prefab
            // instance belongs to that nested asset, not to the prefab being edited; rearranging it
            // would mean writing overrides into somebody else's prefab.
            return Normalize(views, new[] { prefabRoot }, prefabRoot.scene, destinations, false);
        }

        /// <summary>
        /// Normalizes every decoration in <paramref name="scene"/> into the container its band names.
        /// Returns how many instances were moved.
        /// </summary>
        public static int NormalizeScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var views = CollectViews(roots);
            var destinations = new Dictionary<DecorationBand, Transform>();

            // reorganizeInsidePrefabInstances: true. In a scene, a decoration dropped onto a prefab
            // instance is a scene-side override, and filing it under that instance's container is
            // exactly what the collection-prefab workflow asks for.
            return Normalize(views, roots, scene, destinations, true);
        }

        /// <summary>
        /// The pass the entry points share: put every collected instance under the container its band
        /// names, skipping the ones whose destination would be illegal.
        /// </summary>
        /// <remarks>
        /// Public because it is the seam a test can drive without going anywhere near whatever scene
        /// happens to be open, and because the save hooks hand it an explicit root list.
        /// </remarks>
        /// <param name="containerParentOverride">
        /// Where to look for containers, for callers that know the host (a prefab root). When null the
        /// host is worked out per instance by <see cref="PreferredContainerParent"/>.
        /// </param>
        public static int NormalizeRoots(
            IList<GameObject> roots,
            Transform containerParentOverride,
            Scene scene)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var destinations = new Dictionary<DecorationBand, Transform>();
            if (containerParentOverride != null)
            {
                destinations[DecorationBand.Decoration] = containerParentOverride;
                destinations[DecorationBand.Overlay] = containerParentOverride;
            }

            return Normalize(CollectViews(roots), roots, scene, destinations, true);
        }

        /// <summary>
        /// Where an instance should be filed: its own prefab instance when it lives in one, otherwise a
        /// container that some prefab instance among <paramref name="roots"/> already provides, otherwise
        /// null -- which means the scene root.
        /// </summary>
        /// <remarks>
        /// Public, and it takes the roots explicitly rather than a scene, so the rule can be asserted
        /// without moving anything and without depending on whatever happens to be open: the test builds
        /// a prefab that carries containers, instantiates it, and asks where a loose decoration goes.
        /// </remarks>
        public static Transform PreferredContainerParent(
            IList<GameObject> roots,
            GameObject instance,
            DecorationBand band)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var owner = instance == null ? null : PrefabUtility.GetOutermostPrefabInstanceRoot(instance);

            // Inside somebody's prefab: that instance is the only legal host, so normalize within it.
            if (owner != null && owner != instance)
            {
                return owner.transform;
            }

            // Free agent: prefer a container an instance already provides over the scene root.
            Transform first = null;
            var found = 0;
            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                if (root == null)
                {
                    continue;
                }

                var instances = OutermostPrefabInstanceRoots(root);
                for (var instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    var host = instances[instanceIndex];
                    if (FindDirectChildContainer(host.transform, band) == null)
                    {
                        continue;
                    }

                    found++;
                    if (first == null)
                    {
                        first = host.transform;
                    }
                }
            }

            if (found > 1)
            {
                Debug.LogWarning(
                    "More than one prefab instance provides a '" +
                    DecorationContainerPolicy.ContainerName(band) +
                    "' container; using the first one. Remove the others to make the choice explicit.");
            }

            return first;
        }

        private static int Normalize(
            IList<DecorationView> views,
            IList<GameObject> roots,
            Scene scene,
            IDictionary<DecorationBand, Transform> destinations,
            bool reorganizeInsidePrefabInstances)
        {
            var moved = 0;

            for (var index = 0; index < views.Count; index++)
            {
                var view = views[index];
                if (view == null)
                {
                    continue;
                }

                if (!reorganizeInsidePrefabInstances)
                {
                    var owner = PrefabUtility.GetOutermostPrefabInstanceRoot(view.gameObject);
                    if (owner != null && owner != view.gameObject)
                    {
                        continue;
                    }
                }

                DecorationBand band;
                try
                {
                    band = DecorationBands.BandFor(view.Queue);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Left where it is, out loud: guessing a band would file the instance under the
                    // wrong container, and the saved file would look reasonable.
                    Debug.LogWarning(
                        "'" + view.name + "' has render queue " + view.Queue +
                        ", which belongs to no known band; it was left where it is.",
                        view);
                    continue;
                }

                var parent = DestinationFor(destinations, roots, view.gameObject, band);
                var container = DecorationContainerPolicy.Resolve(parent, scene, band);
                if (container == null || view.transform.parent == container)
                {
                    continue;
                }

                if (!CanMove(view.gameObject, container))
                {
                    continue;
                }

                // SetParent(_, true) keeps the world pose, which is what the editor does when an object
                // is dragged under a new parent.
                view.transform.SetParent(container, true);
                moved++;
            }

            return moved;
        }

        private static Transform DestinationFor(
            IDictionary<DecorationBand, Transform> destinations,
            IList<GameObject> roots,
            GameObject instance,
            DecorationBand band)
        {
            // An instance that lives inside somebody's prefab is normalized within that prefab, so its
            // destination is per instance and must not be cached across the pass.
            var owner = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            if (owner != null && owner != instance)
            {
                return owner.transform;
            }

            Transform cached;
            if (destinations.TryGetValue(band, out cached))
            {
                return cached;
            }

            var preferred = PreferredContainerParent(roots, instance, band);
            destinations[band] = preferred;
            return preferred;
        }

        /// <summary>
        /// Whether <paramref name="instance"/> may be filed under <paramref name="container"/>.
        /// </summary>
        /// <remarks>
        /// False only for one case: the instance lives inside a prefab instance and the container is
        /// somewhere else. Moving it would reach out of that prefab. Everything else is allowed --
        /// including filing a free instance <i>into</i> a prefab instance, which is how a decoration
        /// dropped in the scene joins the collection prefab.
        /// </remarks>
        private static bool CanMove(GameObject instance, Transform container)
        {
            var owner = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            if (owner == null || owner == instance)
            {
                return true;
            }

            var destinationOwner = PrefabUtility.GetOutermostPrefabInstanceRoot(container.gameObject);
            return destinationOwner == owner;
        }

        private static List<DecorationView> CollectViews(IList<GameObject> roots)
        {
            var views = new List<DecorationView>();
            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                if (root == null)
                {
                    continue;
                }

                views.AddRange(root.GetComponentsInChildren<DecorationView>(true));
            }

            return views;
        }

        /// <summary>
        /// The direct-child container of <paramref name="parent"/>, or null. Deliberately not a recursive
        /// search: a container is a sibling of the instances it holds, never a distant relative.
        /// </summary>
        private static Transform FindDirectChildContainer(Transform parent, DecorationBand band)
        {
            var name = DecorationContainerPolicy.ContainerName(band);
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name && DecorationContainerPolicy.IsContainer(child.gameObject, band))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Every outermost prefab instance root at or below <paramref name="root"/>, including
        /// <paramref name="root"/> itself.
        /// </summary>
        private static List<GameObject> OutermostPrefabInstanceRoots(GameObject root)
        {
            var instances = new List<GameObject>();
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(root))
            {
                instances.Add(root);
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                var candidate = transforms[index].gameObject;
                if (candidate != root && PrefabUtility.IsOutermostPrefabInstanceRoot(candidate))
                {
                    instances.Add(candidate);
                }
            }

            return instances;
        }
    }
}
