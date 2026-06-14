using UnityEngine;
using UnityEngine.UI;

namespace FaseLucasGame
{
    /// <summary>
    /// Tracks how many red and blue objects have been incinerated. The challenge is
    /// complete when both reach the target, which retracts the magnet and extends the bridge.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance;

        [Header("Goal")]
        public int targetPerColor = 3;

        [Header("Wiring")]
        public MagnetController magnet;
        public BridgeController bridge;
        public Text scoreText;

        public int RedScore { get; private set; }
        public int BlueScore { get; private set; }
        public bool Completed { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            RefreshUI();
        }

        public void AddScore(GrabColor color)
        {
            if (Completed) return;

            if (color == GrabColor.Red) RedScore = Mathf.Min(RedScore + 1, targetPerColor);
            else BlueScore = Mathf.Min(BlueScore + 1, targetPerColor);

            RefreshUI();

            if (RedScore >= targetPerColor && BlueScore >= targetPerColor)
                Complete();
        }

        void Complete()
        {
            Completed = true;
            if (scoreText != null)
                scoreText.text = "DESAFIO COMPLETO!  A ponte se estende.";

            if (magnet != null) magnet.Retract();
            if (bridge != null) bridge.Extend();
        }

        void RefreshUI()
        {
            if (scoreText == null) return;
            scoreText.text = $"Vermelhos: {RedScore}/{targetPerColor}    Azuis: {BlueScore}/{targetPerColor}";
        }
    }
}
