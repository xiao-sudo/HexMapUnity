using System;
using HexMap.UnityRuntime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap.Editor
{
    /// <summary>
    /// Decides which GameObject decoration instances belong under, and finds or creates it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The container is chosen by band, and the band's name is the only token.</b> The name comes
    /// from <see cref="DecorationBand"/> through <see cref="ContainerName"/>, exactly as
    /// <see cref="DecorationPrefabBuilder.BuildAssetName"/> puts the same enum member into a file name.
    /// A second table of strings here would let the container and the prefab band drift apart.
    /// </para>
    /// <para>
    /// <b>"Is this a container" is one predicate, used for finding and for deciding whether an
    /// instance is already placed.</b> A candidate must carry the band's name and must not be a
    /// decoration itself (<see cref="IsContainer"/>). The second half is not decoration: instance
    /// names come from the prefab asset's name, and a decoration prefab called <c>Decoration</c> has
    /// really existed in this project -- with only a name test, a decoration would have been filed
    /// under another decoration.
    /// </para>
    /// <para>
    /// <b>The search stops at direct children of the given parent.</b> Searching deeper would adopt an
    /// instance into a container that lives somewhere else in the hierarchy, which is not a rule
    /// anyone can predict. The cost is that a container nested one level down is not found and a new
    /// one is created beside it; that is visible and easy to fix.
    /// </para>
    /// <para>
    /// Nothing here walks a hierarchy, moves an instance, or knows when it is called. Callers decide
    /// the parent: <see cref="DecorationNormalizer"/> passes the root being normalized (a prefab root,
    /// or <c>null</c> for scene root). This is the half a test can drive.
    /// </para>
    /// </remarks>
    public static class DecorationContainerPolicy
    {
        /// <summary>
        /// The name of the GameObject that holds <paramref name="band"/>'s instances.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="band"/> is not one of the declared <see cref="DecorationBand"/> values.
        /// </exception>
        public static string ContainerName(DecorationBand band)
        {
            if (!Enum.IsDefined(typeof(DecorationBand), band))
            {
                throw new ArgumentOutOfRangeException(nameof(band), band, null);
            }

            return band.ToString();
        }

        /// <summary>
        /// Whether <paramref name="candidate"/> can serve as <paramref name="band"/>'s container:
        /// it carries the band's name and is not itself a decoration.
        /// </summary>
        /// <remarks>
        /// Other components are deliberately ignored. Requiring "nothing but a Transform" would break
        /// the moment anyone gave the container a component of its own, and it would buy protection
        /// against nothing that the name plus this one check does not already cover.
        /// </remarks>
        public static bool IsContainer(GameObject candidate, DecorationBand band)
        {
            if (candidate == null)
            {
                return false;
            }

            return candidate.name == ContainerName(band) &&
                   candidate.GetComponent<DecorationView>() == null;
        }

        /// <summary>
        /// The container <paramref name="band"/>'s instances belong under: the first usable same-named
        /// direct child of <paramref name="parent"/>, or of the scene root when
        /// <paramref name="parent"/> is null, or a newly created one when there is none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="band"/> is not one of the declared <see cref="DecorationBand"/> values.
        /// </exception>
        public static Transform Resolve(Transform parent, Scene scene, DecorationBand band)
        {
            // Validated before anything is created, so a bad band cannot leave a stray object behind.
            var containerName = ContainerName(band);

            var existing = FindUsable(parent, scene, band, containerName);
            if (existing != null)
            {
                return existing;
            }

            return Create(parent, scene, containerName);
        }

        /// <summary>
        /// The first usable container among the direct children of <paramref name="parent"/>, or among
        /// the scene roots when <paramref name="parent"/> is null. Warns when the answer is ambiguous
        /// or when same-named objects had to be passed over.
        /// </summary>
        private static Transform FindUsable(
            Transform parent,
            Scene scene,
            DecorationBand band,
            string containerName)
        {
            var usable = 0;
            var passedOver = 0;
            Transform first = null;

            if (parent != null)
            {
                for (var index = 0; index < parent.childCount; index++)
                {
                    var child = parent.GetChild(index);
                    if (child.name != containerName)
                    {
                        continue;
                    }

                    if (IsContainer(child.gameObject, band))
                    {
                        usable++;
                        if (first == null)
                        {
                            first = child;
                        }
                    }
                    else
                    {
                        passedOver++;
                    }
                }
            }
            else
            {
                var roots = scene.GetRootGameObjects();
                for (var index = 0; index < roots.Length; index++)
                {
                    var root = roots[index];
                    if (root.name != containerName)
                    {
                        continue;
                    }

                    if (IsContainer(root, band))
                    {
                        usable++;
                        if (first == null)
                        {
                            first = root.transform;
                        }
                    }
                    else
                    {
                        passedOver++;
                    }
                }
            }

            if (usable > 1)
            {
                Debug.LogWarning(
                    "More than one GameObject named '" + containerName + "' can hold this band; " +
                    "using the first one. Rename or remove the others to make the choice explicit.");
            }
            else if (usable == 0 && passedOver > 0)
            {
                // Always said out loud: silently creating a second container next to a same-named
                // object is exactly the failure this rule exists to prevent.
                Debug.LogWarning(
                    passedOver + " GameObject(s) named '" + containerName + "' were passed over " +
                    "because they are decorations themselves; a new container was created next to " +
                    "them.");
            }

            return first;
        }

        /// <summary>
        /// Creates the container under <paramref name="parent"/>, or at the root of
        /// <paramref name="scene"/> when there is no parent.
        /// </summary>
        /// <remarks>
        /// Placement is the identity transform, which is what keeps a later reparent from having to
        /// undo any offset. The object is registered for undo because the tooling that creates it runs
        /// in response to an edit; callers that must not leave an undo entry (the save-time
        /// normalizer) pass through here too and accept the entry, since a container only appears once.
        /// </remarks>
        private static Transform Create(Transform parent, Scene scene, string containerName)
        {
            var container = new GameObject(containerName);
            Undo.RegisterCreatedObjectUndo(container, "Create " + containerName + " Container");

            if (parent != null)
            {
                container.transform.SetParent(parent, false);
            }
            else if (scene.IsValid() && container.scene != scene)
            {
                // A new GameObject lands in the active scene, which is not necessarily the scene the
                // caller is working on.
                SceneManager.MoveGameObjectToScene(container, scene);
            }

            return container.transform;
        }
    }
}
