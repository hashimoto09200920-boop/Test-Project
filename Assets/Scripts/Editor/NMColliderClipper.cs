using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NMColliderClipperWindow : EditorWindow
{
    bool useCustomY = false;
    float customClipY = -0.8f;

    void OnEnable()  => SceneView.duringSceneGui += OnSceneGUI;
    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;
    void OnSelectionChange() => Repaint();

    void OnSceneGUI(SceneView sceneView)
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0) return;

        Handles.color = Color.red;

        foreach (var go in selected)
        {
            PolygonCollider2D poly = go.GetComponent<PolygonCollider2D>();
            if (poly == null) continue;

            float clipY;
            if (useCustomY)
            {
                clipY = customClipY;
            }
            else
            {
                Transform muzzle = go.transform.Find("Muzzle");
                if (muzzle == null) continue;
                clipY = muzzle.localPosition.y;
            }

            // PolygonCollider2DのX範囲を求めてラインの幅を決める
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int p = 0; p < poly.pathCount; p++)
            {
                foreach (var pt in poly.GetPath(p))
                {
                    if (pt.x < minX) minX = pt.x;
                    if (pt.x > maxX) maxX = pt.x;
                }
            }

            // NMのローカル座標 → ワールド座標に変換してラインを描画
            Vector3 left  = go.transform.TransformPoint(new Vector3(minX, clipY, 0));
            Vector3 right = go.transform.TransformPoint(new Vector3(maxX, clipY, 0));
            Handles.DrawLine(left, right);

            // ラベル表示
            Vector3 labelPos = go.transform.TransformPoint(new Vector3(maxX + 0.1f, clipY, 0));
            Handles.Label(labelPos, $"{go.name}\nclipY={clipY:F2}");
        }
    }

    [MenuItem("Tools/IronNest/NM Collider Clipper...")]
    static void Open()
    {
        GetWindow<NMColliderClipperWindow>("NM Collider Clipper");
    }

    void OnGUI()
    {
        GUILayout.Label("NM Polygon Collider Clipper", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        useCustomY = EditorGUILayout.Toggle("カットY位置を手動指定", useCustomY);

        if (useCustomY)
        {
            customClipY = EditorGUILayout.FloatField("カットY位置（NMローカル座標）", customClipY);
            EditorGUILayout.HelpBox("Muzzleより下に設定すると弱点範囲が広がります。\nMuzzleより上に設定すると弱点範囲が狭まります。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("各NMのMuzzle子オブジェクトのY位置を自動で使用します。", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 選択中のNMをプレビュー表示
        GameObject[] selected = Selection.gameObjects;
        EditorGUILayout.LabelField($"選択中のオブジェクト: {selected.Length}体");
        foreach (var go in selected)
        {
            Transform muzzle = go.transform.Find("Muzzle");
            float y = useCustomY ? customClipY : (muzzle != null ? muzzle.localPosition.y : float.NaN);
            string yStr = float.IsNaN(y) ? "Muzzle未検出" : y.ToString("F2");
            EditorGUILayout.LabelField($"  {go.name}  →  カットY = {yStr}");
        }

        EditorGUILayout.Space();

        GUI.enabled = selected.Length > 0;
        if (GUILayout.Button("Clip NM Polygon Colliders At Muzzle", GUILayout.Height(36)))
        {
            ClipSelected(selected);
        }
        GUI.enabled = true;
    }

    void ClipSelected(GameObject[] selected)
    {
        int clippedCount = 0;
        foreach (GameObject go in selected)
        {
            PolygonCollider2D poly = go.GetComponent<PolygonCollider2D>();
            if (poly == null)
            {
                Debug.LogWarning($"[NMColliderClipper] {go.name} にPolygonCollider2Dがありません。スキップします。");
                continue;
            }

            float clipY;
            if (useCustomY)
            {
                clipY = customClipY;
            }
            else
            {
                Transform muzzle = go.transform.Find("Muzzle");
                if (muzzle == null)
                {
                    Debug.LogWarning($"[NMColliderClipper] {go.name} に「Muzzle」子オブジェクトが見つかりません。スキップします。");
                    continue;
                }
                clipY = muzzle.localPosition.y;
            }

            Undo.RecordObject(poly, "Clip NM Polygon At Muzzle");

            for (int p = 0; p < poly.pathCount; p++)
            {
                Vector2[] path = poly.GetPath(p);
                List<Vector2> clipped = ClipPolygonAboveY(path, clipY);
                if (clipped.Count >= 3)
                    poly.SetPath(p, clipped.ToArray());
                else
                    Debug.LogWarning($"[NMColliderClipper] {go.name} パス{p}がクリップ後に頂点不足。");
            }

            EditorUtility.SetDirty(poly);
            Debug.Log($"[NMColliderClipper] {go.name}: Y={clipY} でクリップ完了。");
            clippedCount++;
        }

        if (clippedCount > 0)
            Debug.Log($"[NMColliderClipper] {clippedCount}体完了。Ctrl+Sでプレハブを保存してください。");
    }

    static List<Vector2> ClipPolygonAboveY(Vector2[] points, float clipY)
    {
        var result = new List<Vector2>();
        int count = points.Length;

        for (int i = 0; i < count; i++)
        {
            Vector2 curr = points[i];
            Vector2 next = points[(i + 1) % count];

            bool currAbove = curr.y >= clipY;
            bool nextAbove = next.y >= clipY;

            if (currAbove)
                result.Add(curr);

            if (currAbove != nextAbove)
            {
                float t = (clipY - curr.y) / (next.y - curr.y);
                result.Add(new Vector2(Mathf.Lerp(curr.x, next.x, t), clipY));
            }
        }

        return result;
    }
}
