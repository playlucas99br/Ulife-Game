using UnityEngine;
using UnityEngine.SceneManagement;

namespace FaseLucasGame
{
    /// <summary>
    /// "Portal saida" - when the player walks through it, loads the industrial level.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PortalSaida : MonoBehaviour
    {
        public string targetScene = "Industrial_Zone";

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (Application.CanStreamedLevelBeLoaded(targetScene))
            {
                PlayerFPS.LockCursor(false);
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogWarning($"PortalSaida: a cena '{targetScene}' não está nas Build Settings.");
            }
        }
    }
}
