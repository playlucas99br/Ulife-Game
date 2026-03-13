using UnityEngine;

public class PickObject : MonoBehaviour
{
    public GameObject interactionText;
    public Transform holdPoint;

    static bool holdingItem = false;

    bool playerNear = false;
    bool picked = false;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            if(!picked && playerNear && !holdingItem)
            {
                Pick();
            }
            else if(picked)
            {
                Drop();
            }
        }
    }

    void Pick()
    {
        picked = true;
        holdingItem = true;

        transform.SetParent(holdPoint);
        transform.localPosition = new Vector3(0,0,1);
        transform.localRotation = Quaternion.identity;

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

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player") && !holdingItem)
        {
            playerNear = true;
            interactionText.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            playerNear = false;
            interactionText.SetActive(false);
        }
    }
}