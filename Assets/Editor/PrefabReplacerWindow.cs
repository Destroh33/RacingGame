using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PrefabReplacerWindow : EditorWindow
{
    private class PrefabGroup
    {
        public GameObject prefab;
        public List<GameObject> targets = new List<GameObject>();
        public bool foldout = true;
    }

    private List<PrefabGroup> groups = new List<PrefabGroup>();
    private Vector2 scrollPos;

    [MenuItem("Tools/Prefab Replacer")]
    public static void Open()
    {
        GetWindow<PrefabReplacerWindow>("Prefab Replacer");
    }

    private void OnEnable()
    {
        // Initialize 4 groups
        while (groups.Count < 4)
            groups.Add(new PrefabGroup());
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Prefab Replacer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assign a prefab to each group, then list the scene objects to replace. " +
            "Each object will be deleted and replaced with its group's prefab at the same world transform.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < groups.Count; i++)
        {
            DrawGroup(i);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        GUI.enabled = CanReplace();
        if (GUILayout.Button("Replace All", GUILayout.Height(36)))
            ReplaceAll();
        GUI.enabled = true;

        if (GUILayout.Button("Clear All"))
        {
            foreach (var g in groups)
            {
                g.prefab = null;
                g.targets.Clear();
            }
        }
    }

    private void DrawGroup(int index)
    {
        var group = groups[index];

        var boxStyle = new GUIStyle(GUI.skin.box);
        EditorGUILayout.BeginVertical(boxStyle);

        // Header row
        EditorGUILayout.BeginHorizontal();
        group.foldout = EditorGUILayout.Foldout(group.foldout, $"Group {index + 1}", true, EditorStyles.foldoutHeader);
        if (group.prefab != null)
        {
            GUI.color = Color.green;
            GUILayout.Label($"✓ {group.prefab.name}", GUILayout.Width(160));
            GUI.color = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        if (!group.foldout)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.indentLevel++;

        // Prefab field
        group.prefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab", group.prefab, typeof(GameObject), false);

        // Validate it's actually a prefab
        if (group.prefab != null &&
            PrefabUtility.GetPrefabAssetType(group.prefab) == PrefabAssetType.NotAPrefab)
        {
            EditorGUILayout.HelpBox("Assigned object is not a prefab asset.", MessageType.Warning);
            group.prefab = null;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Scene Objects to Replace ({group.targets.Count})", EditorStyles.miniBoldLabel);

        // Target list
        for (int j = 0; j < group.targets.Count; j++)
        {
            EditorGUILayout.BeginHorizontal();
            group.targets[j] = (GameObject)EditorGUILayout.ObjectField(
                group.targets[j], typeof(GameObject), true);

            // Warn if object is a prefab asset (not scene object)
            if (group.targets[j] != null && !IsSceneObject(group.targets[j]))
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⚠ Not in scene", GUILayout.Width(90));
                GUI.color = Color.white;
            }

            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                group.targets.RemoveAt(j);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUI.indentLevel * 15);
        if (GUILayout.Button("+ Add Slot"))
            group.targets.Add(null);
        if (GUILayout.Button("+ Add Selected from Scene"))
            AddSelectedToGroup(group);
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }

    private void AddSelectedToGroup(PrefabGroup group)
    {
        foreach (var obj in Selection.gameObjects)
        {
            if (IsSceneObject(obj) && !group.targets.Contains(obj))
                group.targets.Add(obj);
        }
    }

    private bool IsSceneObject(GameObject obj)
    {
        return obj.scene.IsValid();
    }

    private bool CanReplace()
    {
        foreach (var g in groups)
        {
            if (g.prefab != null && g.targets.Count > 0)
                return true;
        }
        return false;
    }

    private void ReplaceAll()
    {
        int totalReplaced = 0;
        int skipped = 0;

        Undo.SetCurrentGroupName("Prefab Replacer: Replace All");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var group in groups)
        {
            if (group.prefab == null) continue;

            foreach (var target in group.targets)
            {
                if (target == null)
                {
                    skipped++;
                    continue;
                }
                if (!IsSceneObject(target))
                {
                    Debug.LogWarning($"[PrefabReplacer] Skipped '{target.name}' — not a scene object.");
                    skipped++;
                    continue;
                }

                // Capture world transform
                var pos      = target.transform.position;
                var rot      = target.transform.rotation;
                var scale    = target.transform.lossyScale;
                var parent   = target.transform.parent;
                var sibIndex = target.transform.GetSiblingIndex();
                var name     = target.name;

                // Instantiate prefab
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(group.prefab, parent);
                Undo.RegisterCreatedObjectUndo(instance, "Create Prefab Instance");

                // Restore world transform
                instance.transform.position   = pos;
                instance.transform.rotation   = rot;

                // lossyScale is world-space; convert to local if parented
                if (parent != null)
                    instance.transform.localScale = InverseScale(parent.lossyScale, scale);
                else
                    instance.transform.localScale = scale;

                instance.transform.SetSiblingIndex(sibIndex);
                instance.name = name;

                Undo.DestroyObjectImmediate(target);
                totalReplaced++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[PrefabReplacer] Done. Replaced: {totalReplaced}, Skipped: {skipped}");
        EditorUtility.DisplayDialog(
            "Prefab Replacer",
            $"Replaced {totalReplaced} object(s).\nSkipped {skipped} (null or non-scene).",
            "OK");
    }

    // Converts a world lossyScale to a localScale given the parent's lossyScale
    private static Vector3 InverseScale(Vector3 parentLossy, Vector3 worldScale)
    {
        return new Vector3(
            parentLossy.x != 0 ? worldScale.x / parentLossy.x : worldScale.x,
            parentLossy.y != 0 ? worldScale.y / parentLossy.y : worldScale.y,
            parentLossy.z != 0 ? worldScale.z / parentLossy.z : worldScale.z
        );
    }
}