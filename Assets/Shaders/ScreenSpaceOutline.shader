Shader "Hidden/ScreenSpaceOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.04, 0.04, 0.06, 1.0)
        _OutlineThickness("Outline Thickness (pixels)", Float) = 1.3
        _DepthSensitivity("Depth Sensitivity", Range(0.1, 5.0)) = 1.2
        _NormalSensitivity("Normal Sensitivity", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ScreenSpaceOutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float _DepthSensitivity;
                float _NormalSensitivity;
            CBUFFER_END

            float SampleLinearDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = 1.0 / _ScreenParams.xy;
                float2 offset = texelSize * _OutlineThickness;

                // Roberts Cross sampling pattern: 4 corners around current pixel
                float2 uvTopLeft     = uv + float2(-offset.x,  offset.y);
                float2 uvBottomRight = uv + float2( offset.x, -offset.y);
                float2 uvTopRight    = uv + float2( offset.x,  offset.y);
                float2 uvBottomLeft  = uv + float2(-offset.x, -offset.y);

                // Depth edge detection
                float depthTL = SampleLinearDepth(uvTopLeft);
                float depthBR = SampleLinearDepth(uvBottomRight);
                float depthTR = SampleLinearDepth(uvTopRight);
                float depthBL = SampleLinearDepth(uvBottomLeft);

                float depthDiff1 = abs(depthTL - depthBR);
                float depthDiff2 = abs(depthTR - depthBL);
                float depthGrad = sqrt(depthDiff1 * depthDiff1 + depthDiff2 * depthDiff2);

                // Distance-relative depth threshold (edges stay consistent near and far)
                float centerDepth = SampleLinearDepth(uv);
                float depthThreshold = (centerDepth * 0.04 + 0.025) * (1.0 / max(0.1, _DepthSensitivity));
                float depthEdge = smoothstep(depthThreshold * 0.7, depthThreshold * 1.3, depthGrad);

                // Normal edge detection
                float3 normalTL = SampleSceneNormals(uvTopLeft);
                float3 normalBR = SampleSceneNormals(uvBottomRight);
                float3 normalTR = SampleSceneNormals(uvTopRight);
                float3 normalBL = SampleSceneNormals(uvBottomLeft);

                float3 normalDiff1 = normalTL - normalBR;
                float3 normalDiff2 = normalTR - normalBL;
                float normalGrad = sqrt(dot(normalDiff1, normalDiff1) + dot(normalDiff2, normalDiff2));

                float normalThreshold = 0.35 * (1.0 / max(0.1, _NormalSensitivity));
                float normalEdge = smoothstep(normalThreshold * 0.7, normalThreshold * 1.3, normalGrad);

                // Combine depth and normal edges
                float edge = saturate(depthEdge + normalEdge);

                // Sample base scene color from camera color buffer
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Blend ink outline
                half3 resultColor = lerp(sceneColor.rgb, _OutlineColor.rgb, edge);

                return half4(resultColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
