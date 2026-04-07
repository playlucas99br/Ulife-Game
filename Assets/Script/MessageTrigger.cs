using UnityEngine;

public class MessageTrigger : MonoBehaviour
{
    public string mensagem = "O jogador entrou na área!";

    public Color corDoGizmo = new Color(0, 1, 0, 0.3f);

    private void OnTriggerEnter(Collider other){
        if (other.CompareTag("Player")){
            Debug.Log("<color=cyan>[Trigger]</color> " + mensagem);
        }
    }

    private void OnDrawGizmos(){
        Gizmos.color = corDoGizmo;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        Gizmos.color = new Color(corDoGizmo.r, corDoGizmo.g, corDoGizmo.b, 1f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
