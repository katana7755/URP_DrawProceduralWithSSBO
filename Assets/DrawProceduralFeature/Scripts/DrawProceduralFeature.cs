using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DrawProceduralFeature : ScriptableRendererFeature
{
    [SerializeField] private RenderPassEvent _RenderPassEvent;
    [SerializeField] private DrawProceduralTargetDB _Database;

    public override void Create()
    {
        m_RenderPass = new DrawProceduralPass(_Database, name);
        m_RenderPass.renderPassEvent = _RenderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_RenderPass);
    }

    private DrawProceduralPass m_RenderPass;

#region Render Pass Definitions
    class DrawProceduralPass : ScriptableRenderPass
    {
        public DrawProceduralPass(DrawProceduralTargetDB database, string profilingTag)
        {    
            m_Database = database;        
            m_ProfilingTag = profilingTag;
            m_ProfilingSampler = new ProfilingSampler(profilingTag);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Database == null)
            {
                return;
            }

            CommandBuffer cmdBuf = CommandBufferPool.Get(m_ProfilingTag);

            using (new ProfilingScope(cmdBuf, m_ProfilingSampler))
            {                
                m_Database.ConstructGPUBuffers();
                m_Database.BindAllGPUResources();
                m_Database.Render(cmdBuf);
            }

            context.ExecuteCommandBuffer(cmdBuf);
            CommandBufferPool.Release(cmdBuf);
        }

        private DrawProceduralTargetDB m_Database;
        private string m_ProfilingTag;
        private ProfilingSampler m_ProfilingSampler;
    }
#endregion
}


