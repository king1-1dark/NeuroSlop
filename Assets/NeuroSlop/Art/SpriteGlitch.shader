// Unlit sprite shader for URP 2D with the project's "cheap AI" artefacts:
// hit/telegraph flash, horizontal slice tearing + colour split (_Glitch) and pixel dissolve
// (_Dissolve) used for "generating" / "deleting" characters. Driven per-renderer via
// MaterialPropertyBlock from CharacterView.
Shader "NeuroSlop/SpriteGlitch"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _Flash ("Flash", Range(0,1)) = 0
        _Glitch ("Glitch", Range(0,1)) = 0
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // Core2D gives unity_SpriteColor / unity_SpriteProps: in Unity 6 the SpriteRenderer
            // tint and flip arrive there, not only in vertex data.
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
                half _Flash;
                half _Glitch;
                half _Dissolve;
                float _Seed;
            CBUFFER_END

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                float3 position = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(position);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float frame = floor(_Time.y * 20.0);
                float row = floor(uv.y * 28.0);
                float tear = Hash(float2(row + _Seed, frame));
                float shift = tear > 1.0 - _Glitch * 0.55 ? (Hash(float2(row, frame + 7.0)) - 0.5) * 0.3 * _Glitch : 0.0;
                uv.x += shift;

                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_Glitch > 0.01)
                {
                    float split = 0.025 * _Glitch;
                    half r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(split, 0)).r;
                    half b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(split, 0)).b;
                    c.r = lerp(c.r, r, saturate(_Glitch * 1.5));
                    c.b = lerp(c.b, b, saturate(_Glitch * 1.5));
                }

                c *= i.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _Flash);

                float2 cell = floor(uv * 48.0);
                if (Hash(cell + _Seed * 3.1) < _Dissolve) c.a = 0;

                return c;
            }
            ENDHLSL
        }
    }
}
