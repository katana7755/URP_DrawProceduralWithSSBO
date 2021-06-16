#define DRAWPROCEDURAL_MODE_CPU

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "New DrawProcedural Target DB.asset", menuName = "URP Custom/DrawProcedural/Database Asset")]
public class DrawProceduralTargetDB : ScriptableObject
{
    [SerializeField] private Material _Material;

#if UNITY_EDITOR
    private void OnEnable()
    {   
        PlayModeChangeListener.ExternalCallbacks += ClearAllResources;
    }

    private void OnDisable()
    {        
        PlayModeChangeListener.ExternalCallbacks -= ClearAllResources;
        ClearAllResources();
    }
#endif

    public void ClearAllResources()
    {        
        m_InputDataList.Clear();
        m_VertexOffset = 0;
        m_VertexSize = 0;       

        if (m_VertexGPUBuffer != null) 
        {
            m_VertexGPUBuffer.Release();
            m_VertexGPUBuffer = null;
        }

        m_IndexOffset = 0;
        m_IndexSize = 0;        

        if (m_IndexGPUBuffer != null)
        {
            m_IndexGPUBuffer.Release();
            m_IndexGPUBuffer = null;
        }

        m_InstanceOffset = 0;
        m_InstanceSize = 0;    

        if (m_InstanceGPUBuffer != null)
        {
            m_InstanceGPUBuffer.Release();
            m_InstanceGPUBuffer = null;
        }

        m_MaxIndexCount = 0;        
    }

    // TODO: need to implement the functionality in which we can share the previously registered mesh if the requested one is the same.
    // TODO: We need to consider removing too. So, the final shape will be that you can add and remove freely
    //       Removing will be done by removing the item first, and filling the hole with the last item.
    public void AddInput(InputData inputData)
    {        
        if (inputData._Mesh == null)
        {
            Debug.LogWarning("[DrawProceduralTargetDB] the mesh in all input datas shouldn't be null");
            return;
        }

        m_InputDataList.Add(inputData);
    }

    public void ConstructGPUBuffers()
    {        
#if DRAWPROCEDURAL_MODE_CPU
        ConstructGPUBuffersInCPU();
#else
        ConstructGPUBuffersInGPU();
#endif        
    }

    public void BindAllGPUResources()
    {
        if (_Material == null || m_MaxIndexCount <= 0 || m_InstanceOffset <= 0)
        {
            return;
        }

        _Material.SetBuffer(ShaderProperty.VERTEX_BUFFER, m_VertexGPUBuffer);
        _Material.SetBuffer(ShaderProperty.INDEX_BUFFER, m_IndexGPUBuffer);
        _Material.SetBuffer(ShaderProperty.INSTANCE_BUFFER, m_InstanceGPUBuffer);
    }

    public void Render(CommandBuffer cmdBuf)
    {
        if (_Material == null || m_MaxIndexCount <= 0 || m_InstanceOffset <= 0)
        {
            return;
        }    

        cmdBuf.DrawProcedural(Matrix4x4.identity, _Material, 0, MeshTopology.Triangles, m_MaxIndexCount, m_InstanceOffset);
    }    

#if DRAWPROCEDURAL_MODE_CPU
    private void ConstructGPUBuffersInCPU()
    {       
        CheckGPUBuffersValidity();

        if ( m_InputDataList.Count <= 0)
        {
            return;
        }

        foreach (var inputData in m_InputDataList)
        {
            var vertices = inputData._Mesh.vertices;
            var normals = inputData._Mesh.normals;
            var indices = inputData._Mesh.triangles;

            if (m_VertexOffset + vertices.Length > m_VertexSize)
            {
                Debug.LogWarning("[DrawProceduralTargetDB] the vertex buffer reached to its size limit. you need to make a new batch.");
                break;
            }

            if (m_IndexOffset + indices.Length > m_IndexSize)
            {
                Debug.LogWarning("[DrawProceduralTargetDB] the index buffer reached to its size limit. you need to make a new batch.");
                break;
            }            

            if (m_InstanceOffset + 1 > m_InstanceSize)
            {
                Debug.LogWarning("[DrawProceduralTargetDB] the instance buffer reached to its size limit. you need to make a new batch.");
                break;
            } 

            int vertexStart = m_VertexOffset;
            int indexStart = m_IndexOffset;
            m_VertexOffset = CopyDataIntoGPUBuffer<Vector3, Vector3, VertexData>(m_VertexGPUBuffer, m_VertexOffset, m_VertexSize, vertices, normals, VertexData.Parse);
            m_IndexOffset = CopyDataIntoGPUBuffer<int, IndexData>(m_IndexGPUBuffer, m_IndexOffset, m_IndexSize, indices, IndexData.Parse);
            m_InstanceOffset = CopySingleDataIntoGPUBuffer<Matrix4x4, int, int, int, InstanceData>(m_InstanceGPUBuffer, m_InstanceOffset, m_InstanceSize, inputData._LocalToWorldMatrix, vertexStart, indexStart, m_IndexOffset, InstanceData.Parse);
            m_MaxIndexCount = Mathf.Max(m_MaxIndexCount, indices.Length);
        }

        m_InputDataList.Clear();
    }

