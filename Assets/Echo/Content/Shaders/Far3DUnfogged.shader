// Time Run art fix, phase 3: the Far Far Future's sky, skyline silhouettes, beacons and planet. Unlit and never fogged,
// so the sky keeps its gradient and the horizon's outline stays crisp behind the fogged city. The colour can go above 1
// (beacons bloom). _Shade > 0 lights the surface from _LightDir (the planet), so it reads as a sphere.
Shader "Echo/Far3D Unfogged"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Shade ("Shading", Range(0, 1)) = 0
        _LightDir ("Light direction (towards the light)", Vector) = (-0.6, 0.5, -0.6, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unfogged"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Shade;
                float4 _LightDir;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half lambert = saturate(dot(normalize(i.normalWS), normalize(_LightDir.xyz)));
                c.rgb *= lerp(1.0, 0.06 + 0.94 * smoothstep(0.0, 0.7, lambert), _Shade);
                return half4(c.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
