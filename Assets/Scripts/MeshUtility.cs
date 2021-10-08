using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPURemake
{
   public class MeshUtility 
   {
      //创建平面网格(16x16)
      //此部分已推导完毕
      public static Mesh CreatePlaneMesh(int size)
      {
         //size为格子数目
         //创建Patch网格,每个格子的宽度是0.5
         var SizePerGrid = 0.5f;
         var mesh = new Mesh();
         float totalMeterSize = size * SizePerGrid; //网格的实际尺寸
         //平面格子总数
         int gridCount = size * size;
         //一个格子被分成两个三角形，所以三角形总数是格子数x2
         var triangleCount = gridCount * 2;
         var vOffset = -totalMeterSize * 0.5f;
         //存放顶点(Vertex list)
         List<Vector3> vertices = new List<Vector3>();
         //UV List
         List<Vector2> uv = new List<Vector2>();
         float uvstrip = 1f / size;
         
         for (var z = 0; z <= size; z++)
         {
            for (var x = 0; x <= size; x++)
            {
               //保存网格中心点位置
               //顶点之间距离一个格子的长度
               vertices.Add(new Vector3(vOffset+x*SizePerGrid,0,vOffset+z*SizePerGrid));
               uv.Add(new Vector2(x*uvstrip,z*uvstrip));
            }
         }
         mesh.SetVertices(vertices);
         mesh.SetUVs(0,uv);
         
         //三角形顶点数目
         int[] indices = new int[triangleCount * 3];
         //一个格子两个三角形
         for (var gridIndex = 0; gridIndex < gridCount; gridIndex++)
         {
            var offset = gridIndex * 6;
            //vertexIndex(顶点索引)
            var vIndex = (gridIndex / size) * (size + 1) + (gridIndex % size);
            indices[offset] = vIndex;
            indices[offset + 1] = vIndex + size + 1;
            indices[offset + 2] = vIndex + 1;
            indices[offset + 3] = vIndex + 1;
            indices[offset + 4] = vIndex + size + 1;
            indices[offset + 5] = vIndex + size + 2;
         }
         //创建三角形网格
         mesh.SetIndices(indices,MeshTopology.Triangles,0);
         mesh.UploadMeshData(false);
         return mesh;
      }

      //参照cube
      public static Mesh CreateCube(float size)
      {
         var mesh = new Mesh();
         List<Vector3> vertices = new List<Vector3>();
         //中心点离边缘距离
         float extent = size * 0.5f;
         //Cude顶点位置
         vertices.Add(new Vector3(-extent,-extent,-extent));
         vertices.Add(new Vector3(-extent,extent,-extent));
         vertices.Add(new Vector3(extent,extent,-extent));
         vertices.Add(new Vector3(extent,-extent,-extent));
         
         vertices.Add(new Vector3(-extent,extent,extent));
         vertices.Add(new Vector3(extent,extent,extent));
         vertices.Add(new Vector3(extent,-extent,extent));
         vertices.Add(new Vector3(-extent,-extent,extent));

         int[] indices = new int[6 * 6];
         int[] triangles =
         {
            //face front
            0, 2, 1,
            0, 3, 2,
            //face top
            2, 3, 4,
            2, 4, 5,
            //face right
            1, 2, 5,
            1, 5, 6,
            //face left
            0, 4, 3,
            0, 7, 4,
            //face back
            5, 4, 7,
            5, 7, 6,
            //face bottom
            0, 6, 7,
            0, 1, 6
         };
         mesh.SetVertices(vertices);
         mesh.triangles = triangles;
         mesh.UploadMeshData(false);
         return mesh;
      }
      
      
   }
 
}
