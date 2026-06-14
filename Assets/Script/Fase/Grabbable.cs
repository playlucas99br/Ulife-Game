using UnityEngine;

namespace FaseLucasGame
{
    public enum GrabColor { Red, Blue }

    /// <summary>
    /// Marks a physics object that the magnet can pick up and that the incinerator can score.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        public GrabColor color = GrabColor.Red;
        [HideInInspector] public bool held;

        public Rigidbody Body { get; private set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        /// <summary>Numeric encoding used by the magnet's "color below" sensor: 0 none, 1 red, 2 blue.</summary>
        public int ColorCode => color == GrabColor.Red ? 1 : 2;
    }
}
