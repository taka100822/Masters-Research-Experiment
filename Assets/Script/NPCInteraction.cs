using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    private DialogueManager dialogueManager;
    private NPCDialogue npcDialogue;

    private void Start()
    {
        dialogueManager = FindAnyObjectByType<DialogueManager>();
        npcDialogue = GetComponent<NPCDialogue>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.SetCurrentNPC(npcDialogue);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.SetCurrentNPC(null);
        }
    }
}