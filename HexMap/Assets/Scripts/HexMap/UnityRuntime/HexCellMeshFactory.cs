using HexMap.Core;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    internal static class HexCellMeshFactory
    {
        public static Mesh CreateCellMesh(HexLayout layout)
        {
            var mesh = new Mesh { name = "Generated Hex Cell" };
            var vertices = new Vector3[7];
            var borderDistances = new Vector2[7];
            var normalizedPositions = new Vector2[7];
            var triangles = new int[18];
            vertices[0] = Vector3.zero;
            borderDistances[0] = Vector2.right;
            normalizedPositions[0] = Vector2.zero;

            for (var index = 0; index < 6; index++)
            {
                var angle = (layout.Orientation == HexOrientation.Pointy ? 30f : 0f) + index * 60f;
                var radians = angle * Mathf.Deg2Rad;
                var x = Mathf.Cos(radians) * layout.OuterRadius;
                var secondary = Mathf.Sin(radians) * layout.OuterRadius * layout.SecondaryScale;
                vertices[index + 1] = layout.Plane == HexPlane.XY
                    ? new Vector3(x, secondary, 0f)
                    : new Vector3(x, 0f, secondary);
                borderDistances[index + 1] = Vector2.zero;
                // Keep shader-space edge normals fixed for Pointy and Flat layouts.
                var normalizedRadians = index * 60f * Mathf.Deg2Rad;
                normalizedPositions[index + 1] =
                    new Vector2(Mathf.Cos(normalizedRadians), Mathf.Sin(normalizedRadians));

                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                if (layout.Plane == HexPlane.XY)
                {
                    triangles[triangleIndex + 1] = index + 1;
                    triangles[triangleIndex + 2] = index == 5 ? 1 : index + 2;
                }
                else
                {
                    triangles[triangleIndex + 1] = index == 5 ? 1 : index + 2;
                    triangles[triangleIndex + 2] = index + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = borderDistances;
            mesh.uv2 = normalizedPositions;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
