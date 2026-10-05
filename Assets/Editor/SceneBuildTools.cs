using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// MainSceneの作り直し用ツール（README 3.4・7.6）
// メニュー「Tools/研究用」から実行する。Play中は実行できない
public static class SceneBuildTools
{
    private const string MenuRoot = "Tools/研究用/";

    // =========================
    // 水に入れないようにする壁（README 7.6）
    // =========================

    private const string WaterPath = "Background/Landscape/Water";
    private const string BlockersName = "WaterBlockers";
    private const float Cell = 0.5f;          // 調べる間隔（m）
    private const float BlockerHeight = 6f;
    private const int WaterLayer = 4;         // カメラの衝突判定に含まれないレイヤー

    [MenuItem(MenuRoot + "水の壁を作り直す")]
    public static void RebuildWaterBlockers()
    {
        if (!CheckNotPlaying()) return;

        var water = GameObject.Find(WaterPath);
        if (water == null)
        {
            EditorUtility.DisplayDialog("水の壁", WaterPath + " が見つかりません。MainSceneを開いてください。", "OK");
            return;
        }

        // 古い壁は判定から外すため先に消す
        var old = GameObject.Find(BlockersName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        Bounds wb = water.GetComponent<Renderer>().bounds;
        float waterY = wb.max.y;
        float minX = Mathf.Floor(wb.min.x) - 1, minZ = Mathf.Floor(wb.min.z) - 1;
        int nx = Mathf.CeilToInt((wb.size.x + 2) / Cell) + 1;
        int nz = Mathf.CeilToInt((wb.size.z + 2) / Cell) + 1;

        // 各マスが「水」かどうか：水面以外の当たり判定の一番高い所が水面より低いか
        var isWater = new bool[nx, nz];
        var half = new Vector3(Cell * 0.5f, 0.05f, Cell * 0.5f);
        for (int i = 0; i < nx; i++)
        {
            for (int k = 0; k < nz; k++)
            {
                var c = new Vector3(minX + (i + 0.5f) * Cell, 60f, minZ + (k + 0.5f) * Cell);
                if (c.x < wb.min.x || c.x > wb.max.x || c.z < wb.min.z || c.z > wb.max.z)
                    continue;

                float topY = float.NegativeInfinity;
                foreach (var h in Physics.BoxCastAll(c, half, Vector3.down, Quaternion.identity, 200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.gameObject == water || h.collider is CharacterController)
                        continue;
                    float y = h.distance == 0 ? h.collider.bounds.max.y : c.y - h.distance;
                    if (y > topY) topY = y;
                }
                isWater[i, k] = topY < waterY + 0.02f;
            }
        }

        // 陸と接している水のマスに壁を置く（横に続くマスは1つの箱にまとめる）
        var root = new GameObject(BlockersName) { layer = WaterLayer };
        Undo.RegisterCreatedObjectUndo(root, "Rebuild water blockers");
        int boxes = 0;
        for (int k = 0; k < nz; k++)
        {
            int runStart = -1;
            for (int i = 0; i <= nx; i++)
            {
                bool edge = i < nx && isWater[i, k] && HasLandNeighbor(isWater, i, k, nx, nz);
                if (edge)
                {
                    if (runStart < 0) runStart = i;
                    continue;
                }
                if (runStart < 0)
                    continue;

                float len = (i - runStart) * Cell;
                var go = new GameObject("Blocker") { layer = WaterLayer };
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(minX + runStart * Cell + len / 2f, waterY + 2f, minZ + (k + 0.5f) * Cell);
                go.AddComponent<BoxCollider>().size = new Vector3(len, BlockerHeight, Cell);
                boxes++;
                runStart = -1;
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Tools] Water blockers rebuilt: " + boxes + " boxes");
    }

    private static bool HasLandNeighbor(bool[,] isWater, int i, int k, int nx, int nz)
    {
        int[] di = { 1, -1, 0, 0 }, dk = { 0, 0, 1, -1 };
        for (int d = 0; d < 4; d++)
        {
            int ii = i + di[d], kk = k + dk[d];
            if (ii >= 0 && ii < nx && kk >= 0 && kk < nz && !isWater[ii, kk])
                return true;
        }
        return false;
    }

    // =========================
    // ミニマップの地図（README 3.4）
    // =========================

    private const string MinimapImagePath = "Assets/Texture/minimap_village.png";
    private const int MinimapResolution = 1024;

    [MenuItem(MenuRoot + "ミニマップの地図を撮り直す")]
    public static void RecaptureMinimap()
    {
        if (!CheckNotPlaying()) return;

        var minimap = Object.FindAnyObjectByType<MinimapUI>(FindObjectsInactive.Include);
        if (minimap == null)
        {
            EditorUtility.DisplayDialog("ミニマップ", "MinimapUIが見つかりません。MainSceneを開いてください。", "OK");
            return;
        }

        // 撮影範囲はMinimapUIの設定と同じにする
        var so = new SerializedObject(minimap);
        Vector2 center = so.FindProperty("mapWorldCenter").vector2Value;
        float size = so.FindProperty("mapWorldSize").floatValue;

        // NPCとプレイヤーは写さない
        var hidden = new System.Collections.Generic.List<GameObject>();
        foreach (var name in new[] { "NPCs", "Player" })
        {
            var go = GameObject.Find(name);
            if (go != null && go.activeSelf) { go.SetActive(false); hidden.Add(go); }
        }

        var camGo = new GameObject("MinimapCaptureCam");
        try
        {
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size / 2f;
            cam.transform.SetPositionAndRotation(new Vector3(center.x, 120f, center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.6f, 0.3f);

            var rt = new RenderTexture(MinimapResolution, MinimapResolution, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(MinimapResolution, MinimapResolution, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, MinimapResolution, MinimapResolution), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            System.IO.File.WriteAllBytes(MinimapImagePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            foreach (var go in hidden) go.SetActive(true);
        }

        AssetDatabase.ImportAsset(MinimapImagePath);
        Debug.Log("[Tools] Minimap image recaptured: " + MinimapImagePath);
    }

    private static bool CheckNotPlaying()
    {
        if (!EditorApplication.isPlaying)
            return true;
        EditorUtility.DisplayDialog("研究用ツール", "Playを止めてから実行してください。", "OK");
        return false;
    }
}
