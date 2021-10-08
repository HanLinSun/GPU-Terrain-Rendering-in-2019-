using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Rendering;

//基于URP

namespace GPURemake{
    //TerrainAsset 
    [CreateAssetMenu(menuName = "GPUTerrain/TerrainAsset")]
    public class TerrainAsset : ScriptableObject
    {
        //Maximum Node count
        public const uint MAX_NODE_ID = 34124;
        public const int MAX_LOD = 5;
        public Shader BoundsShader;
        ///<summary>
        ///最大LOD层级为5
        ///</summary>
        public const int MAX_LOD_NODE_COUNT = 5;

        [SerializeField] private Vector3 _worldSize = new Vector3(10240, 2048, 10240);
        [SerializeField] private Texture2D _albedoMap;
        [SerializeField] private Texture2D _heightMap;
        [SerializeField] private Texture2D _normalMap;
        [SerializeField] private Texture2D[] _minMaxHeightMaps;
        [SerializeField] private Texture2D[] _quadTreeMaps;
        [SerializeField] private ComputeShader _terrainCompute;
        
        
        private RenderTexture _quadTreeMap;
        private RenderTexture _minMaxHeightMap;
        //bounds visible material
        private static Material _boundsDebugMaterial;

        public Vector3 WorldSize
        {
            get { return _worldSize; }
        }

        public Texture2D HeightMap
        {
            get { return _heightMap; }
        }

        public Texture2D NormalMap
        {
            get { return _normalMap; }
        }

        public Texture2D AlbedoMap
        {
            get { return _albedoMap; }
        }

        public RenderTexture QuadTreeMap
        {
            get
            {
                if (!_quadTreeMap)
                {
                    _quadTreeMap =
                        TextureUtility.CreateRenderTextureWithMipTextures(_quadTreeMaps, RenderTextureFormat.R16);
                }

                return _quadTreeMap;
            }
        }

        public RenderTexture MinMaxHeightMap
        {
            get
            {
                if (!_minMaxHeightMap)
                {
                    _minMaxHeightMap =
                        TextureUtility.CreateRenderTextureWithMipTextures(_minMaxHeightMaps, RenderTextureFormat.RG32);
                }

                return _minMaxHeightMap;
            }
        }

        public ComputeShader computeShader
        {
            get { return _terrainCompute; }
        }

        private static Mesh _patchMesh;
        
        public static Mesh patchMesh
        {
            get
            {
                if (!_patchMesh)
                    _patchMesh = MeshUtility.CreatePlaneMesh(16);
                return _patchMesh;
            }
        }

        //bounds可视化材料
        public  Material BoundsDebugMaterial
        {
            get
            {
                if (!_boundsDebugMaterial)
                {
                    //allocate the shader to target material
                    _boundsDebugMaterial=new Material(BoundsShader);
                }

                return _boundsDebugMaterial;
            }
        }

        private static Mesh _unitCubeMesh;

        public static Mesh unitCubeMesh
        {
            get
            {
                if (!_unitCubeMesh)
                {
                    _unitCubeMesh = MeshUtility.CreateCube(1);
                }
                return _unitCubeMesh;
            }
        }

    }
}
