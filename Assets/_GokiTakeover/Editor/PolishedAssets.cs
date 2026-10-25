using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover.Editor
{
    internal static class PolishedAssets
    {
        internal const string Root = GeneratedAssets.Root + "/Art/Polished";
        internal static Mesh BoxMesh, SphereMesh, RodMesh, ShadowMesh;
        internal static Texture2D WoodTexture, FloorTexture, ClothTexture;

        internal static void Prepare()
        {
            GeneratedAssets.Folder(Root + "/Meshes");
            GeneratedAssets.Folder(Root + "/Materials");
            GeneratedAssets.Folder(Root + "/Textures");
            BoxMesh = RoundedBox();
            SphereMesh = Sphere();
            RodMesh = Rod();
            ShadowMesh = Plane();
            WoodTexture = Texture("WalnutGrain", (x, y) =>
            {
                float warp = Mathf.PerlinNoise(x * 4f, y * 3f) * 0.20f;
                float grain = Mathf.Sin((x + warp) * 135f) * 0.045f + Mathf.PerlinNoise(x * 60, y * 8) * 0.09f;
                float knot = Mathf.PerlinNoise(x * 5, y * 2) * 0.09f;
                return new Color(0.63f + grain + knot, 0.49f + grain + knot, 0.34f + grain + knot);
            });
            FloorTexture = Texture("StoneTiles", (x, y) =>
            {
                float u = Mathf.Repeat(x * 2, 1), v = Mathf.Repeat(y * 2, 1);
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float seam = Mathf.SmoothStep(0, 1, Mathf.Clamp01((edge - 0.008f) / 0.012f));
                float noise = Mathf.PerlinNoise(x * 130, y * 130) * 0.045f + Mathf.PerlinNoise(x * 22, y * 22) * 0.025f;
                float variation = ((int)(x * 2) + (int)(y * 2)) % 2 * 0.018f;
                return Color.Lerp(new Color(0.19f, 0.185f, 0.165f), new Color(0.55f + noise + variation, 0.52f + noise + variation, 0.45f + noise + variation), seam);
            });
            ClothTexture = Texture("WovenCheck", (x, y) =>
            {
                float check = ((int)(x * 12) % 2 + (int)(y * 12) % 2) * 0.27f;
                float weave = Mathf.Sin(x * 800) * Mathf.Sin(y * 800) * 0.025f;
                return Color.Lerp(new Color(0.80f, 0.71f, 0.56f), new Color(0.43f, 0.09f, 0.055f), check) * (0.96f + weave);
            });
        }

        internal static Material Material(string name, Color color, float smoothness = 0.2f, float metallic = 0,
            Texture2D texture = null, Vector2? tiling = null, Color? emission = null)
        {
            string path = Root + "/Materials/" + name + ".mat";
            GeneratedAssets.RequireOwned(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
                GeneratedAssets.Mark(material);
            }
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.mainTexture = texture;
            material.mainTextureScale = tiling ?? Vector2.one;
            material.enableInstancing = true;
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Material ShadowMaterial(string name, float opacity)
        {
            string path = Root + "/Materials/" + name + ".mat";
            GeneratedAssets.RequireOwned(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("GokiTakeover/Contact Shadow");
            if (!shader) throw new InvalidOperationException("[GOKI] ContactShadow.shaderが読み込まれていません。");
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
                GeneratedAssets.Mark(material);
            }
            material.color = new Color(0.018f, 0.012f, 0.008f, opacity);
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale,
            bool collider = false, bool castShadow = true)
        {
            var part = GeneratedAssets.Part(name, parent, mesh, material, position, scale);
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (collider) part.gameObject.AddComponent<BoxCollider>();
            return part;
        }
        internal static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
            => Part(name, parent, BoxMesh, material, position, size, collider);
        internal static Transform Sphere(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => Part(name, parent, SphereMesh, material, position, size);
        internal static Transform Rod(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material)
        {
            Transform part = Part(name, parent, RodMesh, material, (start + end) * 0.5f,
                new Vector3(width, (end - start).magnitude * 0.5f, width));
            part.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            return part;
        }
        internal static void Shadow(string name, Transform parent, Vector3 position, Vector2 size, Material material)
            => Part(name, parent, ShadowMesh, material, position, new Vector3(size.x, 1, size.y), false, false);

        internal static Mesh Lathe(string name, Vector2[] profile, int sides = 20)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
            for (int side = 0; side <= sides; side++)
            {
                float angle = side * Mathf.PI * 2 / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * profile[ring].x, profile[ring].y, Mathf.Sin(angle) * profile[ring].x));
                uv.Add(new Vector2((float)side / sides, (float)ring / (profile.Length - 1)));
            }
            for (int ring = 0; ring < profile.Length - 1; ring++)
            for (int side = 0; side < sides; side++)
            {
                int a = ring * (sides + 1) + side;
                triangles.AddRange(new[] { a, a + sides + 1, a + 1, a + 1, a + sides + 1, a + sides + 2 });
            }
            return SaveMesh(name, vertices, null, uv, triangles);
        }

        internal static Vector3 WingSurface(int side, float across, float along)
        {
            float width = 0.39f * Mathf.Sin(Mathf.PI * (0.06f + along * 0.88f));
            return new Vector3(side * (0.006f + across * width),
                0.35f + 0.13f * Mathf.Sin(Mathf.PI * along) * Mathf.Sqrt(Mathf.Max(0,1-across*across)),
                -0.80f + along * 1.25f);
        }

        internal static Mesh Wing(int side)
        {
            const int rows=12, columns=4, rowSize=columns+1, layerSize=(rows+1)*rowSize;
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            for(int layer=0;layer<2;layer++)
            for(int row=0;row<=rows;row++)
            for(int column=0;column<=columns;column++)
            {
                float u=(float)column/columns, v=(float)row/rows;
                Vector3 point=WingSurface(side,u,v);
                if(layer==1) point.y=0.31f;
                vertices.Add(point); uv.Add(new Vector2(u,v));
            }
            Action<int,int,int> tri=(a,b,c) =>
            {
                triangles.Add(a); triangles.Add(side>0?b:c); triangles.Add(side>0?c:b);
            };
            for(int row=0;row<rows;row++)
            for(int column=0;column<columns;column++)
            {
                int a=row*rowSize+column,b=a+rowSize;
                tri(a,b,a+1);tri(a+1,b,b+1);
                tri(a+layerSize,a+1+layerSize,b+layerSize);tri(a+1+layerSize,b+1+layerSize,b+layerSize);
            }
            for(int row=0;row<rows;row++)
            {
                int a=row*rowSize,b=a+rowSize;
                tri(a,a+layerSize,b);tri(b,a+layerSize,b+layerSize);
                a+=columns;b+=columns;
                tri(a,b,a+layerSize);tri(b,b+layerSize,a+layerSize);
            }
            for(int column=0;column<columns;column++)
            {
                int a=column;
                tri(a,a+1,a+layerSize);tri(a+1,a+1+layerSize,a+layerSize);
                a=rows*rowSize+column;
                tri(a,a+layerSize,a+1);tri(a+1,a+layerSize,a+1+layerSize);
            }
            return SaveMesh(side<0?"WingLeft":"WingRight",vertices,null,uv,triangles);
        }

        private static Texture2D Texture(string name, Func<float, float, Color> sample)
        {
            const int size = 256;
            string path = Root + "/Textures/" + name + ".asset";
            GeneratedAssets.RequireOwned(path);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) colors[y * size + x] = sample((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels(colors);
            texture.Apply(true, false);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(texture, existing);
                UnityEngine.Object.DestroyImmediate(texture);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(texture, path);
            GeneratedAssets.Mark(texture);
            return texture;
        }

        private static Mesh RoundedBox()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            float[] step = { -0.5f, -0.44f, 0.44f, 0.5f };
            foreach (Vector3 normal in faceNormals)
            {
                Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                int offset = vertices.Count;
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Vector3 point = normal * 0.5f + u * step[x] + v * step[y];
                    Vector3 inner = new Vector3(Mathf.Clamp(point.x, -0.44f, 0.44f), Mathf.Clamp(point.y, -0.44f, 0.44f), Mathf.Clamp(point.z, -0.44f, 0.44f));
                    Vector3 n = (point - inner).normalized;
                    vertices.Add(inner + n * 0.06f);
                    normals.Add(n);
                    uv.Add(new Vector2(step[x] + 0.5f, step[y] + 0.5f));
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int a = offset + y * 4 + x;
                    triangles.AddRange(new[] { a, a + 1, a + 4, a + 1, a + 5, a + 4 });
                }
            }
            return SaveMesh("BeveledBox", vertices, normals, uv, triangles);
        }
        private static Mesh Sphere()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            const int rings = 8, sides = 16;
            for (int y = 0; y <= rings; y++)
            for (int x = 0; x <= sides; x++)
            {
                float a = Mathf.PI * y / rings, b = Mathf.PI * 2 * x / sides;
                Vector3 n = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                vertices.Add(n * 0.5f); normals.Add(n); uv.Add(new Vector2((float)x / sides, (float)y / rings));
            }
            for (int y = 0; y < rings; y++)
            for (int x = 0; x < sides; x++)
            {
                int a = y * (sides + 1) + x;
                if (y > 0) triangles.AddRange(new[] { a, a + 1, a + sides + 1 });
                if (y < rings - 1) triangles.AddRange(new[] { a + 1, a + sides + 2, a + sides + 1 });
            }
            return SaveMesh("SmoothLowPolySphere", vertices, normals, uv, triangles);
        }
        private static Mesh Rod()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.5f, -1, Mathf.Sin(a) * 0.5f);
                Vector3 q = new Vector3(Mathf.Cos(b) * 0.5f, -1, Mathf.Sin(b) * 0.5f);
                Vector3 r = p + Vector3.up * 2, s = q + Vector3.up * 2;
                vertices.AddRange(new[] { p, r, q, q, r, s, Vector3.down, p, q, Vector3.up, s, r });
            }
            for (int i = 0; i < vertices.Count; i++) triangles.Add(i);
            return SaveMesh("EightSidedRod", vertices, null, null, triangles);
        }
        private static Mesh Plane()
        {
            return SaveMesh("ContactPlane", new List<Vector3> { new Vector3(-0.5f,0,-0.5f), new Vector3(-0.5f,0,0.5f), new Vector3(0.5f,0,0.5f), new Vector3(0.5f,0,-0.5f) },
                null, new List<Vector2> { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, new List<int> { 0, 1, 2, 0, 2, 3 });
        }
        private static Mesh SaveMesh(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> indices)
        {
            string path = Root + "/Meshes/" + name + ".asset";
            GeneratedAssets.RequireOwned(path);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
            if (uv != null) { mesh.SetUVs(0, uv); mesh.RecalculateTangents(); }
            mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            GeneratedAssets.Mark(mesh);
            return mesh;
        }
    }
}
