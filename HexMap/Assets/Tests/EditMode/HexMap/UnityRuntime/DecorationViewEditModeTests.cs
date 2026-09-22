using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexMap.UnityRuntime.Tests
{
    /// <summary>
    /// Covers what a <see cref="DecorationView"/> assembles when it is enabled, without entering
    /// play mode.
    /// </summary>
    /// <remarks>
    /// This is the only test that exercises the edit-time path: <c>[ExecuteAlways]</c> →
    /// <c>OnEnable</c> → <c>Apply</c>. The geometry tests build meshes directly and the play-mode
    /// tests add components at runtime, so neither would notice if that path stopped working —
    /// and "a decoration is visible in the scene view before you press play" is the whole point of
    /// authoring decorations as prefabs.
    /// </remarks>
    [TestFixture]
    public sealed class DecorationViewEditModeTests
    {
        private readonly List<Object> m_Created = new List<Object>();
        private GameObject m_Root;

        [SetUp]
        public void SetUp()
        {
            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null)
            {
                Object.DestroyImmediate(m_Root);
                m_Root = null;
            }

            for (var index = 0; index < m_Created.Count; index++)
            {
                if (m_Created[index] != null)
                {
                    Object.DestroyImmediate(m_Created[index]);
                }
            }

            m_Created.Clear();

            DecorationMeshFactory.Clear();
            DecorationMaterialCache.Clear();
        }

        [Test]
        public void EnablingTheComponentAssemblesTheRendererWithoutEnteringPlayMode()
        {
            Assert.That(Application.isPlaying, Is.False, "this fixture must run in edit mode");

            var sprite = CreateSprite();
            var view = CreateDisabledViewWithSprite(sprite);

            Assert.That(view.IsReady, Is.False, "nothing is assembled until the component is enabled");

            view.enabled = true;

            var filter = view.GetComponentInChildren<MeshFilter>(true);
            var renderer = view.GetComponentInChildren<MeshRenderer>(true);

            Assert.That(filter, Is.Not.Null, "the prefab shape is a child holding the MeshFilter");
            Assert.That(renderer, Is.Not.Null, "the prefab shape is a child holding the MeshRenderer");
            Assert.That(filter.sharedMesh, Is.Not.Null, "enabling must build and assign the mesh");
            Assert.That(
                renderer.sharedMaterial,
                Is.Not.Null,
                "enabling must derive and assign the material");
            Assert.That(view.IsReady, Is.True);
        }

        [Test]
        public void TheAssembledRendererIsConfiguredForTheDecorationLayer()
        {
            var sprite = CreateSprite();
            var view = CreateDisabledViewWithSprite(sprite);

            view.enabled = true;

            var filter = view.GetComponentInChildren<MeshFilter>(true);
            var renderer = filter.GetComponent<MeshRenderer>();

            Assert.That(renderer.enabled, Is.True, "a visible decoration must draw");
            Assert.That(renderer.sortingOrder, Is.EqualTo(0));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows, Is.False);
            Assert.That(renderer.lightProbeUsage, Is.EqualTo(UnityEngine.Rendering.LightProbeUsage.Off));
            Assert.That(
                renderer.reflectionProbeUsage,
                Is.EqualTo(UnityEngine.Rendering.ReflectionProbeUsage.Off));
            Assert.That(
                renderer.sharedMaterial.renderQueue,
                Is.EqualTo(DecorationQueue.Decoration),
                "a decoration draws before the HexMap");
        }

        [Test]
        public void AnInvisibleDecorationDoesNotDrawButIsStillAssembled()
        {
            var sprite = CreateSprite();
            var view = CreateDisabledViewWithSprite(sprite);
            SetField(view, "m_Visible", false);

            view.enabled = true;

            var filter = view.GetComponentInChildren<MeshFilter>(true);
            var renderer = filter.GetComponent<MeshRenderer>();

            Assert.That(renderer.enabled, Is.False, "an initially hidden decoration must not draw");
            Assert.That(
                filter.sharedMesh,
                Is.Not.Null,
                "hiding must not skip assembly, or showing it later would have to rebuild");
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
        }

        /// <summary>
        /// Builds the prefab shape — a root carrying the component, a child carrying the mesh — and
        /// writes the serialized fields the way the prefab asset would, so that enabling the
        /// component is what triggers assembly.
        /// </summary>
        private DecorationView CreateDisabledViewWithSprite(Sprite sprite)
        {
            m_Root = new GameObject("Decoration");
            var child = new GameObject("Renderer");
            child.transform.SetParent(m_Root.transform, false);
            child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();

            var view = m_Root.AddComponent<DecorationView>();
            view.enabled = false;
            SetField(view, "m_Sprite", sprite);

            Assert.That(view.IsReady, Is.False, "a disabled component must not have assembled anything");
            return view;
        }

        /// <summary>
        /// Writes a serialized field directly. The component exposes no setter for the queue or for
        /// visibility-before-enable on purpose, because those are prefab settings rather than
        /// runtime switches; this is the test standing in for the prefab asset.
        /// </summary>
        private static void SetField(DecorationView view, string name, object value)
        {
            var field = typeof(DecorationView).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "DecorationView must keep a serialized field named " + name);
            field.SetValue(view, value);
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white, Color.white
            });
            texture.Apply();
            m_Created.Add(texture);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                4f,
                0,
                SpriteMeshType.FullRect);
            m_Created.Add(sprite);
            return sprite;
        }
    }
}
