Shader "Sprites/Area10DimensionWarp"
{
    // ★Area10 Final Stage開始の「次元移動」演出用（Area10FinalIntroControllerが使用）。
    //   開始の瞬間に取り込んだゲーム画面（_CaptureTex）を、画面中央へ渦を巻きながら吸い込まれるように歪め、
    //   色ずれ（RGB分離）・横ずれ（スライス）・走査線・ブロックノイズのグリッチを重ねる。最後は_Darkenで暗転する。
    //   このプロジェクトはURPの2D Rendererのため、Sprites/Additiveと同じく"Universal2D"のPassで描画する。
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _CaptureTex ("Captured Screen", 2D) = "black" {}
        _Suck ("Suck (吸い込み)", Float) = 0
        _Twist ("Twist (渦の回転, rad)", Float) = 0
        _Glitch ("Glitch (横ずれ)", Range(0, 1)) = 0
        _RGBSplit ("RGB Split (色ずれ)", Range(0, 1)) = 0
        _Scanline ("Scanline (走査線)", Range(0, 1)) = 0
        _BlockNoise ("Block Noise", Range(0, 1)) = 0
        _BlockGrid ("Block Grid (cols, rows)", Vector) = (53, 30, 0, 0)
        _BlockCoverage ("Block Coverage", Range(0, 1)) = 0.08
        _BlockOpacity ("Block Opacity", Range(0, 1)) = 0.85
        _Darken ("Darken (暗転)", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
        _Aspect ("Screen Aspect (w/h)", Float) = 1.7778
        _FlipY ("Flip Y", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        struct Attributes
        {
            float3 positionOS   : POSITION;
            float4 color        : COLOR;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4  positionCS  : SV_POSITION;
            half4   color       : COLOR;
            float2  uv          : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_CaptureTex);
        SAMPLER(sampler_CaptureTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Suck;
            float _Twist;
            float _Glitch;
            float _RGBSplit;
            float _Scanline;
            float _BlockNoise;
            float4 _BlockGrid;
            float _BlockCoverage;
            float _BlockOpacity;
            float _Darken;
            float _Seed;
            float _Aspect;
            float _FlipY;
        CBUFFER_END

        float Hash11(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

        float Hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

        // Area1〜9の色（AreaSelectのノード色と同じ）
        static const half3 AreaColors[9] =
        {
            half3(0.608, 0.561, 0.780), half3(0.298, 0.686, 0.490), half3(0.553, 0.600, 0.682),
            half3(0.878, 0.478, 0.247), half3(0.698, 0.227, 0.322), half3(0.878, 0.690, 0.310),
            half3(0.310, 0.561, 0.878), half3(0.373, 0.839, 0.839), half3(0.639, 0.682, 0.878)
        };

        half3 SampleCapture(float2 uv)
        {
            float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
            if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
            return SAMPLE_TEXTURE2D(_CaptureTex, sampler_CaptureTex, saturate(uv)).rgb * inside;
        }

        Varyings WarpVertex(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.uv = v.uv;
            o.color = v.color * _Color * unity_SpriteColor;
            return o;
        }

        half4 WarpFragment(Varyings i) : SV_Target
        {
            float2 uv = i.uv;

            // 横ずれ（スライス）：画面を横帯に分け、_Glitchが強いほど多くの帯をランダムにずらす
            float row = floor(uv.y * 28.0);
            float rnd = Hash11(row + _Seed * 17.31);
            float sliceOn = step(1.0 - _Glitch * 0.55, rnd);
            float shift = (Hash11(row * 3.7 + _Seed * 5.13) - 0.5) * 0.14 * _Glitch * sliceOn;

            // 吸い込み：中心からの距離に応じて外側の像を引き寄せ（＝像が中心へ縮む）、中心ほど強く回転させる
            float2 c = uv - 0.5;
            c.x *= _Aspect;
            float r = length(c);
            float nearCenter = saturate(1.0 - r / 1.0);
            float ang = _Twist * nearCenter * nearCenter;
            float s, cs;
            sincos(ang, s, cs);
            float2 rc = float2(c.x * cs - c.y * s, c.x * s + c.y * cs);
            rc *= 1.0 + _Suck * (1.0 + 2.0 * nearCenter);
            rc.x /= _Aspect;
            float2 suv = rc + 0.5;
            suv.x += shift;

            // 色ずれ（RGB分離）
            float split = _RGBSplit * (0.004 + 0.02 * _Glitch);
            half3 col;
            col.r = SampleCapture(suv + float2(split, 0)).r;
            col.g = SampleCapture(suv).g;
            col.b = SampleCapture(suv - float2(split, 0)).b;

            // ブロックノイズ（Area1〜9の色の小さなブロックがランダムに瞬く）
            //   _BlockGrid：画面のマス数（横, 縦）／_BlockCoverage：_BlockNoise=1の時にブロックになるマスの割合／_BlockOpacity：ブロックの濃さ
            float2 grid = floor(i.uv * max(_BlockGrid.xy, float2(1.0, 1.0)));
            float seedStep = floor(_Seed * 3.0);
            float bn = Hash21(grid + seedStep * 1.37);
            float blockOn = step(1.0 - _BlockNoise * _BlockCoverage, bn);
            int ci = (int)min(8.0, floor(Hash21(grid * 1.91 + seedStep) * 9.0));
            col = lerp(col, AreaColors[ci], blockOn * _BlockOpacity);

            // 走査線
            float sl = 0.5 + 0.5 * sin(i.uv.y * _ScreenParams.y * 1.5708);
            col *= 1.0 - _Scanline * 0.4 * sl;

            // 暗転
            col *= 1.0 - _Darken;

            return half4(col, i.color.a);
        }
        ENDHLSL

        // ★2D Rendererはこちらのpassを実行する（メインの描画経路）
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex WarpVertex
            #pragma fragment WarpFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // ★Universal Renderer(標準Forward)向けの互換用パス
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex WarpVertex
            #pragma fragment WarpFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
