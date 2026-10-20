// Window glass as a see-through gap: paints what the sky shows at this spot on screen (RoomAmbience's gradient and
// stars, or the Paint background colour) and writes depth before the walls draw, so the wall behind the pane is hidden.
// shortcut: no halo glow, rain or snow in the gap; add them here if a window over the glow looks too dark.
Shader "StudySim/WindowCut"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "WindowCut"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StudySkyGradient); SAMPLER(sampler_StudySkyGradient);
            TEXTURE2D(_StudyStars); SAMPLER(sampler_StudyStars);
            float _StudyStarAlpha;
            float4 _StudyWindowSolid; // alpha 1: show this colour instead of the sky

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_StudyWindowSolid.a > 0.5) return half4(_StudyWindowSolid.rgb, 1);
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                // The sky image maps the gradient's texel centres to the bottom and top of the screen.
                half3 sky = SAMPLE_TEXTURE2D(_StudySkyGradient, sampler_StudySkyGradient, float2(0.5, 0.25 + 0.5 * uv.y)).rgb;
                half4 star = SAMPLE_TEXTURE2D(_StudyStars, sampler_StudyStars, uv);
                return half4(lerp(sky, star.rgb, star.a * _StudyStarAlpha), 1);
            }
            ENDHLSL
        }
    }
}
