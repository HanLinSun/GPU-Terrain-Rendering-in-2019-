#ifndef TERRAIN_COMMON_INPUT
#define TERRAIN_COMMON_INPUT

//把世界在xz平面上分割成相等的5x5
//世界大小10240x10240
//最大6层LOD
#define MAX_TERRAIN_LOD 5
//节点数目上限
//计算方式：25个LOD节点,每个节点都可能被四等分
//由于有6层LOD 因此一个节点最小可以被分到原先的(1/(2^5)^2)
//每一层的LOD分割出来的节点都要有唯一ID，所以每一层的分割节点数都要加到总数中
//(1+2^2+4^2+8^2+16^2+32^2)x25=34125
//ID起始下标为0，所以最大ID数为34125-1=34124
#define MAX_NODE_ID 34124

//基础渲染单位：Patch
//格子数目16x16 大小为8mx8m 每个格子分辨率0.5m
//Patch边长为8
#define PATCH_MESH_SIZE 8
//一个Node里有8x8个Patch
#define PATCH_COUNT_PER_NODE 8
//Patch网格数目:16x16
#define PATCH_MESH_GRID_COUMNT 16
//Patch分辨率（一个格子的大小为0.5x0.5）
//对LOD层级较高的node 按比例放大该patch即可
#define PATCH_MESH_GRID_SIZE 0.5
#define SECTOR_COUNT_WORLD 160

struct NodeDescriptor
{
    //branch代表节点是否已被分割
    //生成LOD Map会用到
    uint branch;
};

struct RenderPatch
{
    float2 position;
    float2 minMaxHeight;
    uint lod;
    uint4 lodTrans;
};

struct Bounds
{
    float3 minPosition;
    float3 maxPosition;
};

//Bounds可视化
struct BoundsDebug
{
    Bounds bounds;
    float4 color;
};


#endif