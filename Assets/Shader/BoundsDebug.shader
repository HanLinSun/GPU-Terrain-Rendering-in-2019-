Shader "Custom/BoundsDebug"
{
    Properties
    {
    }
    SubShader
    {
        Tags{"RenderType"="Opaque""LightMode"="UniversialForward"}
        
        Pass
        {
            LOD 100
            
            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./CommonInput.hlsl"

            StructuredBuffer<BoundsDebug> BoundsList;
            
            #pragma vert vertexShader
            #pragma frag fragmentShader
            struct appdata
            {
                float4 vertex:POSITION;
                uint instanceID:SV_InstanceID;
            };
            
            struct v2f
            {
                float4 vertex:SV_POSITION;
                float3 color:TEXCOORD1;
            };
            
            v2f vertexShader(appdata v)
            {
                v2f o;
                float4 inVertex=v.vertex;
                //从computeShader中传递boundsList到着色shader
                BoundsDebug boundsDebug=BoundsList[v.instanceID];
                Bounds bounds=boundsDebug.bounds;
                //计算包围盒中心点和放大倍率
                float3 center=(bounds.minPosition+bounds.maxPosition)*0.5;
                float3 scale=(bounds.maxPosition-center)/0.5;
                //顶点变换
                inVertex.xyz=inVertex.xyz*scale+center;
                float4 vertex=TransformObjectToHClip(inVertex.xyz);
                o.vertex=vertex;
                o.color=boundsDebug.color;
                return o;
                
            }
            half4 fragmentShader(v2f i):SV_Target
            {
                half4 col=half4(i.color,1);
                return col;
            }
            ENDHLSL
        }
        
    }
    FallBack "Diffuse"
}
