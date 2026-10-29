using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

// TutorialScene（README 2.4）をMainSceneから作るツール
// メニュー「Tools/研究用/チュートリアルのシーンを作り直す」から実行する。Play中は実行できない
// MainSceneを複製し、村（Background・WaterBlockers）を消して、平らな草地・見えない壁・案内役2人・「やること」の札を置く
public static class TutorialSceneBuilder
{
    private const string MenuPath = "Tools/研究用/チュートリアルのシーンを作り直す";

    private const string MainScenePath = "Assets/MainScene.unity";
    private const string TutorialScenePath = "Assets/TutorialScene.unity";
    private const string GroundMaterialPath = "Assets/Material/TutorialGround.mat";
    private const string MinimapImagePath = "Assets/Texture/minimap_tutorial.png";

    private const float AreaSize = 30f;      // 歩ける範囲（一辺のm）
    private const float GroundSize = 40f;    // 地面とミニマップの地図の大きさ（一辺のm）
    private const float WallHeight = 6f;

    private static readonly Color GroundColor = new Color(0.42f, 0.62f, 0.32f);

    // 案内役（README 2.4）。MainSceneのNPC001・NPC002を作り替える
    private static readonly (string sourceId, string id, string name, Vector2 offset)[] Guides =
    {
        ("NPC001", "TUT001", "案内人ケビン", new Vector2(-3f, 6f)),   // offset：プレイヤーから見て（右, 前）m
        ("NPC002", "TUT002", "旅人クリス", new Vector2(5f, 11f)),
    };

