using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private GameObject bubbleUI;

    private void Start()
    {
        bubbleUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bubbleUI.SetActive(true);

            FindAnyObjectByType<DialogueManager>()
                .SetCurrentNPC(GetComponent<NPCDialogue>());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bubbleUI.SetActive(false);
        }
    }
}