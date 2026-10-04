using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

// プレイヤー中心・北が上のミニマップ（README 13章 ミニマップ）
// 地図はエディタで撮影した静止画（minimap_village.png）の表示範囲を動かしてスクロールさせる
public class MinimapUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform viewport;   // アイコンを置く表示領域（RectMask2Dで切り抜く）
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private Sprite npcIconSprite;

    [Header("地図画像が写している範囲（ワールド座標）")]
    [SerializeField] private Vector2 mapWorldCenter = new Vector2(11f, 128f);
    [SerializeField] private float mapWorldSize = 128f;

    [Header("ミニマップに表示する範囲（一辺のm）")]
    [SerializeField] private float viewWorldSize = 30f;

    [Header("アイコン")]
    [SerializeField] private float npcIconSize = 10f;
    [SerializeField] private float clientIconSize = 14f;
    [SerializeField] private Color npcIconColor = Color.white;
    [SerializeField] private float edgeMargin = 8f;

    private Transform player;

    private class NpcIcon
    {
        public Transform npc;
        public RectTransform icon;
        public bool isClient;
    }

    private readonly List<NpcIcon> npcIcons = new();

    private void Start()
    {
        var controller = FindAnyObjectByType<ThirdPersonController>();
        if (controller != null)
            player = controller.transform;

        foreach (var npc in FindObjectsByType<NPCInteraction>(FindObjectsSortMode.None))
        {
            bool isClient = npc.IsQuestClient();

            var go = new GameObject("Icon_" + npc.name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(viewport, false);

            var image = go.AddComponent<Image>();
            image.sprite = npcIconSprite;
            image.color = isClient ? npc.ClientColor : npcIconColor;
            image.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            float size = isClient ? clientIconSize : npcIconSize;
            rt.sizeDelta = new Vector2(size, size);

            npcIcons.Add(new NpcIcon { npc = npc.transform, icon = rt, isClient = isClient });
        }

        // プレイヤーと依頼人のアイコンを一番手前に
        foreach (var n in npcIcons)
            if (n.isClient) n.icon.SetAsLastSibling();
        playerIcon.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector2 playerPos = new Vector2(player.position.x, player.position.z);

        // 地図のスクロール：プレイヤー位置を中心にviewWorldSize分だけ表示
        float viewUV = viewWorldSize / mapWorldSize;
        Vector2 centerUV = (playerPos - mapWorldCenter) / mapWorldSize + new Vector2(0.5f, 0.5f);
        mapImage.uvRect = new Rect(centerUV.x - viewUV / 2f, centerUV.y - viewUV / 2f, viewUV, viewUV);

        // プレイヤーの向き（北が上なので、ワールドのY回転をそのままUIの回転に）
        playerIcon.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);

        float pixelsPerMeter = viewport.rect.width / viewWorldSize;
        float half = viewport.rect.width / 2f - edgeMargin;

        foreach (var n in npcIcons)
        {
            Vector2 npcPos = new Vector2(n.npc.position.x, n.npc.position.z);
            Vector2 offset = (npcPos - playerPos) * pixelsPerMeter;

            bool inside = Mathf.Abs(offset.x) <= half && Mathf.Abs(offset.y) <= half;

            if (!inside)
            {
                if (!n.isClient)
                {
                    n.icon.gameObject.SetActive(false);
                    continue;
                }

                // 依頼人だけは範囲外でも縁に止めて方向を示す
                offset *= half / Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
            }

            n.icon.gameObject.SetActive(true);
            n.icon.anchoredPosition = offset;
        }
    }
}
