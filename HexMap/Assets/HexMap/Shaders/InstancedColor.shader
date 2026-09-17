Shader "HexMap/InstancedColor"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BorderWidth("Border Width", Range(0.001, 0.5)) = 0.05
        [Toggle] _GradientEnabled("Gradient Enabled", Float) = 0
        _GradientPower("Gradient Power", Range(0.1, 8)) = 1
        _GradientStartAlpha("Gradient Start Alpha", Range(0, 1)) = 0.5
        _InnerRadius("Inner Radius", Range(0, 0.8660254)) = 0.4275
        _InteriorAlpha("Interior Alpha", Range(0, 1)) = 0
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
                float2 normalizedPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float borderDistance : TEXCOORD0;
                float2 normalizedPosition : TEXCOORD1;
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
                float _InnerRadius;
                float _GradientStartAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.borderDistance = input.borderDistance.x;
                output.normalizedPosition = input.normalizedPosition;
                return output;
            }

            // UV2 is a unit flat-top hex, independent of the mesh layout.
            float SmoothInsetDistance(float2 position, float apothem)
            {
                float3 projections = float3(
                    dot(position, float2(0.8660254, 0.5)),
                    position.y,
                    dot(position, float2(-0.8660254, 0.5)));
                float3 positiveDistances = apothem - projections;
                float3 negativeDistances = apothem + projections;
                float3 pairMinimum = min(positiveDistances, negativeDistances);
                float minimumDistance = max(min(pairMinimum.x, min(pairMinimum.y, pairMinimum.z)), 0.0);

                // Equivalent to (sum(distance^-4))^-1/4. Rescaling keeps
                // ratios bounded and avoids overflow near the inset edges.
                float3 positiveRatios = minimumDistance / max(positiveDistances, 0.000001);
                float3 negativeRatios = minimumDistance / max(negativeDistances, 0.000001);
                positiveRatios *= positiveRatios;
                negativeRatios *= negativeRatios;
                float sumFourthPowers = dot(positiveRatios, positiveRatios)
                    + dot(negativeRatios, negativeRatios);
                return minimumDistance * rsqrt(sqrt(max(sumFourthPowers, 0.000001)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float borderWidth = clamp(UNITY_ACCESS_INSTANCED_PROP(Props, _BorderWidth), 0.001, 0.5);
                float gradientEnabled = UNITY_ACCESS_INSTANCED_PROP(Props, _GradientEnabled);
                float gradientPower = max(UNITY_ACCESS_INSTANCED_PROP(Props, _GradientPower), 0.1);
                float distanceFromOuterEdge = saturate(input.borderDistance);
                float centerRadius = length(input.normalizedPosition);
                float insetApothem = 0.8660254 * (1.0 - borderWidth);
                float innerCircleRadius = clamp(_InnerRadius, 0.0, insetApothem - 0.0001);
                float circleDistance = max(centerRadius - innerCircleRadius, 0.0);
                float hexDistance = SmoothInsetDistance(input.normalizedPosition, insetApothem);
                float gradientProgress = saturate(circleDistance / max(circleDistance + hexDistance, 0.000001));
                float outerBoundary = 1.0;
                float solidBorderBoundary = 1.0 - step(borderWidth, distanceFromOuterEdge);
                float solidBorderAlpha = solidBorderBoundary;
                float innerCircleBoundary = step(innerCircleRadius, centerRadius);
                float circularGradient = innerCircleBoundary
                    * pow(gradientProgress, gradientPower)
                    * saturate(_GradientStartAlpha)
                    * outerBoundary;
                // Keep the solid border at full opacity; only the interior
                // gradient is scaled.
                float gradientBorderAlpha = lerp(circularGradient, outerBoundary, solidBorderBoundary);
                float borderAlpha = lerp(solidBorderAlpha, gradientBorderAlpha, saturate(gradientEnabled));
                float alpha = baseColor.a * max(_InteriorAlpha * outerBoundary, borderAlpha);
                return half4(baseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
