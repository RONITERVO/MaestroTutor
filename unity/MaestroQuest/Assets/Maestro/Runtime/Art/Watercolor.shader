// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Maestro/Watercolor"
{
    Properties
    {
        _Color ("Pigment", Color) = (1,.94,.82,1)
        _MainTex ("Page image", 2D) = "white" {}
        _Grain ("Paper grain", Range(0,.2)) = .07
        _PigmentTex ("Dry watercolor", 2D) = "white" {}
        _Shading ("Paper face shade", Range(0,.3)) = .12
        _HasRestCoordinates ("Rest-space coordinates", Float) = 0
        _PencilWidth ("Graphite silhouette in meters", Range(0,.005)) = .0012
        _AlphaCutoff ("Imported texture cutout", Range(0,1)) = 0
        _DecodeBrowserSrgb ("Raw browser sRGB pixels", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "PENCIL"
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float _PencilWidth;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff;
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                world += UnityObjectToWorldNormal(input.normal) * _PencilWidth;
                output.position = mul(UNITY_MATRIX_VP, float4(world, 1));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(tex2D(_MainTex, input.uv).a - _AlphaCutoff);
                return fixed4(.204,.176,.169,1);
            }
            ENDCG
        }
        Pass
        {
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float3 rest : TEXCOORD2; float3 restNormal : TEXCOORD3; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 local : TEXCOORD1; float3 normal : TEXCOORD2; float3 pigmentNormal : TEXCOORD3; float4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            sampler2D _PigmentTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Grain;
            float _Shading;
            float _HasRestCoordinates;
            float _AlphaCutoff;
            float _DecodeBrowserSrgb;
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.local = lerp(input.vertex.xyz, input.rest, _HasRestCoordinates);
                output.normal = input.normal;
                output.pigmentNormal = lerp(input.normal, input.restNormal, _HasRestCoordinates);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                fixed4 surface = tex2D(_MainTex, input.uv);
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_DecodeBrowserSrgb > .5) surface.rgb = GammaToLinearSpace(surface.rgb);
                #endif
                clip(surface.a - _AlphaCutoff);
                // Object-space pigment remains fixed through head motion and between eyes.
                float3 weights = abs(normalize(input.pigmentNormal));
                weights /= max(.001, weights.x + weights.y + weights.z);
                float dry = dot(weights, float3(tex2D(_PigmentTex,input.local.yz*1.8).r, tex2D(_PigmentTex,input.local.xz*1.8).r, tex2D(_PigmentTex,input.local.xy*1.8).r));
                float pigment = lerp(1, dry, saturate(_Grain * 8));
                float face = 1 - _Shading * (1 - saturate(dot(normalize(input.normal), normalize(float3(-.3,.8,-.5)))));
                return fixed4(surface.rgb * _Color.rgb * input.color.rgb * pigment * face, 1);
            }
            ENDCG
        }
    }
}
