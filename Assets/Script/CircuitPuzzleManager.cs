using UnityEngine;
using System.Collections;

public class CircuitPuzzleManager : MonoBehaviour
{
    public static CircuitPuzzleManager Instance;

    [Header("Portas")]
    public GameObject porta1;
    public GameObject porta2;

    [Header("Configurações de Movimento")]
    public float targetZPorta1 = -22.16f;
    public float targetZPorta2 = -15.42f;
    public float slideDuration = 2.0f;

    private bool capacitorPlaced = false;
    private bool ledPlaced = false;
    private bool resistorPlaced = false;
    private bool opened = false;

    public void Awake(){
        if (Instance == null){
            Instance = this;
        }else{
            Destroy(gameObject);
        }
    }

    public void ReportPlacement(string itemName, string slotName){
        itemName = itemName.ToLower();
        slotName = slotName.ToLower();

        // Verifica se o item correto foi colocado na moldura correta
        if (slotName.Contains("molduracapacitor") && itemName.Contains("capacitor")){
            capacitorPlaced = true;
            Debug.Log("Capacitor colocado corretamente!");
        }
        else if (slotName.Contains("molduraled") && itemName.Contains("led")){
            ledPlaced = true;
            Debug.Log("LED colocado corretamente!");
        }
        else if (slotName.Contains("molduraresistor") && itemName.Contains("resistor")){
            resistorPlaced = true;
            Debug.Log("Resistor colocado corretamente!");
        }

        // Se os três estiverem no lugar, abre as portas
        if (capacitorPlaced && ledPlaced && resistorPlaced && !opened){
            opened = true;
            StartCoroutine(SlideDoors());
        }
    }

    public IEnumerator SlideDoors(){
        Debug.Log("Todos os itens colocados! Abrindo portas...");

        if (porta1 == null || porta2 == null){
            Debug.LogError("CircuitPuzzleManager: Portas não atribuídas no Inspector!");
            yield break;
        }

        Vector3 startPos1 = porta1.transform.localPosition;
        Vector3 startPos2 = porta2.transform.localPosition;

        Vector3 endPos1 = new Vector3(startPos1.x, startPos1.y, targetZPorta1);
        Vector3 endPos2 = new Vector3(startPos2.x, startPos2.y, targetZPorta2);

        float elapsed = 0;
        while (elapsed < slideDuration){
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            
            // transição
            float smoothT = Mathf.SmoothStep(0, 1, t);

            porta1.transform.localPosition = Vector3.Lerp(startPos1, endPos1, smoothT);
            porta2.transform.localPosition = Vector3.Lerp(startPos2, endPos2, smoothT);
            
            yield return null;
        }

        porta1.transform.localPosition = endPos1;
        porta2.transform.localPosition = endPos2;
        
        Debug.Log("Portas abertas com sucesso!");
    }
}
