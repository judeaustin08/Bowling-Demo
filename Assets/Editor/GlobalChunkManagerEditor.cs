using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GlobalChunkManager))]
public class GlobalChunkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GlobalChunkManager manager = (GlobalChunkManager)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
            manager.RegenerateAllChunks();
    }
}