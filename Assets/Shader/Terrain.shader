Shader "GPUTerrainLearn/Terrain"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HeightMap ("Texture", 2D) = "white" {}
        _NormalMap ("Texture", 2D) = "white" {}
        _Color("Diffuse",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalForward"}
        LOD 100

        Pass
        {
            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature ENABLE_MIP_DEBUG
            #pragma shader_feature ENABLE_PATCH_DEBUG
            #pragma shader_feature ENABLE_NODE_DEBUG
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./CommonInput.hlsl"
            StructuredBuffer<RenderPatch> PatchList;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
                
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                half3 color: TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _HeightMap;
            sampler2D _NormalMap;
            uniform float3 _WorldSize;
            float4x4 _WorldToNormalMapMatrix;
            half4 _Color;


            float3 TransformNormalToWorldSpace(float3 normal){
                return SafeNormalize(mul(normal,(float3x3)_WorldToNormalMapMatrix));
            }

             //在Node之间留出缝隙供Debug
            float3 ApplyNodeDebug(RenderPatch patch,float3 vertex){
                //每一边的Node数目：5^(2^(5-lod))
                uint nodeCount = (uint)(5 * pow(2,5 - patch.lod));
                
                float nodeSize = _WorldSize.x / nodeCount;
                uint2 nodeLoc = floor((patch.position + _WorldSize.xz * 0.5) / nodeSize);
                float2 nodeCenterPosition = - _WorldSize.xz * 0.5 + (nodeLoc + 0.5) * nodeSize ;
                vertex.xz = nodeCenterPosition + (vertex.xz - nodeCenterPosition) * 0.95;
                return vertex;
            }

            float3 SampleNormal(float2 uv){
                float3 normal;
                normal.xz = tex2Dlod(_NormalMap,float4(uv,0,0)).xy * 2 - 1;
                normal.y = sqrt(max(0,1 - dot(normal.xz,normal.xz)));
                normal = TransformNormalToWorldSpace(normal);
                return normal;
            }

            uniform float4x4 _HizCameraMatrixVP;
            float3 TransformWorldToUVD(float3 positionWS)
            {
                float4 positionHS = mul(_HizCameraMatrixVP, float4(positionWS, 1.0));
                float3 uvd = positionHS.xyz / positionHS.w;
                uvd.xy = (uvd.xy + 1) * 0.5;
                return uvd;
            }

            v2f vert (appdata v)
            {
                v2f o;
                
                float4 inVertex = v.vertex;
                float2 uv = v.uv;

                RenderPatch patch = PatchList[v.instanceID];
                uint lod = patch.lod;
                //lod越大，patch就被放的越大
                float scale = pow(2,lod);
                inVertex.xz *= scale;
                
                #if ENABLE_PATCH_DEBUG
                //缩小顶点 留出边界
                inVertex.xz*=0.9;
                #endif
                inVertex.xz += patch.position;

                
               //高度图部分
                float2 heightUV = (inVertex.xz + (_WorldSize.xz * 0.5) + 0.5) / (_WorldSize.xz + 1);
                float height = tex2Dlod(_HeightMap,float4(heightUV,0,0)).r;
                inVertex.y = height * _WorldSize.y;
                #if ENABLE_NODE_DEBUG
                inVertex.xyz=ApplyNodeDebug(patch,inVertex.xyz);
                #endif

                //法线图
                float3 normal = SampleNormal(heightUV);
                Light light = GetMainLight();
                o.color = max(0.05,dot(light.direction,normal));

                float4 vertex = TransformObjectToHClip(inVertex.xyz);
                o.vertex = vertex;
                o.uv = uv * scale * 8;
     
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color;
                return col;
            }
            ENDHLSL
        }
    }
}
