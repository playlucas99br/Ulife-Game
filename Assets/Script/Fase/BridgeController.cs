using System.Collections;
using UnityEngine;

namespace FaseLucasGame
{
    /// <summary>
    /// A walkable bridge that starts retracted and extends between Spawn and Final
    /// once the challenge is complete. The deck (and any extra parts such as rails) grow
    /// along their local Z axis. Extending also removes the spawn doorway barrier.
    /// </summary>
    public class BridgeController : MonoBehaviour
    {
        public Transform deck;             // the visual/collider plank that scales
        public Transform[] extras;         // rails etc. that scale in Z together with the deck
        public GameObject barrier;         // energy field removed once the bridge is ready

        public float retractedLength = 0.1f;
        public float extendedLength = 1f;  // local scale Z when fully extended
        public float extendDuration = 3f;

        bool extended;

        void Awake()
        {
            ApplyRetracted();
        }

        public void ApplyRetracted()
        {
            SetLength(retractedLength);
        }

        void SetLength(float z)
        {
            if (deck != null)
            {
                var s = deck.localScale; s.z = z; deck.localScale = s;
            }
            if (extras != null)
            {
                foreach (var t in extras)
                {
                    if (t == null) continue;
                    var s = t.localScale; s.z = z; t.localScale = s;
                }
            }
        }

        public void Extend()
        {
            if (extended) return;
            extended = true;
            if (barrier != null) barrier.SetActive(false);
            StartCoroutine(ExtendRoutine());
        }

        IEnumerator ExtendRoutine()
        {
            float t = 0f;
            float startZ = deck != null ? deck.localScale.z : retractedLength;
            while (t < extendDuration)
            {
                t += Time.deltaTime;
                float z = Mathf.Lerp(startZ, extendedLength, Mathf.SmoothStep(0f, 1f, t / extendDuration));
                SetLength(z);
                yield return null;
            }
            SetLength(extendedLength);
        }
    }
}
