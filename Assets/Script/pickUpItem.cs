using UnityEngine;

public class PickObject : MonoBehaviour
{
    public GameObject interactionText;
    public Transform holdPoint;
    public Camera playerCamera;
    public float interactDistance = 3f;

    static bool holdingItem = false;

    bool playerNear = false;
    bool picked = false;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        // Tecla E: pegar ou soltar
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!picked && playerNear && !holdingItem)
            {
                Pick();
            }
            else if (picked)
            {
                Drop();
            }
        }

        // Clique esquerdo: encaixar
        if (Input.GetMouseButtonDown(0))
        {
            TryPlace();
        }
    }

    void Pick()
    {
        picked = true;
        holdingItem = true;

        transform.SetParent(holdPoint, false); 
        transform.localPosition = new Vector3(0, 0, 1);
        transform.localRotation = Quaternion.Euler(0, 270, 0);

        rb.useGravity = false;
        rb.isKinematic = true;

        interactionText.SetActive(false);
    }

    void Drop()
    {
        picked = false;
        holdingItem = false;

        transform.SetParent(null);

        rb.useGravity = true;
        rb.isKinematic = false;
    }

    void TryPlace()
    {
        if (!picked)
        {
            Debug.Log("Não está segurando item");
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        int layerMask = LayerMask.GetMask("LogicSlot");
        Debug.Log("LayerMask: " + layerMask);

        if (Physics.Raycast(ray, out hit, interactDistance, layerMask))
        {
            Debug.Log("Raycast acertou algo na layer!");

            Debug.Log("Objeto: " + hit.collider.name);
            Debug.Log("Tag: " + hit.collider.tag);
            Debug.Log("Layer: " + LayerMask.LayerToName(hit.collider.gameObject.layer));

            Transform slot = hit.collider.GetComponentInParent<Transform>();

            if (slot != null)
            {
                Debug.Log("Slot encontrado: " + slot.name);

                Transform snapPoint = slot.Find("SnapPoint");

                if (snapPoint != null)
                {
                    Debug.Log("SnapPoint encontrado!");
                    PlaceOnSlot(snapPoint, slot.name);
                }
                else
                {
                    Debug.Log("SnapPoint NÃO encontrado!");
                }
            }
            else
            {
                Debug.Log("Slot NULL!");
            }
        }
        else
        {
            Debug.Log("Raycast NÃO acertou nada na layer LogicSlot");
        }
    }

    void PlaceOnSlot(Transform snapPoint, string slotName)
    {
        picked = false;
        holdingItem = false;

        transform.SetParent(snapPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0, 270, 0);

        rb.useGravity = false;
        rb.isKinematic = true;

        CheckLogicGatePlacement(slotName);
    }

    void CheckLogicGatePlacement(string slotName)
    {
        string heldItemName = gameObject.name.ToLower();
        
        if (slotName.Contains("molduraLogicaOr") && heldItemName.Contains("or") && !heldItemName.Contains("xor"))
        {
            DestroyGateBox("caixaComponentesPortaOr");
        }
        else if (slotName.Contains("molduraLogicaAnd") && heldItemName.Contains("and"))
        {
            DestroyGateBox("caixaComponentesPortaAnd");
        }
        else if (slotName.Contains("molduraLogicaXor") && heldItemName.Contains("xor"))
        {
            DestroyGateBox("caixaComponentesPortaXor");
        }
    }

    void DestroyGateBox(string boxName)
    {
        GameObject box = GameObject.Find(boxName);
        if (box != null)
        {
            Destroy(box);
            Debug.Log(boxName + " deletada com sucesso!");
        }
        else
        {
            Debug.Log("Não foi possível encontrar " + boxName);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !holdingItem)
        {
            playerNear = true;
            interactionText.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            interactionText.SetActive(false);
        }
    }
}