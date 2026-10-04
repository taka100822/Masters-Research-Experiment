using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private GameObject bubbleUI;

    [Tooltip("頭上に常に表示する名前（README 19.6.9）")]
    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("今のクエストの依頼人のときの吹き出しと名前の色（README 19.6.8）")]
    [FormerlySerializedAs("clientBubbleColor")]
    [SerializeField] private Color clientColor = new Color(1f, 0.82f, 0.2f, 1f);

    private Image bubbleImage;
    private Color defaultBubbleColor;

    public Color ClientColor => clientColor;

    private void Start()
    {
        var npc = GetComponent<NPCDialogue>();
        if (nameLabel != null && npc != null)
        {
            nameLabel.text = npc.DisplayName;

            if (IsQuestClient())
                nameLabel.color = clientColor;
        }

        if (bubbleUI != null)
        {
            bubbleImage = bubbleUI.GetComponent<Image>();
            if (bubbleImage != null)
                defaultBubbleColor = bubbleImage.color;

            bubbleUI.SetActive(false);
        }
    }

    public void SetBubbleVisible(bool visible)
    {
        if (bubbleUI != null)
        {
            if (visible)
                ApplyBubbleColor();

            bubbleUI.SetActive(visible);
        }
    }

    // 表示する時点で判定する（QuestManagerのクエスト読み込み後であることを保証するため）
    private void ApplyBubbleColor()
    {
        if (bubbleImage == null)
            return;

        bubbleImage.color = IsQuestClient() ? clientColor : defaultBubbleColor;
    }

    public bool IsQuestClient()
    {
        var quest = QuestManager.Instance != null ? QuestManager.Instance.Quest : null;
        var npc = GetComponent<NPCDialogue>();

        return quest != null && npc != null && quest.clientNpcId == npc.npcId;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SetBubbleVisible(true);

            FindAnyObjectByType<DialogueManager>()
                .SetCurrentNPC(GetComponent<NPCDialogue>());

            Debug.Log("NPC SET");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SetBubbleVisible(false);

            FindAnyObjectByType<DialogueManager>()
                .SetCurrentNPC(null);

            Debug.Log("NPC CLEARED");
        }
    }
}
