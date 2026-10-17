Shader "Custom/SovietToonLit"
{
    Properties
    {
        [Header(Base)]
        _BaseColor("Base Color", Color) = (0.8, 0.8, 0.8, 1.0)
        _BaseMap("Base Map (Optional)", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode (0=Off, 1=Front, 2=Back)", Float) = 0

        [Header(Cel Shading)]
        _ShadowColor("Shadow Tint", Color) = (0.40, 0.48, 0.62, 1.0) // Stylish cool graphic novel shadow
        _ShadowThreshold("Shadow Step Threshold", Range(0.0, 1.0)) = 0.45
        _ShadowSmoothness("Step Softness", Range(0.001, 0.1)) = 0.02
        _AmbientStrength("Ambient Baseline", Range(0.0, 1.0)) = 0.38

        [Header(Outlines)]
        [Toggle(_OUTLINE_ON)] _OutlineOn("Enable Outline", Float) = 1.0
        _OutlineColor("Outline Color (Black Ink)", Color) = (0.04, 0.04, 0.06, 1.0)
        _OutlineWidth("Outline Thickness", Range(0.0, 10.0)) = 2.8
        _CornerExpand("Hard Corner Expand", Range(0.0, 1.0)) = 0.65

        [Header(Emission)]
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        // =========================================================================
        // PASS 1: FORWARD CEL SHADING PASS
        // =========================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 shadowCoord  : TEXCOORD3;
                float fogFactor     : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _OutlineColor;
                float4 _EmissionColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _AmbientStrength;
                float _OutlineWidth;
                float _CornerExpand;
                float _OutlineOn;
                float _Cull;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = _BaseColor.rgb;
                float3 normalWS = normalize(input.normalWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                float shadowAtten = lerp(0.40, 1.0, mainLight.shadowAttenuation);

                float NdotL = dot(normalWS, mainLight.direction);
                float halfLambert = saturate(NdotL * 0.5 + 0.5);

                float lightFactor = halfLambert * shadowAtten;
                float toonRamp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, lightFactor);

                half3 lightColor = albedo * (mainLight.color + 0.25);
                half3 shadowColor = albedo * _ShadowColor.rgb;
                half3 diffuse = lerp(shadowColor, lightColor, toonRamp);

                half3 ambient = albedo * _AmbientStrength;
                half3 emission = _EmissionColor.rgb;

                half3 finalColor = diffuse + ambient + emission;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // =========================================================================
        // PASS 2: CORNER-EXPANDED INVERTED HULL OUTLINE (Angle-Independent)
        // =========================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _OutlineColor;
                float4 _EmissionColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _AmbientStrength;
                float _OutlineWidth;
                float _CornerExpand;
                float _OutlineOn;
                float _Cull;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                #if defined(_OUTLINE_ON)
                if (_OutlineWidth <= 0.01)
                {
                    output.positionCS = float4(0, 0, -10, 1);
                    return output;
                }

                // Blend normal with object-space vertex radial direction to seal hard 90° box corners
                float3 radialOS = normalize(input.positionOS.xyz);
                float3 extrudeDirOS = normalize(lerp(input.normalOS, radialOS, _CornerExpand));
                float3 extrudeDirWS = TransformObjectToWorldDir(extrudeDirOS);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS += extrudeDirWS * (_OutlineWidth * 0.0035);

                float4 posCS = TransformWorldToHClip(posWS);

                // Depth bias: nudge slightly closer to camera in clip space so grazing angles never eat the outline
                #if UNITY_REVERSED_Z
                posCS.z += 0.0004 * posCS.w;
                #else
                posCS.z -= 0.0004 * posCS.w;
                #endif

                output.positionCS = posCS;
                #else
                output.positionCS = float4(0, 0, -10, 1);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // =========================================================================
        // PASS 3: DEPTH NORMALS PASS (For SSAO and Screen-space effects)
        // =========================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                return half4(normalWS * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }

        // =========================================================================
        // PASS 4: SHADOW CASTER
        // =========================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
