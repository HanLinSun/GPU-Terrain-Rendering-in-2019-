using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
namespace GPURemake
{
  public class TerrainBuilder:System.IDisposable
  {
    private ComputeShader _computeShader;
    //Node storage
    //Compute Buffer是存放数据的容器，这些数据会被传给GPU
    private ComputeBuffer _maxLODNodeList;
    private ComputeBuffer _NodeListA;
    private ComputeBuffer _NodeListB;
    private ComputeBuffer _finalNodeListBuffer;
    private ComputeBuffer _nodeDescriptors;
    //Patch相关（每个Patch都被细分，并且有一个包围盒）
    private ComputeBuffer _culledPatchBuffer;
    //调试信息用buffer(输出Evaluation判断系数)
    private ComputeBuffer _EvaluateDebug;
    private ComputeBuffer _NodePositionDebug;
    private ComputeBuffer _BoundsOutputDebug;
    private ComputeBuffer _IfCullEnabled;
    //
    private ComputeBuffer _patchIndirectArgs;
    //Bounding Box
    private ComputeBuffer _patchBoundsBuffer;
    private ComputeBuffer _patchBoundsIndirectArgs;
    private ComputeBuffer _indirectArgsBuffer;
    private RenderTexture _lodMap;
    
    //
    private const int PatchStripSize = 9 * 4;
    private Vector4 _nodeEvaluationC = new Vector4(1,0,0,0);
    
    private bool _isNodeEvaluationCDirty = true;
    private bool _isFrustumCullEnbaled;
    private bool _isBoundDebugEnabled;
    
    private TerrainAsset _asset;
    //计算命令表
    private CommandBuffer _commandBuffer = new CommandBuffer();
    //视锥裁剪缓存区
    //基本思路：对每个Patch创建AABB包围盒
    //摄像机视锥有6个平面，一个AABB包围盒有8个顶点
    //因此，只要将8个顶点和视锥体平面进行判定，若在视锥体外侧就证明在视锥之外
    //平面外侧 法线方向为负，内侧为正
    private Plane[] _cameraFrustumPlanes = new Plane[6];
    private Vector4[] _cameraFrustumPlanesV4 = new Vector4[6];
    
    //Compute Shader主入口
    private int _kernalOfTraverseQuadTree;
    private int _kernalOfBuildLodMap;
    private int _kernalOfBuildPatches;
    
    //Buffer的大小需要根据预估的最大分割情况进行分配
    private int _maxNodeBufferSize = 200;
    private int _tempNodeBufferSize = 50;

    
    //构造函数
    public TerrainBuilder(TerrainAsset asset)
    {
      _asset = asset;
      _computeShader = asset.computeShader;
      _commandBuffer.name = "TerrainBuild";
      
      //culledPatchBuffer存放最终的Patch索引值，绘制时把这个参数传给patchIndirectArgs
      _culledPatchBuffer = new ComputeBuffer(_maxNodeBufferSize * 64, PatchStripSize, ComputeBufferType.Append);
      _patchIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
      _patchIndirectArgs.SetData(new uint[]{TerrainAsset.patchMesh.GetIndexCount(0),0,0,0,0});
      
      //patch bounding box, used for clipping
      _patchBoundsIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
      _patchBoundsIndirectArgs.SetData(new uint[]{TerrainAsset.unitCubeMesh.GetIndexCount(0),0,0,0,0});
      _maxLODNodeList = new ComputeBuffer(TerrainAsset.MAX_LOD_NODE_COUNT * TerrainAsset.MAX_LOD_NODE_COUNT,8,ComputeBufferType.Append);
      this.InitMaxLODNodeListDatas();
      
      //Debug Output
      _EvaluateDebug = new ComputeBuffer(_maxNodeBufferSize, 8, ComputeBufferType.Append);
      _NodePositionDebug = new ComputeBuffer(_maxNodeBufferSize, 12, ComputeBufferType.Append);
      _IfCullEnabled = new ComputeBuffer(5, 4, ComputeBufferType.Append);
      //两个float3 一个float3占12位
      _BoundsOutputDebug = new ComputeBuffer(24, 12, ComputeBufferType.Append);
      
      
      _NodeListA = new ComputeBuffer(_tempNodeBufferSize,8,ComputeBufferType.Append);
      _NodeListB = new ComputeBuffer(_tempNodeBufferSize,8,ComputeBufferType.Append);
      //
      _indirectArgsBuffer = new ComputeBuffer(3,4,ComputeBufferType.IndirectArguments);
      _indirectArgsBuffer.SetData(new uint[]{1,1,1});
      
      _finalNodeListBuffer = new ComputeBuffer(_maxNodeBufferSize,12,ComputeBufferType.Append);
      _nodeDescriptors = new ComputeBuffer((int)(TerrainAsset.MAX_NODE_ID + 1),4);
      
      //一个Node有64个patch
      _patchBoundsBuffer = new ComputeBuffer(_maxNodeBufferSize * 64,4*10,ComputeBufferType.Append);
      //生成LOD Map
      _lodMap = TextureUtility.CreateLODMap(160);



      this.InitKernels();
      //世界参数
      this.InitWorldParams();
      
    }
    private void InitMaxLODNodeListDatas()
    {
      var maxLODNodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
      uint2[] datas = new uint2[maxLODNodeCount * maxLODNodeCount];
      var index = 0;
      for (uint i = 0; i < maxLODNodeCount; i++)
      {
        for (uint j = 0; j < maxLODNodeCount; j++)
        {
          datas[index] = new uint2(i, j);
          index++;
        }
      }
      _maxLODNodeList.SetData(datas);
    }

