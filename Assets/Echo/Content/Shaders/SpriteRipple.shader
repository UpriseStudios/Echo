// Time Run art fix, phase 2: the Far Future's wet floor. An unlit sprite whose texture is sampled with a moving sideways
// ripple that grows with depth (the mirrored layer's distance below the floor line), so its reflection shimmers like water;
// its colour can rise above 1 so the neon in it still blooms.
Shader "Echo/Sprite Ripple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Amplitude ("Ripple (UV)", Float) = 0.004
        _Frequency ("Ripples per unit", Float) = 9
        _Speed ("Speed", Float) = 2.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                float _Amplitude;
                float _Frequency;
                float _Speed;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                float3 world = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                o.depth = max(0.0, -world.y);   // how far below the floor line, in units
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float wave = sin(i.depth * _Frequency + _Time.y * _Speed) + 0.5 * sin(i.depth * _Frequency * 2.3 - _Time.y * _Speed * 1.7);
                float2 uv = i.uv + float2(wave * _Amplitude * (0.5 + min(i.depth, 2.5)), 0.0);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;
                c.rgb *= _Intensity;
                return c;
            }
            ENDHLSL
        }
    }
}
