using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private GameObject bubbleUI;

    private void Start()
    {
        if (bubbleUI != null)
            bubbleUI.SetActive(false);
    }

    public void SetBubbleVisible(bool visible)
    {
        if (bubbleUI != null)
            bubbleUI.SetActive(visible);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bubbleUI.SetActive(true);

            FindAnyObjectByType<DialogueManager>()
                .SetCurrentNPC(GetComponent<NPCDialogue>());

            Debug.Log("NPC SET");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bubbleUI.SetActive(false);

            FindAnyObjectByType<DialogueManager>()
                .SetCurrentNPC(null);

            Debug.Log("NPC CLEARED");
        }
    }
}