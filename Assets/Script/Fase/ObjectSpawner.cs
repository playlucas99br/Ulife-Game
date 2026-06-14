using UnityEngine;

namespace FaseLucasGame
{
    /// <summary>
    /// Guarantees there is always exactly one red Cube and one blue Sphere in the arena.
    /// Spawns replacements at random ground positions when objects are incinerated.
    /// </summary>
    public class ObjectSpawner : MonoBehaviour
    {
        [Header("Materials")]
        public Material redMaterial;
        public Material blueMaterial;

        [Header("Spawn area (world XZ), ground at spawnY)")]
        public Vector2 areaMin = new Vector2(-15f, -15f);
        public Vector2 areaMax = new Vector2(15f, 15f);
        public float spawnY = 1f;
        public float objectScale = 1.2f;

        [Tooltip("Keep spawns at least this far (XZ) from the world origin / central furnace.")]
        public float avoidCenterRadius = 0f;

        Grabbable redInstance;
        Grabbable blueInstance;

        void Start()
        {
            EnsureColor(GrabColor.Red);
            EnsureColor(GrabColor.Blue);
        }

        void Update()
        {
            // Self-healing: if something destroyed an object externally, respawn it.
            if (redInstance == null) EnsureColor(GrabColor.Red);
            if (blueInstance == null) EnsureColor(GrabColor.Blue);
        }

        public void EnsureColor(GrabColor color)
        {
            Grabbable g = Spawn(color);
            if (color == GrabColor.Red) redInstance = g;
            else blueInstance = g;
        }

        Grabbable Spawn(GrabColor color)
        {
            GameObject go = GameObject.CreatePrimitive(
                color == GrabColor.Red ? PrimitiveType.Cube : PrimitiveType.Sphere);
            go.name = color == GrabColor.Red ? "Cube" : "Sphere";
            go.transform.localScale = Vector3.one * objectScale;
            go.transform.position = RandomPoint();

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = color == GrabColor.Red ? redMaterial : blueMaterial;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var grab = go.AddComponent<Grabbable>();
            grab.color = color;

            return grab;
        }

        Vector3 RandomPoint()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = Random.Range(areaMin.x, areaMax.x);
                float z = Random.Range(areaMin.y, areaMax.y);
                if (avoidCenterRadius <= 0f || new Vector2(x, z).magnitude >= avoidCenterRadius)
                    return new Vector3(x, spawnY, z);
            }
            // Fallback: push out along +Z past the exclusion zone.
            return new Vector3(0f, spawnY, Mathf.Max(avoidCenterRadius, areaMax.y));
        }
    }
}
