using UnityEngine;

namespace HexMap.UnityRuntime
{
    /// <summary>
    /// Applies the renderer settings shared by decorations and overlays.
    /// </summary>
    /// <remarks>
    /// Everything here is deliberately switched off. Decorations are unlit billboards whose
    /// appearance comes entirely from their texture, so probes, shadows and motion vectors would
    /// only cost time. No per-object renderer data is written: the material carries the
    /// per-decoration data, so renderers stay SRP Batcher compatible and must not touch
    /// <see cref="MaterialPropertyBlock"/>.
    ///
    /// The allowed set is exactly the one the previous decoration implementation used, minus
    /// occlusion-culling control, which it never touched.
    /// </remarks>
    internal static class DecorationRenderSettings
    {
        public static void Apply(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