    private void InitKernels()
    {
      _kernalOfTraverseQuadTree = _computeShader.FindKernel("TraverseQuadTree");
      _kernalOfBuildLodMap = _computeShader.FindKernel("BuildLODMap");
      _kernalOfBuildPatches = _computeShader.FindKernel("BuildPatches");
      this.BindComputeShader(_kernalOfTraverseQuadTree);
      this.BindComputeShader(_kernalOfBuildPatches);
      this.BindComputeShader(_kernalOfBuildLodMap);
    }
    //按传入索引 给对应的ComputeBuffer赋值
    private void BindComputeShader(int kernalIndex)
    {
      _computeShader.SetTexture(kernalIndex,"QuadTreeTexture",_asset.QuadTreeMap);
      if (kernalIndex == _kernalOfTraverseQuadTree)
      {
        //当检测到四叉树kernal核，就把里面的数据按次序放入finalNodeList
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.AppendFinalNodeList,_finalNodeListBuffer);
        _computeShader.SetTexture(kernalIndex,"MinMaxHeightTexture",_asset.MinMaxHeightMap);
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.NodeDescriptors,_nodeDescriptors);
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.NodePositionDebug,_NodePositionDebug);
      }
      else if (kernalIndex == _kernalOfBuildPatches)
      {
        _computeShader.SetTexture(kernalIndex,ShaderConstants.LodMap,_lodMap);
        _computeShader.SetTexture(kernalIndex,"MinMaxHeightTexture",_asset.MinMaxHeightMap);
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.FinalNodeList,_finalNodeListBuffer);
        _computeShader.SetBuffer(kernalIndex,"CulledPatchList",_culledPatchBuffer);
        //传递至GPU
        _computeShader.SetBuffer(kernalIndex,"BoundsList",_patchBoundsBuffer);
        _computeShader.SetBuffer(kernalIndex,"BoundsOutputDebug",_BoundsOutputDebug);
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.EvaluateDebug,_EvaluateDebug);
        _computeShader.SetBuffer(kernalIndex,"IfCullEnabled",_IfCullEnabled);
      }
      else if (kernalIndex == _kernalOfBuildLodMap)
      {
        _computeShader.SetTexture(kernalIndex,ShaderConstants.LodMap,_lodMap);
        _computeShader.SetBuffer(kernalIndex,ShaderConstants.NodeDescriptors,_nodeDescriptors);
      }
    }
    //初始化世界参数
    private void InitWorldParams()
    {
      float wSize = _asset.WorldSize.x;
      int nodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
      //LOD 最大尺寸
      Vector4[] worldLODParams = new Vector4[TerrainAsset.MAX_LOD + 1];
      //Compute Shader: WorldParamsLOD
      for (var lod=TerrainAsset.MAX_LOD;lod>=0;lod--)
      {
        var nodeSize = wSize / nodeCount;
        var patchExtent = nodeSize / 16;
        //sectorCountPerNode:Node随Lod的放大指数
        //Node会随LOD层级变大而被放大
        var sectorCountPerNode = (int) Mathf.Pow(2, lod);
        worldLODParams[lod] = new Vector4(nodeSize, patchExtent, nodeCount, sectorCountPerNode);
        nodeCount *= 2;
      }
      _computeShader.SetVectorArray(ShaderConstants.WorldLodParams,worldLODParams);
      //(5+1)*4=24
      int[] nodeIDOffsetLOD = new int[(TerrainAsset.MAX_LOD + 1) * 4];
      int nodeIdOffset = 0;
      for (int lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
      {
        nodeIDOffsetLOD[lod * 4] = nodeIdOffset;
        nodeIdOffset += (int) (worldLODParams[lod].z * worldLODParams[lod].z);
      }
      _computeShader.SetInts("NodeIDOffsetOfLOD",nodeIDOffsetLOD);
    }

    public int boundsHeightRedundance
    {
      set
      {
        _computeShader.SetInt("_BoundsHeightRedundance",value);
      }
    }
    
    //获取ComputeShader中的常数，以在CS脚本中使用
    private class ShaderConstants
    {
      //要注意,PropertyToID在未找到对应变量时不会报错()
      //debug无法看到compute shader内信息
      //尽量手动输出debug信息定位问题
      
      public static readonly int WorldSize = Shader.PropertyToID("WorldSize");
      public static readonly int CameraPositionWS = Shader.PropertyToID("_CameraPositionWS");
      public static readonly int CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");
      public static readonly int PassLOD = Shader.PropertyToID("PassLOD");
      
      public static readonly int AppendFinalNodeList = Shader.PropertyToID("AppendFinalNodeList");
      public static readonly int FinalNodeList = Shader.PropertyToID("FinalNodeList");
      public static readonly int AppendNodeList = Shader.PropertyToID("AppendNodeList");
      
      public static readonly int ConsumeNodeList = Shader.PropertyToID("ConsumeNodeList");
      public static readonly int NodeEvaluationC = Shader.PropertyToID("NodeEvaluation");
      public static readonly int WorldLodParams = Shader.PropertyToID("WorldLodParams");
      
      public static readonly int NodeDescriptors = Shader.PropertyToID("NodeDescriptors");
      public static readonly int LodMap = Shader.PropertyToID("_LodMap");

      //Debug buffer绑定
      public static readonly int EvaluateDebug = Shader.PropertyToID("EvaluateDebug");
      public static readonly int NodePositionDebug = Shader.PropertyToID("NodePositionDebug");
      public static readonly int BoundsOutputDebug = Shader.PropertyToID("BoundsOutputDebug");
    }

    private void ClearBufferCounter(){
      //改成了单独设置每个compute Buffer的counter
      _maxLODNodeList.SetCounterValue((uint)_maxLODNodeList.count);
      _NodeListA.SetCounterValue(0);
      _NodeListB.SetCounterValue(0);
      _finalNodeListBuffer.SetCounterValue(0);
      _culledPatchBuffer.SetCounterValue(0);
      _patchBoundsBuffer.SetCounterValue(0);
      //Debug Buffer要不断重置，使其能一直读入更新信息
      _EvaluateDebug.SetCounterValue(0);
      _BoundsOutputDebug.SetCounterValue(0);
      _IfCullEnabled.SetCounterValue(0);
    }
  
    //视锥裁剪
    private void UpdateCameraFrustnumPlanes(Camera camera)
    {
      //plane[]作为参量,必须要转换成compute shader支持的float4格式
      GeometryUtility.CalculateFrustumPlanes(camera, _cameraFrustumPlanes);
      for (int i=0;i<_cameraFrustumPlanes.Length;i++)
      {
        //平面方程：ax+by+cz+d=0; 法向量(a,b,c),d代表原点->平面距离
        Vector4 v4 = _cameraFrustumPlanes[i].normal;
        v4.w = _cameraFrustumPlanes[i].distance;
        _cameraFrustumPlanesV4[i] = v4;
      }
      _computeShader.SetVectorArray(ShaderConstants.CameraFrustumPlanes,_cameraFrustumPlanesV4);
    }
    //查了API手册发现对Compute Shader的着色器变体支持是从2020版本开始的
    //2019版本，只能放弃控制着色器变体了
    //着色器变体启用
    /**
    public bool IsEnableFrustumCulled
    {
      set
      {
        if (value)
        {
          Debug.Log("裁剪已启用:"+value);
          _computeShader.EnableKeyword("ENABLE_FRUSTUM_CULL");
        }
        else
        {
          Debug.Log("裁剪已启用:"+value);
          _computeShader.DisableKeyword("ENABLE_FRUSTUM_CULL");
        }
      }
    }
    public bool IsBoundDebugEnabled
    {
      get
      {
        return _isBoundDebugEnabled;
      }
      set
      {
        if (value)
        {
          _computeShader.EnableKeyword("BOUNDS_DEBUG");
        }
        else
        {
          _computeShader.DisableKeyword("BOUNDS_DEBUG");
          _isBoundDebugEnabled = value;
        }
      }
    }
    **/
    
    //C#脚本中执行
    public void Dispatch()
    {
      //dispatch：实时参数设置及变更
      var camera = Camera.main;
      _commandBuffer.Clear();
      this.ClearBufferCounter();
      this.UpdateCameraFrustnumPlanes(camera);
      //参数传递至computeShader
      if (_isNodeEvaluationCDirty)
      {
        _isNodeEvaluationCDirty = false;
        _commandBuffer.SetComputeVectorParam(_computeShader,ShaderConstants.NodeEvaluationC,_nodeEvaluationC);
      }
      //摄像机世界坐标和世界大小向量传入ComputeShader
      _commandBuffer.SetComputeVectorParam(_computeShader,ShaderConstants.CameraPositionWS,camera.transform.position);
      _commandBuffer.SetComputeVectorParam(_computeShader,ShaderConstants.WorldSize,_asset.WorldSize);
      //初步计算PatchLOD(第一层LOD 5)
      _commandBuffer.CopyCounterValue(_maxLODNodeList,_indirectArgsBuffer,0);
      ComputeBuffer consumeNodeList = _NodeListA;
      ComputeBuffer appendNodeList = _NodeListB;
      //根据LOD分级，将对应层级的四叉树节点放入对应缓存区
      for (var lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
      {
        _commandBuffer.SetComputeIntParam(_computeShader,ShaderConstants.PassLOD,lod);
        if (lod == TerrainAsset.MAX_LOD) {
          _commandBuffer.SetComputeBufferParam(_computeShader,_kernalOfTraverseQuadTree,ShaderConstants.ConsumeNodeList,_maxLODNodeList);
        }
        else {
          _commandBuffer.SetComputeBufferParam(_computeShader,_kernalOfTraverseQuadTree,ShaderConstants.ConsumeNodeList,consumeNodeList);
        }
        _commandBuffer.SetComputeBufferParam(_computeShader,_kernalOfTraverseQuadTree,ShaderConstants.AppendNodeList,appendNodeList);
        //执行这个ComputeShader的kernal函数（四叉树）
        _commandBuffer.DispatchCompute(_computeShader,_kernalOfTraverseQuadTree,_indirectArgsBuffer,0);
        _commandBuffer.CopyCounterValue(appendNodeList,_indirectArgsBuffer,0);
        //交换buffer，分割过的子节点在下一轮LOD里进行分割
        var temp = consumeNodeList;
        consumeNodeList = appendNodeList;
        appendNodeList = temp;
      }
      //LOD Map(执行Compute Shader中对应的kernal函数)
      //执行Patch生成函数 
      //Debug输出区
      var data = new float2[10];
      _EvaluateDebug.GetData(data);
      Debug.Log(" Patch minmaxHeight 输出距离为:"+data[0]);
     
      var data2 = new float3[10];
      _NodePositionDebug.GetData(data2);
      Debug.Log("NodePosition 输出为:"+data2[0]);

      var data3 = new float3[10];
     _BoundsOutputDebug.GetData(data3);
      Debug.Log("BoundsMinPos 输出为:"+data3[0]);

      var data4 = new float[5];
      _IfCullEnabled.GetData(data4);
      Debug.Log("CullEnabled 输出为:"+data4[0]);
      
      _commandBuffer.CopyCounterValue(_finalNodeListBuffer,_indirectArgsBuffer,0);
      _commandBuffer.DispatchCompute(_computeShader,_kernalOfBuildPatches,_indirectArgsBuffer,0);
      //关键代码
      _commandBuffer.CopyCounterValue(_culledPatchBuffer,_patchIndirectArgs,4);
      Graphics.ExecuteCommandBuffer(_commandBuffer);
      
    }
    
    public ComputeBuffer patchIndirectArgs{
      get{
        return _patchIndirectArgs;
      }
    }

    public ComputeBuffer patchBoundIndirectArgs
    {
      get
      {
        return _patchBoundsIndirectArgs;
      }
    }
    public ComputeBuffer culledPatchBuffer{
      get{
        return _culledPatchBuffer;
      }
    }

    public ComputeBuffer PatchBoundsBuffer
    {
      get
      {
        return _patchBoundsBuffer;
      }
    }

    public float NodeEvalDistance
    {
      set
      {
        _nodeEvaluationC.x = value;
        _isNodeEvaluationCDirty = true;
      }
    }
    //继承自IDisposable
    //销毁buffer
    public void Dispose()
    {
      _culledPatchBuffer.Dispose();
      _patchIndirectArgs.Dispose();
      _patchBoundsBuffer.Dispose();
      _patchBoundsIndirectArgs.Dispose();
      _finalNodeListBuffer.Dispose();
      _maxLODNodeList.Dispose();
      _NodeListA.Dispose();
      _NodeListB.Dispose();
      _indirectArgsBuffer.Dispose();
      _nodeDescriptors.Dispose();
    }
 
  }

}

