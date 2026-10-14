using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Foundry.Fracture
{
    // Splits a mesh into Voronoi-style chunks: each chunk is the part of the mesh nearer its seed point than any other,
    // cut out by clipping the mesh against the half-way plane between its seed and every other seed. Cut faces are
    // capped and put on an extra submesh (for an "inside" material). Each chunk comes back centred on its own middle.
    //
    // Works on solid, roughly convex shapes (boxes, planks, panes, rocks, platforms): the caps are filled as convex
    // polygons, so a hollow or very concave mesh can get wrong caps.
    public static class MeshFracturer
    {
        public struct Chunk
        {
            public Mesh Mesh;
            public Vector3 Center;  // in the source mesh's space - where to place the chunk
            public float Volume;
        }

        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;

            public static Vertex Lerp(Vertex a, Vertex b, float t) => new()
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Uv = Vector2.Lerp(a.Uv, b.Uv, t)
            };
        }

        private class Polygon
        {
            public List<Vertex> Vertices;
            public int Submesh;
        }

        private const float EPSILON = 1e-5f;
        private const float MIN_VOLUME = 1e-6f;

        // Random seed points inside a mesh's bounds - some of them (focus) clustered around a point, for finer pieces
        // where it was hit
        public static List<Vector3> RandomSeeds(Bounds bounds, int count, System.Random random, Vector3? focus = null, float focusShare = 0f, float focusRadius = 0.5f)
        {
            List<Vector3> seeds = new(count);
            int focused = focus.HasValue ? Mathf.RoundToInt(count * Mathf.Clamp01(focusShare)) : 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 point;
                if (i < focused)
                {
                    Vector3 offset = new((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f);
                    point = focus.Value + offset * focusRadius;
                    point = Vector3.Max(bounds.min, Vector3.Min(bounds.max, point));
                }
                else
                {
                    point = new Vector3(
                        Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                        Mathf.Lerp(bounds.min.y, bounds.max.y, (float)random.NextDouble()),
                        Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
                }

                seeds.Add(point);
            }

            return seeds;
        }

        // The chunks, with the source's submeshes kept and the cut faces on submesh [source.subMeshCount]
        public static List<Chunk> Fracture(Mesh source, IReadOnlyList<Vector3> seeds, float capUvScale = 1f)
        {
            List<Polygon> polygons = ReadPolygons(source, out int submeshCount);
            List<Chunk> chunks = new();

            for (int i = 0; i < seeds.Count; i++)
            {
                List<Polygon> cell = polygons;
                for (int j = 0; j < seeds.Count && cell.Count > 0; j++)
                {
                    if (i == j)
                        continue;

                    Vector3 normal = seeds[j] - seeds[i];
                    if (normal.sqrMagnitude < 1e-10f)
                        continue;

                    normal.Normalize();
                    float distance = Vector3.Dot(normal, (seeds[i] + seeds[j]) * 0.5f);
                    cell = Clip(cell, normal, distance, submeshCount, capUvScale);
                }

                if (cell.Count == 0)
                    continue;

                Chunk chunk = BuildChunk(cell, submeshCount + 1);
                if (chunk.Volume > MIN_VOLUME)
                    chunks.Add(chunk);
                else if (Application.isPlaying)
                    Object.Destroy(chunk.Mesh);
                else
                    Object.DestroyImmediate(chunk.Mesh);
            }

            return chunks;
        }

        private static List<Polygon> ReadPolygons(Mesh source, out int submeshCount)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            bool hasNormals = normals != null && normals.Length == positions.Length;
            bool hasUvs = uvs != null && uvs.Length == positions.Length;

            submeshCount = Mathf.Max(1, source.subMeshCount);
            List<Polygon> polygons = new();

            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] triangles = source.GetTriangles(s);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    List<Vertex> vertices = new(3);
                    Vector3 faceNormal = Vector3.Cross(positions[triangles[t + 1]] - positions[triangles[t]], positions[triangles[t + 2]] - positions[triangles[t]]).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        int index = triangles[t + k];
                        vertices.Add(new Vertex
                        {
                            Position = positions[index],
                            Normal = hasNormals ? normals[index] : faceNormal,
                            Uv = hasUvs ? uvs[index] : Vector2.zero
                        });
                    }

                    polygons.Add(new Polygon { Vertices = vertices, Submesh = s });
                }
            }

            return polygons;
        }

        // Keeps the part of every polygon on the near side of the plane (dot(normal, p) <= distance) and caps the cut
        private static List<Polygon> Clip(List<Polygon> polygons, Vector3 normal, float distance, int capSubmesh, float capUvScale)
        {
            List<Polygon> kept = new(polygons.Count);
            List<Vector3> cutPoints = new();

            foreach (Polygon polygon in polygons)
            {
                List<Vertex> vertices = polygon.Vertices;
                int count = vertices.Count;
                List<Vertex> output = null;

                for (int k = 0; k < count; k++)
                {
                    Vertex current = vertices[k];
                    Vertex next = vertices[(k + 1) % count];
                    float dc = Vector3.Dot(normal, current.Position) - distance;
                    float dn = Vector3.Dot(normal, next.Position) - distance;

                    output ??= new List<Vertex>(count + 2);
                    if (dc <= EPSILON)
                    {
                        output.Add(current);
                        if (Mathf.Abs(dc) <= EPSILON)
                            cutPoints.Add(current.Position);
                    }

                    if ((dc < -EPSILON && dn > EPSILON) || (dc > EPSILON && dn < -EPSILON))
                    {
                        Vertex crossing = Vertex.Lerp(current, next, dc / (dc - dn));
                        output.Add(crossing);
                        cutPoints.Add(crossing.Position);
                    }
                }

                if (output != null && output.Count >= 3)
                    kept.Add(new Polygon { Vertices = output, Submesh = polygon.Submesh });
            }

            Polygon cap = BuildCap(cutPoints, normal, capSubmesh, capUvScale);
            if (cap != null)
                kept.Add(cap);

            return kept;
        }

        // The cut face: the cut points sorted around their middle into a convex polygon facing out along the plane normal
        private static Polygon BuildCap(List<Vector3> points, Vector3 normal, int submesh, float uvScale)
        {
            List<Vector3> unique = new();
            foreach (Vector3 point in points)
            {
                bool duplicate = false;
                foreach (Vector3 existing in unique)
                {
                    if ((existing - point).sqrMagnitude < EPSILON * EPSILON * 100f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    unique.Add(point);
            }

            if (unique.Count < 3)
                return null;

            Vector3 center = Vector3.zero;
            foreach (Vector3 point in unique)
                center += point;
            center /= unique.Count;

            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal, u);
            unique.Sort((a, b) =>
                Mathf.Atan2(Vector3.Dot(a - center, v), Vector3.Dot(a - center, u))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(b - center, v), Vector3.Dot(b - center, u))));

            List<Vertex> vertices = new(unique.Count);
            foreach (Vector3 point in unique)
            {
                vertices.Add(new Vertex
                {
                    Position = point,
                    Normal = normal,
                    Uv = new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v)) * uvScale
                });
            }

            return new Polygon { Vertices = vertices, Submesh = submesh };
        }

        private static Chunk BuildChunk(List<Polygon> polygons, int submeshCount)
        {
            List<Vector3> positions = new();
            List<Vector3> normals = new();
            List<Vector2> uvs = new();
            List<int>[] triangles = new List<int>[submeshCount];
            for (int s = 0; s < submeshCount; s++)
                triangles[s] = new List<int>();

            Vector3 sum = Vector3.zero;
            int vertexCount = 0;
            foreach (Polygon polygon in polygons)
            {
                foreach (Vertex vertex in polygon.Vertices)
                {
                    sum += vertex.Position;
                    vertexCount++;
                }
            }

            Vector3 center = vertexCount > 0 ? sum / vertexCount : Vector3.zero;
            float volume = 0f;

            foreach (Polygon polygon in polygons)
            {
                List<Vertex> vertices = polygon.Vertices;
                if (vertices.Count < 3)
                    continue;

                // Fan it, wound so the face points the way its normal does. The polygon's facing comes from all its
                // corners (Newell's method) - its first three can sit in a straight line (common on a cut face, along
                // the edge where it meets a flat side), which would give no direction and a randomly flipped face.
                Vector3 faceNormal = Vector3.zero;
                for (int k = 0; k < vertices.Count; k++)
                {
                    Vector3 current = vertices[k].Position;
                    Vector3 next = vertices[(k + 1) % vertices.Count].Position;
                    faceNormal.x += (current.y - next.y) * (current.z + next.z);
                    faceNormal.y += (current.z - next.z) * (current.x + next.x);
                    faceNormal.z += (current.x - next.x) * (current.y + next.y);
                }

                Vector3 expected = Vector3.zero;
                foreach (Vertex vertex in vertices)
                    expected += vertex.Normal;
                bool flip = Vector3.Dot(faceNormal, expected) < 0f;

                int start = positions.Count;
                foreach (Vertex vertex in vertices)
                {
                    positions.Add(vertex.Position - center);
                    normals.Add(vertex.Normal);
                    uvs.Add(vertex.Uv);
                }

                List<int> list = triangles[Mathf.Clamp(polygon.Submesh, 0, submeshCount - 1)];
                for (int k = 1; k < vertices.Count - 1; k++)
                {
                    int a = start, b = start + k, c = start + k + 1;
                    if (flip) (b, c) = (c, b);
                    list.Add(a); list.Add(b); list.Add(c);

                    // Volume of the closed surface (divergence theorem), about the centre
                    volume += Vector3.Dot(positions[a], Vector3.Cross(positions[b], positions[c])) / 6f;
                }
            }

            Mesh mesh = new() { name = "Chunk" };
            mesh.indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshCount;
            for (int s = 0; s < submeshCount; s++)
                mesh.SetTriangles(triangles[s], s);
            mesh.RecalculateBounds();

            return new Chunk { Mesh = mesh, Center = center, Volume = Mathf.Abs(volume) };
        }
    }
}
