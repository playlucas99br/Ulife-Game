using System.Collections;
using UnityEngine;

namespace FaseLucasGame
{
    /// <summary>
    /// Trigger volume. When a grabbable object is dropped in, it is destroyed in a flash of
    /// light, the score increases and the spawner creates a replacement of the same color.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Incinerator : MonoBehaviour
    {
        public ObjectSpawner spawner;
        public Light flashLight;
        public float flashIntensity = 30f;
        public float flashDuration = 0.4f;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            Grabbable g = other.GetComponentInParent<Grabbable>();
            if (g == null) return;

            GrabColor color = g.color;

            // Release from magnet if it was somehow still held.
            g.held = false;

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(color);

            StartCoroutine(Flash());

            Destroy(g.gameObject);

            if (spawner != null)
                spawner.EnsureColor(color);
        }

        IEnumerator Flash()
        {
            if (flashLight == null) yield break;
            flashLight.enabled = true;
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                flashLight.intensity = Mathf.Lerp(flashIntensity, 0f, t / flashDuration);
                yield return null;
            }
            flashLight.intensity = 0f;
            flashLight.enabled = false;
        }
    }
}
