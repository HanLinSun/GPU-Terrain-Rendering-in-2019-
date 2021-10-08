using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPURemake
{
    public class GPUTerrain : MonoBehaviour
    {
        //实际渲染
        public TerrainAsset _terrainAsset;
        [Range(0.1f, 1.9f)] 
        public float distanceEvaluation = 1.2f;

        [Range(0, 100)] public int boundsHeightRedundance = 5;
        public Shader TerrainShader;
        private TerrainBuilder _traverse;
        private Material _terrainMaterial;
        private bool _isTerrainMaterialDirty = true;
        public bool _enablePatchDebug=false;
        public bool NodeDebug = false;
        //后期开裁剪时使用
        public bool EnableFrustumCull = false;
        //包围盒可视化
        public bool _patchBoundsDebug = false;
        void Start()
        {
            _traverse = new TerrainBuilder(_terrainAsset);
            //设置着色shader中的structuredBufferList
           _terrainAsset.BoundsDebugMaterial.SetBuffer("BoundsList",_traverse.PatchBoundsBuffer);
            this.ApplySettings();
        }

        private void OnValidate()
        {
            this.ApplySettings();
        }
        //确保运行
        private Material EnsureTerrainMaterial(){
            if(!_terrainMaterial){
                var material = new Material(TerrainShader);
                material.SetTexture("_HeightMap",_terrainAsset.HeightMap);
                material.SetTexture("_NormalMap",_terrainAsset.NormalMap);
                material.SetTexture("_MainTex",_terrainAsset.AlbedoMap);
                material.SetBuffer("PatchList",_traverse.culledPatchBuffer);
                _terrainMaterial = material;
                this.UpdateTerrainMaterialProperties();
            }
            return _terrainMaterial;
        }

        private void ApplySettings()
        {
            if (_traverse != null)
            {
                _traverse.NodeEvalDistance = this.distanceEvaluation; 
                //_traverse.IsEnableFrustumCulled = this.EnableFrustumCull;
                //_traverse.IsBoundDebugEnabled = this._patchBoundsDebug;         
                _traverse.boundsHeightRedundance = this.boundsHeightRedundance;
            }
            _isTerrainMaterialDirty = true;
        }
        private void OnDestroy()
        {
            _traverse.Dispose();
        }

        private void UpdateTerrainMaterialProperties()
        {
            _isTerrainMaterialDirty = false;
            if (_terrainMaterial)
            {
                if (_enablePatchDebug)
                {
                    _terrainMaterial.EnableKeyword("ENABLE_PATCH_DEBUG");
                }
                else
                {
                    _terrainMaterial.DisableKeyword("ENABLE_PATCH_DEBUG");
                }

                if (NodeDebug)
                {
                    _terrainMaterial.EnableKeyword("ENABLE_NODE_DEBUG");
                }
                else
                {
                    _terrainMaterial.DisableKeyword("ENABLE_NODE_DEBUG");
                }
            }
            _terrainMaterial.SetVector("_WorldSize",_terrainAsset.WorldSize);
            _terrainMaterial.SetMatrix("_WorldNormalMapMatrix",Matrix4x4.Scale(this._terrainAsset.WorldSize).inverse);
        }
        void Update()
        {
            //实时参数计算启动
            _traverse.Dispatch();
            var terrainMaterial = this.EnsureTerrainMaterial();
            if (_isTerrainMaterialDirty)
            {
                this.UpdateTerrainMaterialProperties();
            }
            //真正的绘制(patchIndirectArgs来自CullPatch)
            //Node debug会发现节点并未被分割
            //25个Node被合并成了一个Patch
            Graphics.DrawMeshInstancedIndirect(TerrainAsset.patchMesh,0,terrainMaterial,new Bounds(Vector3.zero,Vector3.one*10240),_traverse.patchIndirectArgs);
            if (_patchBoundsDebug)
            {
                Graphics.DrawMeshInstancedIndirect(TerrainAsset.unitCubeMesh,0,_terrainAsset.BoundsDebugMaterial,new Bounds(Vector3.zero,Vector3.one*10240),_traverse.patchBoundIndirectArgs);
            }
        }
    }
    
}

