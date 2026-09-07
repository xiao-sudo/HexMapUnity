using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapView : MonoBehaviour
    {
        [SerializeField] private HexMapConfigAsset config;
        [SerializeField] private HexOrientation orientation = HexOrientation.Pointy;
        [SerializeField] private HexPlane plane = HexPlane.XZ;
        [SerializeField] private float outerRadius = 1f;
        [SerializeField] private Vector3 origin;
        [SerializeField] private Material cellMaterial;
        [SerializeField] private int cellLayer;

        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private Transform generatedRoot;
        private Material fallbackMaterial;
        private RuntimeHexMap map;
        private HexLayout layout;

        public RuntimeHexMap Map
        {
            get { return map; }
        }

        public HexLayout Layout
        {
            get { return layout; }
        }

        public void Awake()
        {
            Build();
        }

        public void Build()
        {
            ClearGeneratedObjects();

            var definition = config == null
                ? new HexMapDefinition(
                    new HexMapBounds(-3, 3, -2, 2),
                    new HexCoord[0])
                : config.CreateDefinition();

            map = new RuntimeHexMap(definition);
            layout = new HexLayout(orientation, plane, outerRadius, origin);
            generatedRoot = new GameObject("Generated Hex Cells").transform;
            generatedRoot.SetParent(transform, false);

            foreach (var cell in map.Cells)
            {
                CreateCellObject(cell);
            }
        }

        public bool OwnsCollider(Collider collider)
        {
            return collider != null &&
                   generatedRoot != null &&
                   collider.transform.IsChildOf(generatedRoot);
        }

        private void CreateCellObject(HexCell cell)
        {
            var cellObject = new GameObject(cell.Coordinate.ToString());
            cellObject.layer = cellLayer;
            cellObject.transform.SetParent(generatedRoot, false);
            cellObject.transform.localPosition = layout.HexToWorld(cell.Coordinate);

            var filter = cellObject.AddComponent<MeshFilter>();
            var renderer = cellObject.AddComponent<MeshRenderer>();
            var collider = cellObject.AddComponent<MeshCollider>();
            var mesh = CreateCellMesh();

            filter.sharedMesh = mesh;
            collider.sharedMesh = mesh;

            var material = cellMaterial != null ? cellMaterial : GetFallbackMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            generatedObjects.Add(cellObject);
        }

        private Mesh CreateCellMesh()
        {
            var mesh = new Mesh();
            var vertices = new Vector3[7];
            var triangles = new int[18];
            vertices[0] = Vector3.zero;

            for (var index = 0; index < 6; index++)
            {
                var angle = (orientation == HexOrientation.Pointy ? 30f : 0f) + index * 60f;
                var radians = angle * Mathf.Deg2Rad;
                var x = Mathf.Cos(radians) * outerRadius;
                var secondary = Mathf.Sin(radians) * outerRadius;
                vertices[index + 1] = plane == HexPlane.XY
                    ? new Vector3(x, secondary, 0f)
                    : new Vector3(x, 0f, secondary);

                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                if (plane == HexPlane.XY)
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

            mesh.name = "Generated Hex Cell";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }

            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            fallbackMaterial = new Material(shader);
            fallbackMaterial.name = "Generated Hex Map Material";
            fallbackMaterial.color = new Color(0.2f, 0.65f, 0.9f, 1f);
            return fallbackMaterial;
        }

        private void ClearGeneratedObjects()
        {
            for (var index = 0; index < generatedObjects.Count; index++)
            {
                if (generatedObjects[index] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(generatedObjects[index]);
                }
                else
                {
                    DestroyImmediate(generatedObjects[index]);
                }
            }

            generatedObjects.Clear();

            if (generatedRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(generatedRoot.gameObject);
                }

                generatedRoot = null;
            }
        }

        private void OnDestroy()
        {
            if (fallbackMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(fallbackMaterial);
            }
            else
            {
                DestroyImmediate(fallbackMaterial);
            }

            fallbackMaterial = null;
        }
    }
}