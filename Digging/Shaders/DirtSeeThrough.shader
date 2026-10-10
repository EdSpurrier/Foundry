// Dirt for Rounded DiggableTerrain (URP). Opaque and lit by the main light (with shadows), ambient light and fog;
// textured by world-space triplanar projection (no UVs needed), with steep surfaces tinted by Side Color.
//
// See-through window (driven by a SeeThroughWindow component, via the global _FoundrySeeThrough = centre xyz, radius w):
//  - the dirt itself (opaque) is cut away wherever the window reaches - including its soft edge - in front of the
//    window's plane (closer to the camera than the chicken)
//  - the cap (_SeeThroughCap = 1, a runtime copy of this material) is the slice through the dirt at the plane, drawn
//    ONLY where the window reaches - so it fills the cut-away area
//  - the band (_SeeThroughBand = 1, a transparent runtime copy drawn over the same mesh) puts the front dirt back across
//    the soft edge, fading from solid at the rim to clear inside - a smooth blur over the cap rather than a hard line
// With no window (radius 0) the dirt is drawn whole and the cap and band are hidden.
Shader "Foundry/Dirt (See-Through)"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.55, 0.4, 0.26, 1)
        _SideColor ("Side Color (steep walls)", Color) = (0.42, 0.3, 0.19, 1)
        _TextureScale ("Texture Repeats Per Metre", Float) = 0.5
        [HideInInspector] _SeeThroughCap ("Cap (set by script)", Float) = 0
        [HideInInspector] _SeeThroughBand ("Band (set by script)", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SideColor;
            float _TextureScale;
            float _SeeThroughCap;
            float _SeeThroughBand;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Set globally by SeeThroughWindow
        float4 _FoundrySeeThrough;
        float _FoundrySeeThroughSoftness;

        // How much of this pixel is "inside the window": 1 well inside, 0 outside, a ramp across the soft edge
        float SeeThroughInside(float3 positionWS)
        {
            float radius = _FoundrySeeThrough.w;
            if (radius <= 0.0)
                return 0.0;

            float distanceXY = length(positionWS.xy - _FoundrySeeThrough.xy);
            float softness = max(_FoundrySeeThroughSoftness, 0.0001);
            return 1.0 - smoothstep(radius - softness, radius, distanceXY);
        }

        // Discards what's cut away, and returns the opacity to draw the rest at:
        //  - cap: only where the window reaches (opaque)
        //  - dirt in front of the plane: cut away wherever the window reaches; the band copy draws it back across the soft
        //    edge, fading out toward the inside
        //  - dirt behind the plane: drawn as normal (the band copy skips it)
        float SeeThroughAlpha(float3 positionWS)
        {
            float inside = SeeThroughInside(positionWS);

            if (_SeeThroughCap > 0.5)
            {
                clip(inside - 0.0001);
                return 1.0;
            }

            bool inFront = positionWS.z < _FoundrySeeThrough.z - 0.01;
            if (_SeeThroughBand > 0.5)
            {
                clip(inFront ? 1.0 : -1.0);
                clip(inside - 0.0001);
                clip(0.9999 - inside);
                return 1.0 - inside;
            }

            if (inFront)
                clip(0.0001 - inside);
            return 1.0;
        }

        // Depth passes: the same cut as the opaque dirt and the cap
        void SeeThroughClip(float3 positionWS, float2 pixel)
        {
            SeeThroughAlpha(positionWS);
        }

        // World-space triplanar texture, tinted toward Side Color where the surface is steep (facing sideways or up/down
        // rather than at the camera)
        half3 DirtAlbedo(float3 positionWS, float3 normalWS)
        {
            float3 weights = pow(abs(normalWS), 4.0);
            weights /= max(weights.x + weights.y + weights.z, 0.0001);

            float scale = _TextureScale;
            half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * scale).rgb;
            half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * scale).rgb;
            half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * scale).rgb;
            half3 texture3 = x * weights.x + y * weights.y + z * weights.z;

            half facing = saturate(abs(normalWS.z));
            half3 tint = lerp(_SideColor.rgb, _BaseColor.rgb, facing);
            return texture3 * tint;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float alpha = SeeThroughAlpha(input.positionWS);

                float3 normalWS = normalize(input.normalWS);
                half3 albedo = DirtAlbedo(input.positionWS, normalWS);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.screenPos;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half lambert = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS) + mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * lambert);

                half3 color = albedo * lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth (and depth + normals) must cut the same window, or a depth prepass would hide what's behind it
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                SeeThroughClip(input.positionWS, input.positionCS.xy);
                return half4(input.positionCS.z, 0, 0, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                SeeThroughClip(input.positionWS, input.positionCS.xy);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
