using System;
using System.Collections.Generic;
using Foundry.Damage;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Fracture
{
    // Breaks this object's mesh into chunks - each with its own convex collider and Rigidbody - flung apart from the
    // break point. Call Break() (or use the Fracture action, or leave Break When Life Dies on with a Life on this object).
    //  - Pre-baked: press Bake Fracture in the editor - the chunks are cut now and saved (meshes in Assets/Fractures, the
    //    pieces as a hidden child), so breaking costs nothing at runtime and you can see the exact pieces.
    //  - Runtime: the mesh is cut at the moment it breaks - no setup, and more of the pieces cluster where it was hit.
    // Either way the original's renderer and colliders are switched off and the pieces take over.
    //
    // Works best on solid, roughly convex shapes: planks, boxes, panes, rocks, platforms. Needs a MeshFilter + MeshRenderer
    // on this object (an imported model's mesh needs Read/Write enabled to break at runtime in a build).
    [DisallowMultipleComponent]
    public class Fracturable : MonoBehaviour
    {
        public enum FractureMode
        {
            PreBaked,
            Runtime
        }

        [Title("Fracture")]
        [EnumToggleButtons]
        [Tooltip("Pre-Baked: cut in the editor (Bake Fracture) and saved - free at runtime, exact pieces. Runtime: cut when it breaks - no setup, finer pieces around the hit.")]
        [SerializeField] private FractureMode mode = FractureMode.Runtime;

        [Tooltip("How many pieces to break into.")]
        [SerializeField, Range(2, 40)] private int pieceCount = 8;

        [Tooltip("Pattern of the cuts. The same number gives the same pieces. Runtime: 0 = a different pattern every time.")]
        [SerializeField] private int randomSeed = 1;

        [ShowIf(nameof(mode), FractureMode.Runtime)]
        [Tooltip("Share of the pieces clustered around the break point - smaller pieces where it was hit, bigger ones further away. 0 = spread evenly.")]
        [SerializeField, Range(0f, 1f)] private float focusOnHit = 0.5f;

        [ShowIf(nameof(mode), FractureMode.Runtime)]
        [Tooltip("How far (m) around the break point the clustered pieces are.")]
        [SerializeField, Min(0.01f)] private float focusRadius = 0.5f;

        [Tooltip("Material for the freshly cut faces inside the pieces. Empty = the object's own (first) material.")]
        [SerializeField] private Material insideMaterial;

        [Tooltip("Texture repeats per metre on the cut faces.")]
        [SerializeField, Min(0.01f)] private float insideUvScale = 1f;

        [Title("Pieces")]
        [Tooltip("Layer for the pieces - e.g. Debris so the chicken passes through the rubble, or a solid layer so it can stand on and push them.")]
        [SerializeField, ValueDropdown(nameof(LayerNames))] private int pieceLayer;

        [Tooltip("Total mass (kg) shared between the pieces by size. 0 = this object's Rigidbody mass, or 1 kg if it has none.")]
        [SerializeField, Min(0f)] private float totalMass;

        [Tooltip("Keep the pieces in the X/Y plane (2.5D) - they don't fly toward or away from the camera.")]
        [SerializeField] private bool lockToPlane = true;

        [Tooltip("How hard the pieces burst apart from the break point (m/s at the centre of the blast).")]
        [SerializeField, Min(0f)] private float burstSpeed = 3f;

        [Tooltip("Reach (m) of the burst from the break point.")]
        [SerializeField, Min(0.01f)] private float burstRadius = 2f;

        [Tooltip("How much the burst throws pieces upward as well as outward.")]
        [SerializeField, Min(0f)] private float burstLift = 0.5f;

        [Title("Debris")]
        [EnumToggleButtons, HideLabel]
        [SerializeField] private FractureChunk.Debris debris = FractureChunk.Debris.ShrinkAway;

        [ShowIf(nameof(debris), FractureChunk.Debris.ShrinkAway)]
        [Tooltip("Seconds the pieces stay before shrinking away.")]
        [SerializeField, Min(0f)] private float lifetime = 4f;

        [ShowIf(nameof(debris), FractureChunk.Debris.ShrinkAway)]
        [Tooltip("Seconds the shrink takes.")]
        [SerializeField, Min(0.01f)] private float shrinkTime = 0.6f;

        [ShowIf(nameof(debris), FractureChunk.Debris.SettleThenFreeze)]
        [Tooltip("A piece moving slower than this (m/s) for half a second has settled and is frozen in place.")]
        [SerializeField, Min(0.001f)] private float settleSpeed = 0.1f;

        [Title("Breaking")]
        [Tooltip("Break when a Life on this object dies, from where it was last hit - no wiring needed.")]
        [SerializeField] private bool breakWhenLifeDies = true;

        [SerializeField, HideLabel] private FrameCoreEvent onBreak = new() { eventName = "Fracturable - Break" };

        [FoldoutGroup("Baked")]
        [SerializeField, ReadOnly] private GameObject bakedPieces;

        [FoldoutGroup("Baked")]
        [SerializeField, ReadOnly] private List<Mesh> bakedMeshes = new();

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool broken;

        private const string BAKED_ROOT_NAME = "Fractured Pieces";

        private Life _life;

        public bool IsBroken => broken;

        // Fires once it has broken, with the pieces
        public event Action<Fracturable, IReadOnlyList<GameObject>> Broke;

        private void Reset()
        {
            int debrisLayer = LayerMask.NameToLayer("Debris");
            pieceLayer = debrisLayer >= 0 ? debrisLayer : gameObject.layer;
        }

        private void OnEnable()
        {
            if (breakWhenLifeDies && TryGetComponent(out _life))
                _life.Died += OnLifeDied;
        }

        private void OnDisable()
        {
            if (_life != null)
                _life.Died -= OnLifeDied;
        }

        private void OnLifeDied(Life life)
        {
            if (life.LastDamage != null && life.LastDamage.point != Vector3.zero)
                Break(life.LastDamage.point);
            else
                Break();
        }

        // Breaks from the middle of the object
        [Button]
        public void Break()
        {
            Break(TryGetComponent(out Renderer own) ? own.bounds.center : transform.position);
        }

        // Breaks from a world point (where it was hit): the pieces burst away from it
        public void Break(Vector3 worldPoint)
        {
            if (broken || !Application.isPlaying)
                return;

            List<GameObject> pieces = mode == FractureMode.PreBaked && bakedPieces != null
                ? ReleaseBakedPieces()
                : CutPieces(transform.InverseTransformPoint(worldPoint));

            if (pieces == null)
                return;

            broken = true;
            Rigidbody original = GetComponent<Rigidbody>();
            float mass = totalMass > 0f ? totalMass : original != null ? original.mass : 1f;
            float volumeSum = 0f;
            foreach (GameObject piece in pieces)
                volumeSum += PieceVolume(piece);

            foreach (GameObject piece in pieces)
            {
                piece.layer = pieceLayer;
                piece.transform.SetParent(null, true);

                Rigidbody body = piece.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.01f, mass * (volumeSum > 0f ? PieceVolume(piece) / volumeSum : 1f / pieces.Count));
                body.interpolation = RigidbodyInterpolation.Interpolate;
                if (lockToPlane)
                    body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

                if (original != null && !original.isKinematic)
                    body.linearVelocity = original.GetPointVelocity(piece.transform.position);

                body.AddExplosionForce(burstSpeed, worldPoint, burstRadius, burstLift, ForceMode.VelocityChange);

                piece.AddComponent<FractureChunk>().Initialise(debris, lifetime, shrinkTime, settleSpeed);
            }

            HideOriginal();
            onBreak?.Activate();
            Broke?.Invoke(this, pieces);
        }

        private static float PieceVolume(GameObject piece)
        {
            Mesh mesh = piece.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh == null) return 0f;
            Vector3 size = Vector3.Scale(mesh.bounds.size, piece.transform.lossyScale);
            return Mathf.Abs(size.x * size.y * size.z);
        }

        // The original stops being drawn and collided with; the object (and anything else on it) stays active
        private void HideOriginal()
        {
            foreach (Renderer own in GetComponents<Renderer>())
                own.enabled = false;
            foreach (Collider own in GetComponents<Collider>())
                own.enabled = false;

            if (TryGetComponent(out Rigidbody body))
                body.isKinematic = true;
        }

        private List<GameObject> ReleaseBakedPieces()
        {
            bakedPieces.SetActive(true);
            List<GameObject> pieces = new();
            foreach (Transform child in bakedPieces.transform)
                pieces.Add(child.gameObject);

            foreach (GameObject piece in pieces)
                piece.transform.SetParent(transform, true);
            Destroy(bakedPieces);
            bakedPieces = null;
            return pieces;
        }

        // Runtime: cut the mesh now, with some pieces clustered around the hit
        private List<GameObject> CutPieces(Vector3 localHit)
        {
            if (!TryGetSource(out Mesh mesh, out Material[] materials))
                return null;

            if (!mesh.isReadable)
            {
                Debug.LogWarning($"Fracturable >> {name}: its mesh isn't readable - enable Read/Write on the model to break it at runtime, or use Pre-Baked.", this);
                return null;
            }

            System.Random random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            List<Vector3> seeds = MeshFracturer.RandomSeeds(mesh.bounds, pieceCount, random, localHit, focusOnHit, focusRadius);
            List<MeshFracturer.Chunk> chunks = MeshFracturer.Fracture(mesh, seeds, insideUvScale);

            List<GameObject> pieces = new(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
                pieces.Add(CreatePiece(chunks[i], i, materials, transform));
            return pieces;
        }

        private bool TryGetSource(out Mesh mesh, out Material[] materials)
        {
            mesh = TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            materials = null;
            if (mesh == null || !TryGetComponent(out MeshRenderer meshRenderer))
            {
                Debug.LogWarning($"Fracturable >> {name} needs a MeshFilter with a mesh and a MeshRenderer to break.", this);
                return false;
            }

            // The source's materials, one per submesh, plus the inside material for the cut faces
            Material[] own = meshRenderer.sharedMaterials;
            int submeshes = Mathf.Max(1, mesh.subMeshCount);
            materials = new Material[submeshes + 1];
            for (int s = 0; s < submeshes; s++)
                materials[s] = own.Length > 0 ? own[Mathf.Min(s, own.Length - 1)] : null;
            materials[submeshes] = insideMaterial != null ? insideMaterial : materials[0];
            return true;
        }

        private GameObject CreatePiece(MeshFracturer.Chunk chunk, int index, Material[] materials, Transform parent)
        {
            GameObject piece = new($"{name} - Piece {index}") { layer = pieceLayer };
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = chunk.Center;

            piece.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials;

            MeshCollider collider = piece.AddComponent<MeshCollider>();
            collider.sharedMesh = chunk.Mesh;
            collider.convex = true;
            return piece;
        }

        private static IEnumerable<ValueDropdownItem<int>> LayerNames()
        {
            for (int layer = 0; layer < 32; layer++)
            {
                string layerName = LayerMask.LayerToName(layer);
                if (!string.IsNullOrEmpty(layerName))
                    yield return new ValueDropdownItem<int>(layerName, layer);
            }
        }

#if UNITY_EDITOR
        // Cuts the pieces now and saves them: meshes in Assets/Fractures, the pieces as a hidden (inactive) child
        [ShowIf(nameof(mode), FractureMode.PreBaked)]
        [Button("Bake Fracture"), GUIColor(0.6f, 0.9f, 0.6f)]
        private void BakeFracture()
        {
            if (!TryGetSource(out Mesh mesh, out Material[] materials))
                return;

            ClearFracture();

            System.Random random = new(randomSeed);
            List<Vector3> seeds = MeshFracturer.RandomSeeds(mesh.bounds, pieceCount, random);
            List<MeshFracturer.Chunk> chunks = MeshFracturer.Fracture(mesh, seeds, insideUvScale);
            if (chunks.Count == 0)
            {
                Debug.LogWarning($"Fracturable >> {name}: the fracture produced no pieces.", this);
                return;
            }

            // Save the meshes as one asset file
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Fractures"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Fractures");
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"Assets/Fractures/{name} Fracture.asset");

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Mesh.name = $"{name} Piece {i}";
                if (i == 0) UnityEditor.AssetDatabase.CreateAsset(chunks[i].Mesh, path);
                else UnityEditor.AssetDatabase.AddObjectToAsset(chunks[i].Mesh, path);
            }
            UnityEditor.AssetDatabase.SaveAssets();

            GameObject root = new(BAKED_ROOT_NAME);
            UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Bake Fracture");
            root.transform.SetParent(transform, false);

            bakedMeshes.Clear();
            for (int i = 0; i < chunks.Count; i++)
            {
                CreatePiece(chunks[i], i, materials, root.transform);
                bakedMeshes.Add(chunks[i].Mesh);
            }

            root.SetActive(false);
            UnityEditor.Undo.RecordObject(this, "Bake Fracture");
            bakedPieces = root;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Fracturable >> {name}: baked {chunks.Count} pieces into {path}.", this);
        }

        // Removes the baked pieces and their saved meshes
        [ShowIf(nameof(mode), FractureMode.PreBaked)]
        [Button("Clear Fracture")]
        private void ClearFracture()
        {
            if (bakedMeshes.Count > 0 && bakedMeshes[0] != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(bakedMeshes[0]);
                if (!string.IsNullOrEmpty(path))
                    UnityEditor.AssetDatabase.DeleteAsset(path);
            }
            bakedMeshes.Clear();

            if (bakedPieces != null)
                UnityEditor.Undo.DestroyObjectImmediate(bakedPieces);
            bakedPieces = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Preview the baked pieces in the Scene view while selected
        private void OnDrawGizmosSelected()
        {
            if (bakedPieces == null)
                return;

            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.8f);
            foreach (Transform piece in bakedPieces.transform)
            {
                if (piece.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    Gizmos.DrawWireMesh(filter.sharedMesh, piece.position, piece.rotation, piece.lossyScale);
            }
        }
#endif
    }
}
