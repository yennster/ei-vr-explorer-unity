using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Populates the object-detection demo scene with a few props on a virtual
    /// table. If no <see cref="propPrefabs"/> are wired in the inspector,
    /// falls back to spawning Unity primitive shapes (cube/sphere/capsule)
    /// with random colors so the demo runs out of the box.
    ///
    /// Recommended workflow:
    /// 1. Run <c>tools/fetch_apple_usdz.sh</c> to download Apple's free USDZ
    ///    sample models (Pancakes, Toy Drummer, Hummingbird, etc.).
    /// 2. Convert each .usdz → .fbx or .glb (Blender or Pixar usdtools).
    /// 3. Drop the resulting prefabs into Assets/Models/ and assign them to
    ///    the propPrefabs array on this component.
    /// </summary>
    public class DemoSceneSpawner : MonoBehaviour
    {
        [Header("Props")]
        [SerializeField] private GameObject[] propPrefabs;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private int spawnCount = 5;
        [SerializeField] private Vector3 areaSize = new Vector3(2f, 0f, 1f);
        [SerializeField] private float yOnTable = 0.05f;

        [Header("Fallback (when no propPrefabs assigned)")]
        [SerializeField] private bool spawnPrimitivesIfEmpty = true;

        [Header("Auto-normalize prefab size")]
        [Tooltip("Target longest-axis size in meters for spawned prefabs. " +
                 "Khronos glTF samples ship in inconsistent units (DamagedHelmet ~2m, " +
                 "Avocado ~0.07m, Lantern ~3m); this normalizes them to a sane table size.")]
        [SerializeField] private float targetSizeMeters = 0.18f;

        private void Start()
        {
            if (spawnRoot == null) spawnRoot = transform;
            for (int i = 0; i < spawnCount; i++) SpawnOne(i);
        }

        private void SpawnOne(int seed)
        {
            Vector3 pos = spawnRoot.position + new Vector3(
                (Random.value - 0.5f) * areaSize.x,
                yOnTable + areaSize.y * Random.value,
                (Random.value - 0.5f) * areaSize.z);
            float yaw = Random.value * 360f;

            if (propPrefabs != null && propPrefabs.Length > 0)
            {
                var prefab = propPrefabs[seed % propPrefabs.Length];
                if (prefab == null) return;
                var go = Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), spawnRoot);
                go.name = $"prop_{seed}_{prefab.name}";
                NormalizeToFitTable(go);
                return;
            }

            if (!spawnPrimitivesIfEmpty) return;

            var primTypes = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder };
            var prim = GameObject.CreatePrimitive(primTypes[seed % primTypes.Length]);
            prim.transform.SetParent(spawnRoot);
            prim.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            prim.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);
            var mr = prim.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(mr.sharedMaterial != null ? mr.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = Color.HSVToRGB(Random.value, 0.6f, 0.95f);
                mr.material = mat;
            }
            prim.name = $"prop_{seed}_{prim.name}";
        }

        /// <summary>
        /// Scale a freshly-instantiated prefab so its largest dimension equals
        /// targetSizeMeters. Avoids the situation where DamagedHelmet (2 m) and
        /// Avocado (7 cm) sit on the same table.
        /// </summary>
        private void NormalizeToFitTable(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float maxAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxAxis <= 1e-4f) return;
            float k = targetSizeMeters / maxAxis;
            go.transform.localScale *= k;
            // Lift slightly so the prop's lowest point sits on the table.
            // Recompute bounds after scaling.
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float lift = go.transform.position.y - b.min.y;
            go.transform.position += new Vector3(0, lift, 0);
        }
    }
}
