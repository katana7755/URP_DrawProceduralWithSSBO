Shader "URP Custom/DrawProceduralShader"
{
    Properties
    {
        [HideInInspector][MainTexture]  _BaseMap("Base Map (RGB) Smoothness / Alpha (A)", 2D) = "white" {}
        [MainColor]                     _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0.0      
        [Toggle] _ZWrite("Z Write", Float) = 1.0  
        [Enum(Front,2,Back,1,Both,0)] _Cull("Culling", Float) = 2.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Use same blending / depth states as Standard shader
            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _ _SPECGLOSSMAP _SPECULAR_COLOR
            #pragma shader_feature _GLOSSINESS_FROM_BASE_ALPHA
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #pragma vertex LitPassVertexSimple_Custom
            #pragma fragment LitPassFragmentSimple
            #define BUMP_SCALE_NOT_SUPPORTED 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"

            struct VertexData
            {
                float3 _Position;
                float3 _Normal;
            };

            struct IndexData
            {
                int _Value;
            };

            struct InstanceData
            {
                float4x4 _LocalToWorldMatrix;
                int _VertexStart;
                int _IndexStart;
                int _IndexEnd;
            };      

            StructuredBuffer<VertexData> VERTEX_BUFFER;                  
            StructuredBuffer<IndexData> INDEX_BUFFER;
            StructuredBuffer<InstanceData> INSTANCE_BUFFER;

            struct Attributes_Custom
            {
                uint _InstanceID    : SV_InstanceID;
                uint _VertexID      : SV_VertexID;
            };

            Varyings LitPassVertexSimple_Custom(Attributes_Custom input)
            {
                Varyings output = (Varyings)0;
                InstanceData instanceData = INSTANCE_BUFFER[input._InstanceID];

                if (instanceData._IndexStart + (int)input._VertexID < instanceData._IndexEnd)
                {                    
                    IndexData indexData = INDEX_BUFFER[instanceData._IndexStart + input._VertexID];
                    VertexData vertexData = VERTEX_BUFFER[instanceData._VertexStart + indexData._Value];

                    VertexPositionInputs vertexInput;
                    vertexInput.positionWS = mul(instanceData._LocalToWorldMatrix, float4(vertexData._Position, 1.0)).xyz;
                    vertexInput.positionVS = TransformWorldToView(vertexInput.positionWS);
                    vertexInput.positionCS = TransformWorldToHClip(vertexInput.positionWS);

                    float4 ndc = vertexInput.positionCS * 0.5f;
                    vertexInput.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                    vertexInput.positionNDC.zw = vertexInput.positionCS.zw;

                    output.positionCS = vertexInput.positionCS;
                    output.normal = mul((float3x3)instanceData._LocalToWorldMatrix, vertexData._Normal);
                    output.normal = SafeNormalize(output.normal);                    
                    output.viewDir = GetCameraPositionWS() - vertexInput.positionWS;
                }

            //     VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
            //     VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            //     half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
            //     half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
            //     half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

            //     output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            //     output.posWS.xyz = vertexInput.positionWS;
            //     output.positionCS = vertexInput.positionCS;

            // #ifdef _NORMALMAP
            //     output.normal = half4(normalInput.normalWS, viewDirWS.x);
            //     output.tangent = half4(normalInput.tangentWS, viewDirWS.y);
            //     output.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
            // #else
            //     output.normal = NormalizeNormalPerVertex(normalInput.normalWS);
            //     output.viewDir = viewDirWS;
            // #endif

            //     OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
            //     OUTPUT_SH(output.normal.xyz, output.vertexSH);

            //     output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

            // #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            //     output.shadowCoord = GetShadowCoord(vertexInput);
            // #endif

                return output;
            }

            ENDHLSL
        }
    }
}