    private void CheckGPUBuffersValidity()
    {
        if (m_VertexGPUBuffer == null)
        {
            int dataSize = Marshal.SizeOf(new VertexData());
            m_VertexOffset = 0;
            m_VertexSize = MAX_GPU_BUFFER_SIZE / dataSize;
            m_VertexGPUBuffer = new ComputeBuffer(m_VertexSize, dataSize);
        }

        if (m_IndexGPUBuffer == null)
        {
            int dataSize = Marshal.SizeOf(new IndexData());
            m_IndexOffset = 0;
            m_IndexSize = MAX_GPU_BUFFER_SIZE / dataSize;
            m_IndexGPUBuffer = new ComputeBuffer(m_IndexSize, dataSize);
        }        

        if (m_InstanceGPUBuffer == null)
        {
            int dataSize = Marshal.SizeOf(new InstanceData());
            m_InstanceOffset = 0;
            m_InstanceSize = MAX_GPU_BUFFER_SIZE / dataSize;            
            m_InstanceGPUBuffer = new ComputeBuffer(m_InstanceSize, dataSize);
        }        
    }    

    private int CopyDataIntoGPUBuffer<TSourceType, TDestType>(ComputeBuffer destGPUBuffer, int destOffset, int destSize, TSourceType[] sourceBuffer, System.Func<TSourceType, TDestType> funcParse) 
        where TSourceType : struct
        where TDestType : struct
    {        
        var cpuBuffer = new TDestType[sourceBuffer.Length];
        destGPUBuffer.GetData(cpuBuffer, 0, destOffset, sourceBuffer.Length);

        // TODO: Will it be better to use Job System for making this multi-threaded???
        for (int i = 0; i < sourceBuffer.Length; ++i)
        {                    
            cpuBuffer[i] = funcParse(sourceBuffer[i]);
        }

        destGPUBuffer.SetData(cpuBuffer, 0, destOffset, sourceBuffer.Length);

        return destOffset + sourceBuffer.Length;
    }

    private int CopyDataIntoGPUBuffer<TSource0Type, TSource1Type, TDestType>(ComputeBuffer destGPUBuffer, int destOffset, int destSize, TSource0Type[] source0Buffer, TSource1Type[] source1Buffer, System.Func<TSource0Type, TSource1Type, TDestType> funcParse) 
        where TSource0Type : struct
        where TSource1Type : struct
        where TDestType : struct
    {        
        var cpuBuffer = new TDestType[source0Buffer.Length];
        destGPUBuffer.GetData(cpuBuffer, 0, destOffset, source0Buffer.Length);

        // TODO: Will it be better to use Job System for making this multi-threaded???
        for (int i = 0; i < source0Buffer.Length; ++i)
        {                    
            cpuBuffer[i] = funcParse(source0Buffer[i], source1Buffer[i]);
        }

        destGPUBuffer.SetData(cpuBuffer, 0, destOffset, source0Buffer.Length);

        return destOffset + source0Buffer.Length;
    }  

    private int CopyDataIntoGPUBuffer<TSource0Type, TSource1Type, TSource2Type, TDestType>(ComputeBuffer destGPUBuffer, int destOffset, int destSize, TSource0Type[] source0Buffer, TSource1Type[] source1Buffer, TSource2Type[] source2Buffer, System.Func<TSource0Type, TSource1Type, TSource2Type, TDestType> funcParse) 
        where TSource0Type : struct
        where TSource1Type : struct
        where TSource2Type : struct
        where TDestType : struct
    {        
        var cpuBuffer = new TDestType[source0Buffer.Length];
        destGPUBuffer.GetData(cpuBuffer, 0, destOffset, source0Buffer.Length);

        // TODO: Will it be better to use Job System for making this multi-threaded???
        for (int i = 0; i < source0Buffer.Length; ++i)
        {                    
            cpuBuffer[i] = funcParse(source0Buffer[i], source1Buffer[i], source2Buffer[i]);
        }

        destGPUBuffer.SetData(cpuBuffer, 0, destOffset, source0Buffer.Length);

        return destOffset + source0Buffer.Length;
    }     

