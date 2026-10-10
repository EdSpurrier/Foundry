using System;
using System.Collections.Generic;
using Foundry.Data;
using Foundry.Interaction;
using FrameCoreU.Events;
using FrameCoreU.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Foundry.Digging
{
    // A block of dirt (or clay, sand, snow...) that can be dug through freely. Striking it (e.g. the chicken pecking it)
    // bites a hole out where it's hit, so tunnels and pathways form wherever the player digs, and anything buried in
    // it (see BuriedObject) is uncovered. Two modes:
    //  - Through: a grid of density samples in local X/Y, rebuilt as a smooth-edged mesh (marching squares) extruded
    //    Depth along Z - each bite cuts straight through the block (a see-through hole). Cheap; good for thin things.
    //  - Rounded: a 3D grid (DirtVolume) - each bite scoops a sphere, so tunnels are rounded hollows inside the dirt.
    //    The dirt stays in front of the chicken; give it a material using the "Foundry/Dirt (See-Through)" shader and
    //    put a SeeThroughWindow on the player to see the chicken (and its tunnels) through a window in the dirt.
    // Either way it has a matching collider to walk on, split into chunks so a bite only rebuilds what it touched.
    //
    // Centred on this object; its front face is towards -Z (the camera side in a 2.5D level). Put it on the Ground layer
    // so the chicken can stand on it. Keep its rotation and scale at their defaults.
    public class DiggableTerrain : MonoBehaviour, IStrikeReceiver
    {
        [Serializable]
        public struct Hole
        {
            [Tooltip("Centre (m) in this object's local X/Y.")]
            public Vector2 center;

            [Min(0.01f)] public float radius;
        }

        private const int CHUNK_CELLS = 16;
        private const float ISO = 0.5f;

        public enum DigMode
        {
            Through,    // bites cut straight through the block (2D, extruded)
            Rounded     // bites scoop spheres - rounded tunnels inside the dirt (3D)
        }

        [Title("Shape")]
        [EnumToggleButtons]
        [Tooltip("Through: each bite cuts straight through the block (see-through holes; cheap - good for hedges, thin walls). Rounded: each bite scoops a sphere, so tunnels are rounded hollows inside the dirt - use the Foundry/Dirt (See-Through) shader and a SeeThroughWindow on the player to see inside.")]
        [SerializeField] private DigMode mode = DigMode.Through;

        [Tooltip("Width x height (m) of the dirt, centred on this object (local X/Y).")]
        [SerializeField] private Vector2 size = new(6f, 3f);

        [Tooltip("Thickness (m) along Z.")]
        [SerializeField, Min(0.05f)] private float depth = 2f;

        [Tooltip("Size (m) of each density cell - smaller gives smoother, more detailed holes but more mesh to rebuild.")]
        [SerializeField, Range(0.05f, 0.5f)] private float cellSize = 0.1f;

        [ShowIf(nameof(mode), DigMode.Rounded)]
        [Tooltip("Where the chicken walks, as a local Z offset (0 = the middle of the dirt's depth - put this object at the chicken's Z). Bites, pre-dug holes and the see-through window's slice are centred on it.")]
        [SerializeField] private float playPlane;

        [Tooltip("Holes already dug when the level starts (local X/Y centre and radius, in metres - spheres on the play plane in Rounded mode).")]
        [SerializeField] private List<Hole> preDug = new();

        [Title("Digging")]
        [Tooltip("Striking it (e.g. a peck) digs a hole. Off = only Dig() from code digs it.")]
        [SerializeField] private bool digByStrike = true;

        [ShowIf(nameof(digByStrike))]
        [Tooltip("Only strikes of this kind dig (e.g. \"Peck\"). Empty = any strike.")]
        [SerializeField] private string strikeKind;

        [Tooltip("Radius (m) of each bite.")]
        [SerializeField, Min(0.05f)] private float digRadius = 0.4f;

        [Tooltip("How hard it is to dig: the number of strikes to fully clear a bite. 1 = soft dirt (one peck), 3 = hard clay.")]
        [SerializeField, Range(0.25f, 10f)] private float hardness = 1f;

        [Tooltip("How far into the surface a bite is centred, as a fraction of Dig Radius - more digs deeper per peck.")]
        [SerializeField, Range(0f, 1f)] private float digInto = 0.4f;

        [Tooltip("Moves each bite across the strike, as a fraction of Dig Radius. For a forward peck: + is up, - is down (e.g. raise bites so tunnels come out tall enough for the chicken). For an up or down peck: + is the way the chicken faces.")]
        [SerializeField, Range(-1f, 1f)] private float digAcross;

        [Title("Look")]
        [Tooltip("Material for the front and back faces.")]
        [SerializeField] private Material material;

        [ShowIf(nameof(mode), DigMode.Through)]
        [Tooltip("Material for the dug edges and tunnel walls. Empty = the same as Material. (Rounded mode uses Material everywhere - the see-through shader tints steep walls with its Side Color.)")]
        [SerializeField] private Material sideMaterial;

        [Tooltip("Texture repeats per metre (UVs are laid out in world units).")]
        [SerializeField, Min(0.01f)] private float uvScale = 1f;

        [SerializeField] private ShadowCastingMode castShadows = ShadowCastingMode.On;

        [Title("Events")]
        [Tooltip("Spawned (pooled) where each bite is dug - e.g. a burst of dirt particles.")]
        [SerializeField] private Transform digEffect;

        [SerializeField, HideLabel] private FrameCoreEvent onDig = new() { eventName = "Diggable - Dig" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private Vector2Int cells;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int chunkCount;

        private float[] _density;
        private int _nx, _ny;       // cells across and up; corners are (_nx + 1) x (_ny + 1)
        private float _cx, _cy;     // actual cell size, fitted exactly to Size
        private Chunk[] _chunks;
        private int _chunksX, _chunksY;
        private readonly MeshBuilder _builder = new();
        private DirtVolume _volume;

        // Fires with the world point and radius of every bite dug
        public event Action<Vector3, float> Dug;

        private class Chunk
        {
            public Mesh Mesh;
            public MeshFilter Filter;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
        }

        private void Awake()
        {
            if (mode == DigMode.Rounded)
            {
                _volume = new DirtVolume(transform, size, depth, cellSize, playPlane, material, castShadows, uvScale);
                foreach (Hole hole in preDug)
                    _volume.Carve(new Vector3(hole.center.x, hole.center.y, playPlane), hole.radius, 1f, out _);
                _volume.BuildAll();
                chunkCount = transform.childCount;
                return;
            }

            InitialiseDensity();
            CreateChunks();
            for (int chunk = 0; chunk < _chunks.Length; chunk++)
                RebuildChunk(chunk % _chunksX, chunk / _chunksX);
        }

        private void OnDestroy()
        {
            _volume?.Dispose();
            if (_chunks == null) return;
            foreach (Chunk chunk in _chunks)
                if (chunk?.Mesh != null) Destroy(chunk.Mesh);
        }

        public void OnStrike(in StrikeData strike)
        {
            if (!digByStrike)
                return;
            if (!string.IsNullOrEmpty(strikeKind) && strike.Kind != strikeKind)
                return;

            Vector3 into = strike.Direction.sqrMagnitude > 0f ? strike.Direction.normalized : -strike.Normal;

            // Across a mostly-sideways strike is up; across a mostly-vertical one is the way the striker faces
            Vector3 across = Mathf.Abs(into.y) < 0.7f ? Vector3.up
                : strike.Facing.sqrMagnitude > 0f ? strike.Facing.normalized : Vector3.right;

            Dig(strike.Point + into * (digRadius * digInto) + across * (digRadius * digAcross));
        }

        // Digs one bite (Dig Radius, at this dirt's Hardness) centred on a world point
        public void Dig(Vector3 worldPoint)
        {
            Dig(worldPoint, digRadius, 1f / hardness);
        }

        // Digs a round hole: strength 1 fully clears its centre, less takes a partial bite (repeated bites finish it)
        public void Dig(Vector3 worldPoint, float radius, float strength)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            if (_volume != null)
            {
                if (!_volume.Carve(local, radius, strength, out RectInt changed))
                    return;
                _volume.RebuildAround(changed);
            }
            else
            {
                if (_density == null || !Carve(local, radius, strength, out RectInt changedCorners))
                    return;
                RebuildAround(changedCorners);
            }

            if (digEffect != null)
                digEffect.SpawnObject(worldPoint, Quaternion.identity);

            onDig?.Activate();
            Dug?.Invoke(worldPoint, radius);
        }

        // How solid the dirt is at a world point: 1 = solid, 0 = dug out (or outside the dirt). Solid above 0.5.
        public float DensityAt(Vector3 worldPoint)
        {
            if (_volume != null)
                return _volume.DensityAt(transform.InverseTransformPoint(worldPoint));
            if (_density == null)
                return 0f;

            Vector3 local = transform.InverseTransformPoint(worldPoint);
            if (Mathf.Abs(local.z) > depth * 0.5f)
                return 0f;

            float gx = (local.x + size.x * 0.5f) / _cx;
            float gy = (local.y + size.y * 0.5f) / _cy;
            int i = Mathf.FloorToInt(gx), j = Mathf.FloorToInt(gy);
            float tx = gx - i, ty = gy - j;

            float bottom = Mathf.Lerp(Sample(i, j), Sample(i + 1, j), tx);
            float top = Mathf.Lerp(Sample(i, j + 1), Sample(i + 1, j + 1), tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        public bool IsSolidAt(Vector3 worldPoint) => DensityAt(worldPoint) >= ISO;

        // Whether a world point is inside the dirt's box (dug or not)
        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= depth * 0.5f;
        }

        #region Density

        private void InitialiseDensity()
        {
            _nx = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize));
            _ny = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize));
            _cx = size.x / _nx;
            _cy = size.y / _ny;
            cells = new Vector2Int(_nx, _ny);

            _density = new float[(_nx + 1) * (_ny + 1)];
            for (int k = 0; k < _density.Length; k++)
                _density[k] = 1f;

            foreach (Hole hole in preDug)
                Carve(hole.center, hole.radius, 1f, out _);
        }

        // Outside the grid counts as empty, so the dirt's outer edges get walls too
        private float Sample(int i, int j)
        {
            if (i < 0 || j < 0 || i > _nx || j > _ny)
                return 0f;
            return _density[j * (_nx + 1) + i];
        }

        private Vector2 CornerPosition(int i, int j) => new(-size.x * 0.5f + i * _cx, -size.y * 0.5f + j * _cy);

        private bool Carve(Vector2 local, float radius, float strength, out RectInt changed)
        {
            int iMin = Mathf.Max(0, Mathf.FloorToInt((local.x - radius + size.x * 0.5f) / _cx));
            int iMax = Mathf.Min(_nx, Mathf.CeilToInt((local.x + radius + size.x * 0.5f) / _cx));
            int jMin = Mathf.Max(0, Mathf.FloorToInt((local.y - radius + size.y * 0.5f) / _cy));
            int jMax = Mathf.Min(_ny, Mathf.CeilToInt((local.y + radius + size.y * 0.5f) / _cy));
            changed = new RectInt(iMin, jMin, iMax - iMin, jMax - jMin);

            bool any = false;
            float core = radius * 0.5f;
            for (int j = jMin; j <= jMax; j++)
            {
                for (int i = iMin; i <= iMax; i++)
                {
                    float distance = Vector2.Distance(CornerPosition(i, j), local);
                    if (distance >= radius)
                        continue;

                    // Full strength in the middle, easing off to nothing at the rim - a soft, round bite
                    float amount = strength * (1f - Mathf.SmoothStep(0f, 1f, (distance - core) / (radius - core)));
                    int index = j * (_nx + 1) + i;
                    float before = _density[index];
                    _density[index] = Mathf.Max(0f, before - amount);
                    any |= _density[index] != before;
                }
            }

            return any;
        }

        #endregion

        #region Meshing

        // Cells run from -1 to _nx (one ring outside the grid, which is empty) so the outer edges close
        private void CreateChunks()
        {
            _chunksX = Mathf.CeilToInt((_nx + 2) / (float)CHUNK_CELLS);
            _chunksY = Mathf.CeilToInt((_ny + 2) / (float)CHUNK_CELLS);
            _chunks = new Chunk[_chunksX * _chunksY];
            chunkCount = _chunks.Length;

            Material[] materials = { material, sideMaterial != null ? sideMaterial : material };

            for (int cy = 0; cy < _chunksY; cy++)
            {
                for (int cx = 0; cx < _chunksX; cx++)
                {
                    GameObject chunkObject = new($"Dirt Chunk {cx},{cy}") { layer = gameObject.layer };
                    chunkObject.transform.SetParent(transform, false);

                    Chunk chunk = new()
                    {
                        Mesh = new Mesh { name = chunkObject.name },
                        Filter = chunkObject.AddComponent<MeshFilter>(),
                        Renderer = chunkObject.AddComponent<MeshRenderer>(),
                        Collider = chunkObject.AddComponent<MeshCollider>()
                    };
                    chunk.Mesh.MarkDynamic();
                    chunk.Filter.sharedMesh = chunk.Mesh;
                    chunk.Renderer.sharedMaterials = materials;
                    chunk.Renderer.shadowCastingMode = castShadows;
                    _chunks[cy * _chunksX + cx] = chunk;
                }
            }
        }

        private void RebuildAround(RectInt corners)
        {
            // A corner is shared by the cells to either side of it; cell c sits at chunk (c + 1) / CHUNK_CELLS
            int cellMinX = corners.xMin - 1, cellMaxX = corners.xMax;
            int cellMinY = corners.yMin - 1, cellMaxY = corners.yMax;
            int chunkMinX = Mathf.Clamp((cellMinX + 1) / CHUNK_CELLS, 0, _chunksX - 1);
            int chunkMaxX = Mathf.Clamp((cellMaxX + 1) / CHUNK_CELLS, 0, _chunksX - 1);
            int chunkMinY = Mathf.Clamp((cellMinY + 1) / CHUNK_CELLS, 0, _chunksY - 1);
            int chunkMaxY = Mathf.Clamp((cellMaxY + 1) / CHUNK_CELLS, 0, _chunksY - 1);

            for (int cy = chunkMinY; cy <= chunkMaxY; cy++)
                for (int cx = chunkMinX; cx <= chunkMaxX; cx++)
                    RebuildChunk(cx, cy);
        }

        private void RebuildChunk(int chunkX, int chunkY)
        {
            Chunk chunk = _chunks[chunkY * _chunksX + chunkX];
            int firstX = chunkX * CHUNK_CELLS - 1, firstY = chunkY * CHUNK_CELLS - 1;
            int lastX = Mathf.Min(firstX + CHUNK_CELLS - 1, _nx), lastY = Mathf.Min(firstY + CHUNK_CELLS - 1, _ny);

            _builder.Clear();
            for (int j = firstY; j <= lastY; j++)
                for (int i = firstX; i <= lastX; i++)
                    MeshCell(i, j, _builder);

            _builder.ApplyTo(chunk.Mesh);

            // A collider can't have an empty mesh - a fully dug-out chunk has none
            bool hasMesh = _builder.VertexCount > 0;
            chunk.Collider.sharedMesh = null;
            chunk.Collider.enabled = hasMesh;
            if (hasMesh)
                chunk.Collider.sharedMesh = chunk.Mesh;
        }

        // Marching squares for one cell: the solid part as a polygon (corners in counter-clockwise order, with the
        // points where the surface crosses its edges), filled front and back, and walled where it meets the air
        private readonly Vector2[] _polygon = new Vector2[8];
        private readonly bool[] _crossing = new bool[8];

        private void MeshCell(int i, int j, MeshBuilder builder)
        {
            float d0 = Sample(i, j), d1 = Sample(i + 1, j), d2 = Sample(i + 1, j + 1), d3 = Sample(i, j + 1);
            if (d0 < ISO && d1 < ISO && d2 < ISO && d3 < ISO)
                return;

            Vector2 p0 = CornerPosition(i, j), p1 = CornerPosition(i + 1, j), p2 = CornerPosition(i + 1, j + 1), p3 = CornerPosition(i, j + 1);
            int count = 0;
            AddEdge(p0, d0, p1, d1, ref count);
            AddEdge(p1, d1, p2, d2, ref count);
            AddEdge(p2, d2, p3, d3, ref count);
            AddEdge(p3, d3, p0, d0, ref count);
            if (count < 3)
                return;

            float front = -depth * 0.5f, back = depth * 0.5f;
            builder.AddFan(_polygon, count, front, back, uvScale);

            for (int m = 0; m < count; m++)
            {
                int next = (m + 1) % count;
                if (_crossing[m] && _crossing[next])
                    builder.AddWall(_polygon[m], _polygon[next], front, back, uvScale);
            }
        }

        private void AddEdge(Vector2 a, float da, Vector2 b, float db, ref int count)
        {
            bool solidA = da >= ISO, solidB = db >= ISO;
            if (solidA)
            {
                _polygon[count] = a;
                _crossing[count++] = false;
            }

            if (solidA != solidB)
            {
                float t = (ISO - da) / (db - da);
                _polygon[count] = Vector2.Lerp(a, b, t);
                _crossing[count++] = true;
            }
        }

        // Collects a mesh's vertices and triangles: submesh 0 = front/back faces, submesh 1 = walls
        private class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _normals = new();
            private readonly List<Vector2> _uvs = new();
            private readonly List<int> _faces = new();
            private readonly List<int> _walls = new();

            public int VertexCount => _vertices.Count;

            public void Clear()
            {
                _vertices.Clear();
                _normals.Clear();
                _uvs.Clear();
                _faces.Clear();
                _walls.Clear();
            }

            // A convex polygon (counter-clockwise in X/Y) on the front (facing -Z) and back (facing +Z)
            public void AddFan(Vector2[] polygon, int count, float front, float back, float uvScale)
            {
                int frontStart = _vertices.Count;
                for (int m = 0; m < count; m++)
                    AddVertex(new Vector3(polygon[m].x, polygon[m].y, front), Vector3.back, polygon[m] * uvScale);

                int backStart = _vertices.Count;
                for (int m = 0; m < count; m++)
                    AddVertex(new Vector3(polygon[m].x, polygon[m].y, back), Vector3.forward, polygon[m] * uvScale);

                for (int m = 1; m < count - 1; m++)
                {
                    _faces.Add(frontStart); _faces.Add(frontStart + m + 1); _faces.Add(frontStart + m);
                    _faces.Add(backStart); _faces.Add(backStart + m); _faces.Add(backStart + m + 1);
                }
            }

            // A wall along the surface from a to b (solid on the left), facing out into the dug space
            public void AddWall(Vector2 a, Vector2 b, float front, float back, float uvScale)
            {
                Vector2 edge = b - a;
                if (edge.sqrMagnitude < 1e-10f)
                    return;

                Vector3 normal = new Vector3(edge.y, -edge.x, 0f).normalized;
                float u0 = (a.x + a.y) * uvScale, u1 = (b.x + b.y) * uvScale;

                int start = _vertices.Count;
                AddVertex(new Vector3(a.x, a.y, front), normal, new Vector2(u0, front * uvScale));
                AddVertex(new Vector3(b.x, b.y, front), normal, new Vector2(u1, front * uvScale));
                AddVertex(new Vector3(b.x, b.y, back), normal, new Vector2(u1, back * uvScale));
                AddVertex(new Vector3(a.x, a.y, back), normal, new Vector2(u0, back * uvScale));

                _walls.Add(start); _walls.Add(start + 1); _walls.Add(start + 2);
                _walls.Add(start); _walls.Add(start + 2); _walls.Add(start + 3);
            }

            private void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                _vertices.Add(position);
                _normals.Add(normal);
                _uvs.Add(uv);
            }

            public void ApplyTo(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(_faces, 0);
                mesh.SetTriangles(_walls, 1);
                mesh.RecalculateBounds();
            }
        }

        #endregion

#if UNITY_EDITOR
        private static readonly Color GizmoDirtColor = new(0.55f, 0.4f, 0.25f);

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;

            // Edit-mode preview: the dirt's box, and the holes dug at the start
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(GizmoDirtColor.r, GizmoDirtColor.g, GizmoDirtColor.b, 0.35f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, depth));
            Gizmos.color = GizmoDirtColor;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, depth));

            Gizmos.color = new Color(0.1f, 0.08f, 0.05f, 0.9f);
            foreach (Hole hole in preDug)
            {
                const int STEPS = 24;
                Vector3 previous = new(hole.center.x + hole.radius, hole.center.y, -depth * 0.5f);
                for (int s = 1; s <= STEPS; s++)
                {
                    float angle = s * Mathf.PI * 2f / STEPS;
                    Vector3 point = new(hole.center.x + Mathf.Cos(angle) * hole.radius, hole.center.y + Mathf.Sin(angle) * hole.radius, -depth * 0.5f);
                    Gizmos.DrawLine(previous, point);
                    previous = point;
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
