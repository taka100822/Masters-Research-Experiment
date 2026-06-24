using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    private DialogueManager dialogueManager;
    private NPCDialogue npcDialogue;

    // [SerializeField] private GameObject exclamationUI;

    private void Start()
    {
        dialogueManager = FindAnyObjectByType<DialogueManager>();
        npcDialogue = GetComponent<NPCDialogue>();

        // exclamationUI.transform.position =
        // Camera.main.transform.position + Camera.main.transform.forward * 2f;
        // exclamationUI.SetActive(true);
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