    [MenuItem(MenuPath)]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("チュートリアル", "Playを止めてから実行してください。", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) != null &&
            !EditorUtility.DisplayDialog("チュートリアル", TutorialScenePath + " を作り直します（今の中身は消えます）。", "作り直す", "やめる"))
            return;

        // 作り直すときは中身だけを上書きして、guid（ビルド設定が参照する）を変えない
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) != null)
        {
            System.IO.File.Copy(MainScenePath, TutorialScenePath, true);
            AssetDatabase.ImportAsset(TutorialScenePath, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(MainScenePath, TutorialScenePath))
        {
            EditorUtility.DisplayDialog("チュートリアル", MainScenePath + " を複製できませんでした。", "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        try
        {
            BuildScene(scene);
        }
        catch (System.Exception e)
        {
            // 作りかけのシーンは捨てる（MainSceneの複製のまま残ると、チュートリアルの代わりに村が開いてしまう）
            Debug.LogException(e);
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(TutorialScenePath);
            EditorUtility.DisplayDialog("チュートリアル", "シーンを作れませんでした（TutorialSceneは消しました）。Consoleのエラーを見てください。\n" + e.Message, "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();

        Debug.Log("[Tools] Tutorial scene built: " + TutorialScenePath);
    }

    // 名前が同じものがシーンの中に他にもある（村人の頭上の「Canvas」、スライダーの「Background」など）ので、
    // 最上位のオブジェクトだけから探す
    private static GameObject Root(UnityEngine.SceneManagement.Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
    }

    private static void BuildScene(UnityEngine.SceneManagement.Scene scene)
    {
        var player = Object.FindAnyObjectByType<ThirdPersonController>().transform;
        Vector3 origin = player.position;
        // 消す前の村の地面の高さに草地を置く（プレイヤーとカメラは動かさない）。プレイヤー自身の当たり判定は除く
        var hits = Physics.RaycastAll(origin + Vector3.up * 5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !(h.collider is CharacterController) && !h.collider.transform.IsChildOf(player))
            .OrderBy(h => h.distance);
        if (hits.Any())
            origin.y = hits.First().point.y;

        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        foreach (var name in new[] { "Background", "WaterBlockers" })
        {
            var go = Root(scene, name);
            if (go != null) Object.DestroyImmediate(go);
        }

        BuildGround(origin);
        BuildNpcs(Root(scene, "NPCs"), origin, forward, right);
        var guide = SetUpGameManager();
        SetUpCanvas(Root(scene, "Canvas"), guide);
        SetUpMinimap(origin);
    }

    // =========================
    // 草地と見えない壁
    // =========================

    private static void BuildGround(Vector3 origin)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TutorialGround";
        ground.transform.position = origin;
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);   // Planeは一辺10m
        ground.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial();

        var walls = new GameObject("TutorialWalls");
        float half = AreaSize / 2f;
        var sides = new[]
        {
            (new Vector3(0, 0, half), new Vector3(AreaSize, WallHeight, 1f)),
            (new Vector3(0, 0, -half), new Vector3(AreaSize, WallHeight, 1f)),
            (new Vector3(half, 0, 0), new Vector3(1f, WallHeight, AreaSize)),
            (new Vector3(-half, 0, 0), new Vector3(1f, WallHeight, AreaSize)),
        };
        foreach (var (pos, size) in sides)
        {
            var wall = new GameObject("Wall");
            wall.transform.SetParent(walls.transform, false);
            wall.transform.position = origin + pos + Vector3.up * (WallHeight / 2f);
            wall.AddComponent<BoxCollider>().size = size;
        }
    }

    private static Material GroundMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (mat != null)
            return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetColor("_BaseColor", GroundColor);
        mat.color = GroundColor;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, GroundMaterialPath);
        return mat;
    }

    // =========================
    // 案内役の村人
    // =========================

    private static void BuildNpcs(GameObject root, Vector3 origin, Vector3 forward, Vector3 right)
    {
        var all = root.GetComponentsInChildren<NPCDialogue>(true);

        foreach (var npc in all)
        {
            var guide = Guides.FirstOrDefault(g => g.sourceId == npc.npcId);
            Transform top = TopUnder(npc.transform, root.transform);

            if (guide.id == null)
            {
                Object.DestroyImmediate(top.gameObject);
                continue;
            }

            top.name = guide.id;
            Vector3 pos = origin + right * guide.offset.x + forward * guide.offset.y;
            top.position = pos;
            top.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(origin - pos, Vector3.up));

            npc.npcId = guide.id;
            npc.displayName = guide.name;
            npc.set2DisplayName = "";
            npc.csvFileName = guide.id;
            npc.promptData = null;

            // 衣装は「今回の村では使わない方」（もう一方の話題セットの衣装）にする（README 2.4）
            if (npc.costumesBySet != null && npc.costumesBySet.Length == 2)
                npc.costumesBySet = new[] { npc.costumesBySet[1], npc.costumesBySet[0] };

            EditorUtility.SetDirty(npc);
        }
    }

    private static Transform TopUnder(Transform t, Transform root)
    {
        while (t.parent != null && t.parent != root)
            t = t.parent;
        return t;
    }

    // =========================
    // GameManager（制限時間を外し、TutorialGuideを付ける）
    // =========================

    private static TutorialGuide SetUpGameManager()
    {
        var gm = Object.FindAnyObjectByType<GameManager>().gameObject;

        var timer = gm.GetComponent<ExplorationTimer>();
        if (timer != null)
            Object.DestroyImmediate(timer);

        var guide = gm.GetComponent<TutorialGuide>();
        return guide != null ? guide : gm.AddComponent<TutorialGuide>();
    }

    // =========================
    // 画面（説明画面・残り時間を消し、「やること」の札を足す）
    // =========================

    private static void SetUpCanvas(GameObject canvasRoot, TutorialGuide guide)
    {
        var canvas = canvasRoot.transform;

        var intro = canvas.Find("IntroPanel");
        if (intro != null) intro.gameObject.SetActive(false);

        var dm = Object.FindAnyObjectByType<DialogueManager>();
        var dmSo = new SerializedObject(dm);
        dmSo.FindProperty("introPanel").objectReferenceValue = null;
        dmSo.ApplyModifiedPropertiesWithoutUndo();

        // 札は残り時間の窓（樫色＋真鍮の縁）を複製して作る
        var timerPanel = canvas.Find("TimerPanel");
        timerPanel.gameObject.SetActive(false);

        var panel = Object.Instantiate(timerPanel.gameObject, canvas);
        panel.name = "TutorialGuidePanel";
        panel.SetActive(true);
        panel.transform.SetSiblingIndex(timerPanel.GetSiblingIndex() + 1);   // 設定パネルより後ろ

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -16f);
        rt.sizeDelta = new Vector2(720f, 96f);

        var text = panel.GetComponentInChildren<TMP_Text>(true);
        text.name = "GuideText";
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(24f, 10f);
        trt.offsetMax = new Vector2(-24f, -10f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.text = "";

        var so = new SerializedObject(guide);
        so.FindProperty("guidePanel").objectReferenceValue = panel;
        so.FindProperty("guideText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =========================
    // ミニマップ（草地の地図）
    // =========================

    private static void SetUpMinimap(Vector3 origin)
    {
        var minimap = Object.FindAnyObjectByType<MinimapUI>(FindObjectsInactive.Include);
        if (minimap == null)
            return;

        var tex = MinimapTexture();
        var so = new SerializedObject(minimap);
        so.FindProperty("mapWorldCenter").vector2Value = new Vector2(origin.x, origin.z);
        so.FindProperty("mapWorldSize").floatValue = GroundSize;
        var image = so.FindProperty("mapImage").objectReferenceValue as RawImage;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (image != null)
        {
            image.texture = tex;
            EditorUtility.SetDirty(image);
        }
    }

    // 草地（緑）と、歩ける範囲の境目（濃い線）だけの地図
    private static Texture2D MinimapTexture()
    {
        const int res = 256;
        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        var outside = GroundColor * 0.7f;
        var line = new Color(0.25f, 0.2f, 0.15f);
        float edge = AreaSize / GroundSize / 2f;   // 中心から境目までの割合

        var pixels = new List<Color>(res * res);
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / res - 0.5f), v = Mathf.Abs((y + 0.5f) / res - 0.5f);
                float m = Mathf.Max(u, v);
                pixels.Add(Mathf.Abs(m - edge) < 1.5f / res ? line : m < edge ? GroundColor : outside);
            }
        }
        tex.SetPixels(pixels.ToArray());
        tex.Apply();

        System.IO.File.WriteAllBytes(MinimapImagePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(MinimapImagePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(MinimapImagePath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(MinimapImagePath);
    }

    // =========================
    // ビルド設定
    // =========================

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(s => s.path == TutorialScenePath);
        if (index >= 0)
            scenes.RemoveAt(index);   // guidを今のものに入れ直す
        else
            index = Mathf.Min(1, scenes.Count);   // タイトルの次（1番）に入れる

        scenes.Insert(index, new EditorBuildSettingsScene(TutorialScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
