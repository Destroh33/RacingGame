using UnityEngine;
using UnityEditor;

public class RemovePhysicsComponents
{
    [MenuItem("Tools/Remove Physics From Selected")]
    static void Remove()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No GameObject selected.");
            return;
        }

        int removed = 0;

        foreach (Rigidbody rb in selected.GetComponentsInChildren<Rigidbody>())
        {
            Undo.DestroyObjectImmediate(rb);
            removed++;
        }

        foreach (Collider col in selected.GetComponentsInChildren<Collider>())
        {
            Undo.DestroyObjectImmediate(col);
            removed++;
        }

        Debug.Log($"Removed {removed} components from {selected.name} and its children.");
    }
}
