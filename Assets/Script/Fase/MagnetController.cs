using UnityEngine;

namespace FaseLucasGame
{
    /// <summary>
    /// The "Ima" (magnet). Hangs from "Teto" by a rope and is driven by the visual program.
    /// XYZ velocity commands move it inside a clamped play volume; it can grab/release
    /// nearby Cube/Sphere physics objects with a FixedJoint and exposes sensors.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MagnetController : MonoBehaviour
    {
        [Header("Anchor / Rope")]
        public Transform ceilingAnchor;        // attach point on Teto
        public LineRenderer rope;

        [Header("Play volume (world space)")]
        public Vector3 areaMin = new Vector3(-18f, 2.5f, -18f);
        public Vector3 areaMax = new Vector3(18f, 11f, 18f);

        [Header("Movement")]
        public float maxSpeed = 17f;

        [Header("Grab")]
        public float grabRadius = 2.4f;
        public Transform grabPoint;            // bottom of the magnet
        public LayerMask grabbableMask = ~0;

        Rigidbody rb;
        FixedJoint joint;
        Grabbable held;

        // sensor cache
        public Vector3 SensorPosition => transform.position;
        public int SensorBelowColor { get; private set; }      // 0 none, 1 red, 2 blue
        public float SensorBelowDistance { get; private set; }
        public bool IsHolding => held != null;

        bool retracting;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void Update()
        {
            UpdateRope();
        }

        void UpdateRope()
        {
            if (rope == null || ceilingAnchor == null) return;
            rope.positionCount = 2;
            // Gantry-style: the cable drops straight down from the ceiling above the magnet.
            rope.SetPosition(0, new Vector3(transform.position.x, ceilingAnchor.position.y, transform.position.z));
            rope.SetPosition(1, transform.position);
        }

        /// <summary>Called by the program runtime each physics step.</summary>
        public void Tick(Vector3 velocityCommand, bool grab, bool release)
        {
            if (retracting) return;

            // If a carried object was destroyed externally (the incinerator burns it while it is
            // still held), drop the now-dangling joint so the next grab starts clean.
            if (joint != null && held == null)
            {
                Destroy(joint);
                joint = null;
            }

            Vector3 v = Vector3.ClampMagnitude(velocityCommand, maxSpeed);
            Vector3 target = transform.position + v * Time.fixedDeltaTime;
            target.x = Mathf.Clamp(target.x, areaMin.x, areaMax.x);
            target.y = Mathf.Clamp(target.y, areaMin.y, areaMax.y);
            target.z = Mathf.Clamp(target.z, areaMin.z, areaMax.z);
            rb.MovePosition(target);

            if (grab && !IsHolding) TryGrab();
            if (release && IsHolding) Release();

            UpdateBelowSensor();
        }

        void UpdateBelowSensor()
        {
            Vector3 origin = grabPoint != null ? grabPoint.position : transform.position;
            const float sensorRadius = 0.8f;
            // Sweep a fat sphere straight down instead of a thin ray, so an object that is NEAR
            // below (not only dead-centre) still registers its colour/distance. Start a touch
            // above the grab-point so the sweep doesn't begin already overlapping the object
            // directly beneath it (initial overlaps report distance 0 and no usable surface).
            float lift = sensorRadius + 0.25f;
            Vector3 castOrigin = origin + Vector3.up * lift;
            if (Physics.SphereCast(castOrigin, sensorRadius, Vector3.down, out RaycastHit hit, 100f,
                    grabbableMask, QueryTriggerInteraction.Ignore))
            {
                SensorBelowDistance = Mathf.Max(0f, hit.distance - lift);
                Grabbable g = hit.collider.GetComponentInParent<Grabbable>();
                SensorBelowColor = (g != null) ? g.ColorCode : 0;   // 0 none, 1 red, 2 blue
            }
            else
            {
                SensorBelowDistance = 999f;
                SensorBelowColor = 0;
            }
        }

        void TryGrab()
        {
            Vector3 center = grabPoint != null ? grabPoint.position : transform.position;
            Collider[] hits = Physics.OverlapSphere(center, grabRadius, grabbableMask, QueryTriggerInteraction.Ignore);
            Grabbable best = null;
            float bestDist = float.MaxValue;
            foreach (var c in hits)
            {
                Grabbable g = c.GetComponentInParent<Grabbable>();
                if (g == null || g.held) continue;
                float d = Vector3.Distance(center, g.transform.position);
                if (d < bestDist) { bestDist = d; best = g; }
            }
            if (best == null) return;

            held = best;
            held.held = true;
            held.Body.isKinematic = false;
            joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = held.Body;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }

        public void Release()
        {
            if (joint != null) Destroy(joint);
            joint = null;
            if (held != null)
            {
                held.held = false;
                held = null;
            }
        }

        /// <summary>Called when the challenge is complete: drop anything and rise to the ceiling.</summary>
        public void Retract()
        {
            Release();
            retracting = true;
            StopAllCoroutines();
            StartCoroutine(RetractRoutine());
        }

        System.Collections.IEnumerator RetractRoutine()
        {
            Vector3 start = transform.position;
            // Rise straight up to the ceiling (gantry pulls the cable in).
            float topY = ceilingAnchor != null ? ceilingAnchor.position.y - 1f : start.y + 8f;
            Vector3 end = new Vector3(start.x, topY, start.z);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 0.5f;
                rb.MovePosition(Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t)));
                yield return new WaitForFixedUpdate();
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Vector3 c = (areaMin + areaMax) * 0.5f;
            Gizmos.DrawWireCube(c, areaMax - areaMin);
            if (grabPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(grabPoint.position, grabRadius);
            }
        }
    }
}
