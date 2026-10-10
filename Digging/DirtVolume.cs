using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Foundry.Digging
{
    // The 3D dirt behind DiggableTerrain's Rounded mode: a grid of density samples through the whole block (1 = solid,
    // 0 = dug out), carved by spheres and rebuilt as a smooth closed surface with surface nets (one vertex per cell the
    // surface passes through, joined into quads across every edge it crosses). Split into chunks across X/Y so a bite
    // only rebuilds what it touched. Each chunk has a collider, and - if the material is the dirt see-through shader - a
    // "cap": a flat slice through the dirt at the play plane, shown only inside the see-through window, so the window
    // shows solid dirt where it's undug and the rounded tunnels where it's dug.
    internal class DirtVolume
    {
        private const int CHUNK = 16;
        private const float ISO = 0.5f;
        private const int MAX_Z_CELLS = 64;

        private class Chunk
        {
            public Mesh Mesh;
            public MeshCollider Collider;
            public Mesh CapMesh;
        }

        private readonly Transform _root;
        private readonly Vector3 _size;         // width, height, depth (local)
        private readonly float _planeZ;         // local Z of the play plane (where the cap slices)
        private readonly int _nx, _ny, _nz;
        private readonly float _cx, _cy, _cz;
        private readonly float[] _density;
        private readonly int _chunksX, _chunksY;
        private readonly Chunk[] _chunks;
        private readonly float _uvScale;
        private readonly Material _capMaterial;
        private readonly Material _bandMaterial;

        // Reused while meshing
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int> _triangles = new();
        private int[] _cellVertex = new int[0];
        private readonly Vector2[] _polygon = new Vector2[8];

        public int CellCount => _nx * _ny * _nz;

        public DirtVolume(Transform root, Vector2 size, float depth, float cellSize, float planeZ, Material material,
                          ShadowCastingMode castShadows, float uvScale)
        {
            _root = root;
            _size = new Vector3(size.x, size.y, depth);
            _planeZ = Mathf.Clamp(planeZ, -depth * 0.5f, depth * 0.5f);
            _uvScale = uvScale;

            _nx = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize));
            _ny = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize));
            _nz = Mathf.Clamp(Mathf.CeilToInt(depth / cellSize), 2, MAX_Z_CELLS);
            _cx = size.x / _nx;
            _cy = size.y / _ny;
            _cz = depth / _nz;

            _density = new float[(_nx + 1) * (_ny + 1) * (_nz + 1)];
            for (int i = 0; i < _density.Length; i++)
                _density[i] = 1f;

            // The cap needs the see-through shader (it shows only inside the window) - with any other material there's
            // no window, so no cap
            if (material != null && material.HasProperty("_SeeThroughCap"))
            {
                _capMaterial = new Material(material) { name = material.name + " (Cap)" };
                _capMaterial.SetFloat("_SeeThroughCap", 1f);
            }

            // The window's soft edge: the front dirt drawn again, transparent, fading out across the edge over the cap
            if (material != null && material.HasProperty("_SeeThroughBand"))
            {
                _bandMaterial = new Material(material) { name = material.name + " (Window Edge)" };
                _bandMaterial.SetFloat("_SeeThroughBand", 1f);
                _bandMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _bandMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _bandMaterial.SetFloat("_ZWrite", 0f);
                _bandMaterial.renderQueue = (int)RenderQueue.Transparent;
                _bandMaterial.SetShaderPassEnabled("ShadowCaster", false);
                _bandMaterial.SetShaderPassEnabled("DepthOnly", false);
                _bandMaterial.SetShaderPassEnabled("DepthNormals", false);
            }

            _chunksX = Mathf.CeilToInt((_nx + 2) / (float)CHUNK);
            _chunksY = Mathf.CeilToInt((_ny + 2) / (float)CHUNK);
            _chunks = new Chunk[_chunksX * _chunksY];
            CreateChunks(material, castShadows);
        }

        public void Dispose()
        {
            foreach (Chunk chunk in _chunks)
            {
                if (chunk == null) continue;
                if (chunk.Mesh != null) Object.Destroy(chunk.Mesh);
                if (chunk.CapMesh != null) Object.Destroy(chunk.CapMesh);
            }

            if (_capMaterial != null)
                Object.Destroy(_capMaterial);
            if (_bandMaterial != null)
                Object.Destroy(_bandMaterial);
        }

        #region Density

        private int Index(int i, int j, int k) => (k * (_ny + 1) + j) * (_nx + 1) + i;

        // Outside the grid counts as empty, so the block's outside is a closed surface
        private float Sample(int i, int j, int k)
        {
            if (i < 0 || j < 0 || k < 0 || i > _nx || j > _ny || k > _nz)
                return 0f;
            return _density[Index(i, j, k)];
        }

        private Vector3 Corner(int i, int j, int k) => new(-_size.x * 0.5f + i * _cx, -_size.y * 0.5f + j * _cy, -_size.z * 0.5f + k * _cz);

        // Trilinear density at a local point (0 outside)
        public float DensityAt(Vector3 local)
        {
            float gx = (local.x + _size.x * 0.5f) / _cx;
            float gy = (local.y + _size.y * 0.5f) / _cy;
            float gz = (local.z + _size.z * 0.5f) / _cz;
            int i = Mathf.FloorToInt(gx), j = Mathf.FloorToInt(gy), k = Mathf.FloorToInt(gz);
            float tx = gx - i, ty = gy - j, tz = gz - k;

            float c00 = Mathf.Lerp(Sample(i, j, k), Sample(i + 1, j, k), tx);
            float c10 = Mathf.Lerp(Sample(i, j + 1, k), Sample(i + 1, j + 1, k), tx);
            float c01 = Mathf.Lerp(Sample(i, j, k + 1), Sample(i + 1, j, k + 1), tx);
            float c11 = Mathf.Lerp(Sample(i, j + 1, k + 1), Sample(i + 1, j + 1, k + 1), tx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, ty), Mathf.Lerp(c01, c11, ty), tz);
        }

        // Carves a soft sphere (full strength inside half the radius, easing to nothing at the rim). Returns whether
        // anything changed, and the X/Y range of corners that did.
        public bool Carve(Vector3 local, float radius, float strength, out RectInt changed)
        {
            int iMin = Mathf.Max(0, Mathf.FloorToInt((local.x - radius + _size.x * 0.5f) / _cx));
            int iMax = Mathf.Min(_nx, Mathf.CeilToInt((local.x + radius + _size.x * 0.5f) / _cx));
            int jMin = Mathf.Max(0, Mathf.FloorToInt((local.y - radius + _size.y * 0.5f) / _cy));
            int jMax = Mathf.Min(_ny, Mathf.CeilToInt((local.y + radius + _size.y * 0.5f) / _cy));
            int kMin = Mathf.Max(0, Mathf.FloorToInt((local.z - radius + _size.z * 0.5f) / _cz));
            int kMax = Mathf.Min(_nz, Mathf.CeilToInt((local.z + radius + _size.z * 0.5f) / _cz));
            changed = new RectInt(iMin, jMin, iMax - iMin, jMax - jMin);

            bool any = false;
            float core = radius * 0.5f;
            for (int k = kMin; k <= kMax; k++)
            for (int j = jMin; j <= jMax; j++)
            for (int i = iMin; i <= iMax; i++)
            {
                float distance = Vector3.Distance(Corner(i, j, k), local);
                if (distance >= radius)
                    continue;

                float amount = strength * (1f - Mathf.SmoothStep(0f, 1f, (distance - core) / (radius - core)));
                int index = Index(i, j, k);
                float before = _density[index];
                _density[index] = Mathf.Max(0f, before - amount);
                any |= _density[index] != before;
            }

            return any;
        }

        #endregion

        #region Chunks

        // Chunk c owns edge/cell start indices from c * CHUNK - 1 (the grid runs -1..n, one empty ring outside)
        private void CreateChunks(Material material, ShadowCastingMode castShadows)
        {
            for (int cy = 0; cy < _chunksY; cy++)
            for (int cx = 0; cx < _chunksX; cx++)
            {
                GameObject chunkObject = new($"Dirt Chunk {cx},{cy}") { layer = _root.gameObject.layer };
                chunkObject.transform.SetParent(_root, false);

                Chunk chunk = new() { Mesh = new Mesh { name = chunkObject.name } };
                chunk.Mesh.MarkDynamic();
                chunkObject.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
                MeshRenderer renderer = chunkObject.AddComponent<MeshRenderer>();
                // A second material on a one-submesh renderer draws the same mesh again - the window's soft edge
                renderer.sharedMaterials = _bandMaterial != null ? new[] { material, _bandMaterial } : new[] { material };
                renderer.shadowCastingMode = castShadows;
                chunk.Collider = chunkObject.AddComponent<MeshCollider>();

                if (_capMaterial != null)
                {
                    GameObject capObject = new("Cap") { layer = _root.gameObject.layer };
                    capObject.transform.SetParent(chunkObject.transform, false);
                    chunk.CapMesh = new Mesh { name = chunkObject.name + " Cap" };
                    chunk.CapMesh.MarkDynamic();
                    capObject.AddComponent<MeshFilter>().sharedMesh = chunk.CapMesh;
                    MeshRenderer capRenderer = capObject.AddComponent<MeshRenderer>();
                    capRenderer.sharedMaterial = _capMaterial;
                    capRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                _chunks[cy * _chunksX + cx] = chunk;
            }
        }

        public void BuildAll()
        {
            for (int c = 0; c < _chunks.Length; c++)
                RebuildChunk(c % _chunksX, c / _chunksX);
        }

        // A changed corner moves the vertices of the cells around it, which feed quads one edge further out
        public void RebuildAround(RectInt corners)
        {
            int minX = Mathf.Clamp((corners.xMin - 2 + 1) / CHUNK, 0, _chunksX - 1);
            int maxX = Mathf.Clamp((corners.xMax + 1 + 1) / CHUNK, 0, _chunksX - 1);
            int minY = Mathf.Clamp((corners.yMin - 2 + 1) / CHUNK, 0, _chunksY - 1);
            int maxY = Mathf.Clamp((corners.yMax + 1 + 1) / CHUNK, 0, _chunksY - 1);

            for (int cy = minY; cy <= maxY; cy++)
            for (int cx = minX; cx <= maxX; cx++)
                RebuildChunk(cx, cy);
        }

        private void RebuildChunk(int chunkX, int chunkY)
        {
            Chunk chunk = _chunks[chunkY * _chunksX + chunkX];
            int x0 = chunkX * CHUNK - 1, x1 = Mathf.Min(x0 + CHUNK - 1, _nx);
            int y0 = chunkY * CHUNK - 1, y1 = Mathf.Min(y0 + CHUNK - 1, _ny);

            BuildSurface(x0, x1, y0, y1);
            Apply(chunk.Mesh);

            bool hasMesh = _vertices.Count > 0;
            chunk.Collider.sharedMesh = null;
            chunk.Collider.enabled = hasMesh;
            if (hasMesh)
                chunk.Collider.sharedMesh = chunk.Mesh;

            if (chunk.CapMesh != null)
            {
                BuildCap(x0, x1, y0, y1);
                Apply(chunk.CapMesh);
            }
        }

        private void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
        }

        #endregion

        #region Surface nets

        private static readonly Vector3Int[] CubeCorners =
        {
            new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1)
        };

        // The 12 edges of a cell, as pairs of CubeCorners indices
        private static readonly int[] CubeEdges =
        {
            0, 1, 2, 3, 4, 5, 6, 7,     // along X
            0, 2, 1, 3, 4, 6, 5, 7,     // along Y
            0, 4, 1, 5, 2, 6, 3, 7      // along Z
        };

        private readonly float[] _cornerDensity = new float[8];

        private void BuildSurface(int x0, int x1, int y0, int y1)
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();

            // Vertices for every cell the chunk's quads can touch: one cell further back in X and Y, all of Z
            int vx0 = x0 - 1, vy0 = y0 - 1, vz0 = -1;
            int sizeX = x1 - vx0 + 1, sizeY = y1 - vy0 + 1, sizeZ = _nz + 2;
            int cellCount = sizeX * sizeY * sizeZ;
            if (_cellVertex.Length < cellCount)
                _cellVertex = new int[cellCount];

            for (int k = vz0; k <= _nz; k++)
            for (int j = vy0; j <= y1; j++)
            for (int i = vx0; i <= x1; i++)
            {
                int slot = ((k - vz0) * sizeY + (j - vy0)) * sizeX + (i - vx0);
                _cellVertex[slot] = MakeCellVertex(i, j, k);
            }

            int VertexOf(int i, int j, int k)
            {
                if (i < vx0 || j < vy0 || k < vz0 || i > x1 || j > y1 || k > _nz) return -1;
                return _cellVertex[((k - vz0) * sizeY + (j - vy0)) * sizeX + (i - vx0)];
            }

            // A quad across every edge the surface crosses, joining the four cells around it
            for (int k = -1; k <= _nz; k++)
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float d0 = Sample(i, j, k);
                bool solid0 = d0 >= ISO;

                if (solid0 != Sample(i + 1, j, k) >= ISO)
                    AddQuad(VertexOf(i, j - 1, k - 1), VertexOf(i, j, k - 1), VertexOf(i, j, k), VertexOf(i, j - 1, k), Vector3.right, solid0);

                if (solid0 != Sample(i, j + 1, k) >= ISO)
                    AddQuad(VertexOf(i - 1, j, k - 1), VertexOf(i, j, k - 1), VertexOf(i, j, k), VertexOf(i - 1, j, k), Vector3.up, solid0);

                if (solid0 != Sample(i, j, k + 1) >= ISO)
                    AddQuad(VertexOf(i - 1, j - 1, k), VertexOf(i, j - 1, k), VertexOf(i, j, k), VertexOf(i - 1, j, k), Vector3.forward, solid0);
            }
        }

        // The cell's surface vertex: the average of where the surface crosses its edges (-1 if it doesn't pass through)
        private int MakeCellVertex(int i, int j, int k)
        {
            int solidCount = 0;
            for (int c = 0; c < 8; c++)
            {
                Vector3Int o = CubeCorners[c];
                _cornerDensity[c] = Sample(i + o.x, j + o.y, k + o.z);
                if (_cornerDensity[c] >= ISO) solidCount++;
            }

            if (solidCount == 0 || solidCount == 8)
                return -1;

            Vector3 sum = Vector3.zero;
            int crossings = 0;
            for (int e = 0; e < CubeEdges.Length; e += 2)
            {
                int a = CubeEdges[e], b = CubeEdges[e + 1];
                float da = _cornerDensity[a], db = _cornerDensity[b];
                if (da >= ISO == db >= ISO)
                    continue;

                float t = (ISO - da) / (db - da);
                Vector3Int oa = CubeCorners[a], ob = CubeCorners[b];
                sum += Vector3.Lerp(Corner(i + oa.x, j + oa.y, k + oa.z), Corner(i + ob.x, j + ob.y, k + ob.z), t);
                crossings++;
            }

            Vector3 position = sum / crossings;
            Vector3 normal = SurfaceNormal(position);

            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(Uv(position, normal));
            return _vertices.Count - 1;
        }

        // Pointing out of the dirt: down the density gradient
        private Vector3 SurfaceNormal(Vector3 position)
        {
            float h = Mathf.Min(_cx, Mathf.Min(_cy, _cz)) * 0.5f;
            Vector3 gradient = new(
                DensityAt(position + new Vector3(h, 0f, 0f)) - DensityAt(position - new Vector3(h, 0f, 0f)),
                DensityAt(position + new Vector3(0f, h, 0f)) - DensityAt(position - new Vector3(0f, h, 0f)),
                DensityAt(position + new Vector3(0f, 0f, h)) - DensityAt(position - new Vector3(0f, 0f, h)));

            return gradient.sqrMagnitude > 1e-10f ? (-gradient).normalized : Vector3.back;
        }

        // Projected along whichever axis the surface mostly faces, in world metres
        private Vector2 Uv(Vector3 local, Vector3 normal)
        {
            Vector3 world = local + _root.position;
            Vector3 n = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Vector2 uv = n.z >= n.x && n.z >= n.y ? new Vector2(world.x, world.y)
                : n.x >= n.y ? new Vector2(world.z, world.y)
                : new Vector2(world.x, world.z);
            return uv * _uvScale;
        }

        // Two triangles, wound to face out of the dirt (from the solid end of the edge toward the empty end)
        private void AddQuad(int a, int b, int c, int d, Vector3 edgeAxis, bool startSolid)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0)
                return;

            Vector3 outward = startSolid ? edgeAxis : -edgeAxis;
            Vector3 facing = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
            if (Vector3.Dot(facing, outward) < 0f)
                (b, d) = (d, b);

            _triangles.Add(a); _triangles.Add(b); _triangles.Add(c);
            _triangles.Add(a); _triangles.Add(c); _triangles.Add(d);
        }

        #endregion

        #region Cap

        // The slice through the dirt at the play plane, as flat polygons facing the camera (-Z): marching squares on the
        // density interpolated between the two Z layers either side of the plane
        private void BuildCap(int x0, int x1, int y0, int y1)
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();

            float layer = (_planeZ + _size.z * 0.5f) / _cz;
            int k = Mathf.Clamp(Mathf.FloorToInt(layer), 0, _nz - 1);
            float t = Mathf.Clamp01(layer - k);

            float Slice(int i, int j) => Mathf.Lerp(Sample(i, j, k), Sample(i, j, k + 1), t);
            Vector2 Point(int i, int j) => new(-_size.x * 0.5f + i * _cx, -_size.y * 0.5f + j * _cy);

            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float d0 = Slice(i, j), d1 = Slice(i + 1, j), d2 = Slice(i + 1, j + 1), d3 = Slice(i, j + 1);
                if (d0 < ISO && d1 < ISO && d2 < ISO && d3 < ISO)
                    continue;

                int count = 0;
                AddCapEdge(Point(i, j), d0, Point(i + 1, j), d1, ref count);
                AddCapEdge(Point(i + 1, j), d1, Point(i + 1, j + 1), d2, ref count);
                AddCapEdge(Point(i + 1, j + 1), d2, Point(i, j + 1), d3, ref count);
                AddCapEdge(Point(i, j + 1), d3, Point(i, j), d0, ref count);
                if (count < 3)
                    continue;

                int start = _vertices.Count;
                for (int m = 0; m < count; m++)
                {
                    Vector3 position = new(_polygon[m].x, _polygon[m].y, _planeZ);
                    _vertices.Add(position);
                    _normals.Add(Vector3.back);
                    _uvs.Add(Uv(position, Vector3.back));
                }

                // Counter-clockwise polygon in X/Y, fanned to face -Z
                for (int m = 1; m < count - 1; m++)
                {
                    _triangles.Add(start); _triangles.Add(start + m + 1); _triangles.Add(start + m);
                }
            }
        }

        private void AddCapEdge(Vector2 a, float da, Vector2 b, float db, ref int count)
        {
            bool solidA = da >= ISO, solidB = db >= ISO;
            if (solidA)
                _polygon[count++] = a;

            if (solidA != solidB)
                _polygon[count++] = Vector2.Lerp(a, b, (ISO - da) / (db - da));
        }

        #endregion
    }
}
