// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Maestro/Watercolor"
{
    Properties
    {
        [HideInInspector] _LiquidRippleCount ("Live liquid contact ripples", Float) = 0
        _WorldLighting ("Accept world illumination", Float) = 1
        _Color ("Pigment", Color) = (1,.94,.82,1)
        _PatternMode ("Pattern: solid/checker/stripes", Float) = 0
        _PatternPlane ("Pattern: UV/XY/XZ/YZ", Float) = 0
        _PatternColor ("Alternate pigment", Color) = (1,1,1,1)
        _PatternCounts ("Pattern columns and rows", Vector) = (1,1,0,0)
        _PatternCoordinates ("Normalized part coordinates", Vector) = (1,1,1,0)
        _MainTex ("Page image", 2D) = "white" {}
        _Grain ("Paper grain", Range(0,.2)) = .07
        _PigmentTex ("Dry watercolor", 2D) = "white" {}
        _Shading ("Paper face shade", Range(0,.3)) = .12
        _HasRestCoordinates ("Rest-space coordinates", Float) = 0
        _PencilWidth ("Graphite silhouette in meters", Range(0,.005)) = .0012
        _AlphaCutoff ("Texture cutout", Range(0,1)) = 0
        _SurfaceMode ("Opaque / cutout / blend", Float) = 0
        _SurfaceOpacity ("Surface opacity", Range(0,1)) = 1
        [HideInInspector] _SurfaceCull ("Surface culling", Float) = 0
        [HideInInspector] _SrcBlend ("Source blending", Float) = 1
        [HideInInspector] _DstBlend ("Destination blending", Float) = 0
        [HideInInspector] _ZWrite ("Depth writing", Float) = 1
        [HideInInspector] _VisibilityOpacity ("Layer opacity", Range(0,1)) = 1
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
        _DecodeBrowserSrgb ("Raw browser sRGB pixels", Float) = 0
    }
    SubShader
    {
        // Dynamic batching replaces object coordinates with world coordinates.
        // Keep local pigment/patterns intact; shared meshes can still use GPU instancing.
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Name "PIGMENT"
            Tags { "LightMode"="ForwardBase" }
            Cull [_SurfaceCull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "MaestroEnvironmentDepth.cginc"
            #include "MaestroWorldLighting.cginc"
            #include "WatercolorSurface.cginc"
            half4 frag(Varying input) : SV_Target {
                half4 surface = MaestroSurface(input);
                float face = 1 - _Shading * (1 - saturate(dot(normalize(input.normal), normalize(float3(-.3,.8,-.5)))));
                return half4(MaestroAtmosphere(surface.rgb * MaestroLight(input.worldNormal, face), input.world), surface.a);
            }
            ENDCG
        }
        Pass
        {
            Name "LOCAL_LIGHT"
            Tags { "LightMode"="ForwardAdd" }
            Cull [_SurfaceCull]
            ZWrite Off
            Blend One One
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdadd_fullshadows
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "MaestroEnvironmentDepth.cginc"
            #include "MaestroWorldLighting.cginc"
            #define MAESTRO_FORWARD_ADD 1
            #include "WatercolorSurface.cginc"
            float _MaestroLocalLightingEnabled;
            half4 frag(Varying input) : SV_Target {
                // The authored sun remains in the shared day/weather projection.
                #if defined(DIRECTIONAL) || defined(DIRECTIONAL_COOKIE)
                    return 0;
                #else
                    if (_MaestroLocalLightingEnabled < .5 || _WorldLighting < .5) clip(-1);
                    half4 surface = MaestroSurface(input);
                    UNITY_LIGHT_ATTENUATION(attenuation, input, input.world);
                    float facing = saturate(dot(normalize(input.worldNormal), normalize(UnityWorldSpaceLightDir(input.world))));
                    float fog = exp(-max(0, _MaestroFogDensity) * length(UnityWorldSpaceViewDir(input.world)));
                    return half4(surface.rgb * surface.a * _LightColor0.rgb * attenuation * facing * fog, 0);
                #endif
            }
            ENDCG
        }
        Pass
        {
            Name "PENCIL"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "MaestroEnvironmentDepth.cginc"
            #include "MaestroWorldLighting.cginc"
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float alpha : TEXCOORD2; float3 worldNormal : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            float _PencilWidth;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff, _SurfaceMode, _SurfaceOpacity;
            float _VisibilityOpacity, _VisibilityRealDepth;
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                world += UnityObjectToWorldNormal(input.normal) * _PencilWidth;
                output.position = mul(UNITY_MATRIX_VP, float4(world, 1));
                output.world = world;
                output.alpha = input.color.a;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityOpacity <= 0) clip(-1);
                if (_VisibilityRealDepth > .5) MaestroOccludeEnvironment(input.world);
                float alpha = tex2D(_MainTex, input.uv).a * (_SurfaceMode > .5 ? _SurfaceOpacity * input.alpha : 1);
                clip(alpha - _AlphaCutoff);
                return half4(MaestroAtmosphere(float3(.204,.176,.169) * MaestroLight(input.worldNormal, 1), input.world),_VisibilityOpacity);
            }
            ENDCG
        }
        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_SurfaceCull]
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            struct Vertex { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { V2F_SHADOW_CASTER; float2 uv:TEXCOORD1; float alpha:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; float4 _MainTex_ST;
            float _WorldLighting, _SurfaceMode, _SurfaceOpacity, _AlphaCutoff, _VisibilityOpacity;
            Varying vert(Vertex v) {
                Varying o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.alpha=v.color.a;return o;
            }
            float4 frag(Varying i):SV_Target {
                // Blend surfaces and faded layers transmit light. Cutouts retain their actual holes.
                if (_WorldLighting < .5 || _SurfaceMode > 1.5 || _VisibilityOpacity < .999) clip(-1);
                float alpha=tex2D(_MainTex,i.uv).a * (_SurfaceMode > .5 ? _SurfaceOpacity*i.alpha : 1);
                clip(alpha-_AlphaCutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