    private int CopySingleDataIntoGPUBuffer<TSource0Type, TSource1Type, TSource2Type, TSource3Type, TDestType>(ComputeBuffer destGPUBuffer, int destOffset, int destSize, TSource0Type source0, TSource1Type source1, TSource2Type source2, TSource3Type source3, System.Func<TSource0Type, TSource1Type, TSource2Type, TSource3Type, TDestType> funcParse) 
        where TSource0Type : struct
        where TSource1Type : struct
        where TSource2Type : struct
        where TDestType : struct
    {        
        var cpuBuffer = new TDestType[1];
        destGPUBuffer.GetData(cpuBuffer, 0, destOffset, 1);
        cpuBuffer[0] = funcParse(source0, source1, source2, source3);
        destGPUBuffer.SetData(cpuBuffer, 0, destOffset, 1);

        return destOffset + 1;
    }     

#else
    // TODO: It would be much much better if we can use Compute Shader to copy data into GPU buffer instead...
    private void ConstructGPUBuffersInGPU()
    {        
    }
#endif

    [NonSerialized] private List<InputData> m_InputDataList = new List<InputData>();

#if DRAWPROCEDURAL_MODE_CPU    
    [NonSerialized] private int m_VertexOffset = 0;
    [NonSerialized] private int m_VertexSize = 0;        
    [NonSerialized] private ComputeBuffer m_VertexGPUBuffer = null;
    [NonSerialized] private int m_IndexOffset = 0;
    [NonSerialized] private int m_IndexSize = 0;        
    [NonSerialized] private ComputeBuffer m_IndexGPUBuffer = null;
    [NonSerialized] private int m_InstanceOffset = 0;
    [NonSerialized] private int m_InstanceSize = 0;    
    [NonSerialized] private ComputeBuffer m_InstanceGPUBuffer = null;
    [NonSerialized] private int m_MaxIndexCount = 0;
#else
    // TODO: It would be much much better if we can use Compute Shader to copy data into GPU buffer instead...
#endif

    public struct InputData
    {
        public Mesh _Mesh;
        public Matrix4x4 _LocalToWorldMatrix;
    }

    private struct VertexData
    {
        public Vector3 _Position;
        public Vector3 _Normal;

        public static VertexData Parse(Vector3 position, Vector3 normal)
        {
            return new VertexData
            {
                _Position = position,
                _Normal = normal,
            };
        }
    }

    private struct IndexData
    {
        public int _Value;

        public static IndexData Parse(int index)
        {
            return new IndexData
            {
                _Value = index,
            };
        }        
    }

    private struct InstanceData
    {
        public Matrix4x4 _LocalToWorldMatrix;
        public int _VertexStart;
        public int _IndexStart;
        public int _IndexEnd;

        public static InstanceData Parse(Matrix4x4 localToWorldMatrix, int vertexStart, int indexStart, int indexEnd)
        {
            return new InstanceData
            {
                _LocalToWorldMatrix = localToWorldMatrix,
                _VertexStart = vertexStart,
                _IndexStart = indexStart,
                _IndexEnd = indexEnd,
            };
        }                
    }

    private const int MAX_GPU_BUFFER_SIZE = 3 * 1024 * 1024;

    private static class ShaderProperty
    {
        public static int VERTEX_BUFFER = Shader.PropertyToID("VERTEX_BUFFER");
        public static int INDEX_BUFFER = Shader.PropertyToID("INDEX_BUFFER");
        public static int INSTANCE_BUFFER = Shader.PropertyToID("INSTANCE_BUFFER");
    }
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
public static class PlayModeChangeListener
{
    static PlayModeChangeListener()
    {
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }    

    private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {        
        if (ExternalCallbacks == null)
        {
            return;
        }

        switch (state)
        {            
            case UnityEditor.PlayModeStateChange.ExitingEditMode:
            case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                ExternalCallbacks();
                break;
        }
    }

    public static System.Action ExternalCallbacks;
}
#endif