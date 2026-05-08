using UnityEngine;

public class FloatingText : MonoBehaviour
{
    public float amplitude = 0.5f; // altura do movimento
    public float frequency = 2f;   // velocidade da oscilação

    private Vector3 startPos;

    public void Start()
    {
        startPos = transform.position;
    }

    public void Update()
    {
        float offsetY = Mathf.Sin(Time.time * frequency) * amplitude;
        transform.position = startPos + new Vector3(0, offsetY, 0);
    }
}