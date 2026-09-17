Shader "HexMap/InstancedColor"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BorderWidth("Border Width", Range(0.001, 0.5)) = 0.05
        [Toggle] _GradientEnabled("Gradient Enabled", Float) = 0
        _GradientPower("Gradient Power", Range(0.1, 8)) = 1
        _InteriorAlpha("Interior Alpha", Range(0, 1)) = 0
        [Toggle] _AntiAliasing("Anti-Aliasing", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 borderDistance : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float borderDistance : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _BorderWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _GradientEnabled)
                UNITY_DEFINE_INSTANCED_PROP(float, _GradientPower)
            UNITY_INSTANCING_BUFFER_END(Props)

            CBUFFER_START(UnityPerMaterial)
                float _InteriorAlpha;
                float _AntiAliasing;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.borderDistance = input.borderDistance.x;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float borderWidth = max(UNITY_ACCESS_INSTANCED_PROP(Props, _BorderWidth), 0.001);
                float gradientEnabled = UNITY_ACCESS_INSTANCED_PROP(Props, _GradientEnabled);
                float gradientPower = max(UNITY_ACCESS_INSTANCED_PROP(Props, _GradientPower), 0.1);
                float distanceFromOuterEdge = saturate(input.borderDistance);
                float edgeWidth = _AntiAliasing > 0.5
                    ? max(fwidth(distanceFromOuterEdge), 0.0001)
                    : 0.0;

                float outerBoundary = _AntiAliasing > 0.5
                    ? smoothstep(0.0, edgeWidth, distanceFromOuterEdge)
                    : 1.0;
                float innerBoundary = _AntiAliasing > 0.5
                    ? 1.0 - smoothstep(borderWidth - edgeWidth, borderWidth + edgeWidth, distanceFromOuterEdge)
                    : 1.0 - step(borderWidth, distanceFromOuterEdge);
                float borderMask = outerBoundary * innerBoundary;
                float gradient = pow(saturate(1.0 - distanceFromOuterEdge / borderWidth), gradientPower);
                float borderAlpha = lerp(1.0, gradient, saturate(gradientEnabled)) * borderMask;
                float alpha = baseColor.a * max(_InteriorAlpha * outerBoundary, borderAlpha);
                return half4(baseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}