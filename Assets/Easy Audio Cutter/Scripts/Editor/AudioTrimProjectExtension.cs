namespace EasyAudioCutter
{
    using UnityEngine;
    using UnityEditor;

    [InitializeOnLoad]
    public class AudioTrimProjectExtension
    {
        static AudioTrimProjectExtension()
        {
            // Subscribe to the project window item GUI event
            EditorApplication.projectWindowItemOnGUI += DrawAudioTrimButton;
        }

        private static void DrawAudioTrimButton(string guid, Rect selectionRect)
        {
            // Get the asset path from the GUID
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            // Check if the asset is an AudioClip
            if (asset is AudioClip audioClip)
            {
                float fixedWidth = 40f;
                float fixedHeight = 16f;

                Rect buttonRect = new Rect(
                    selectionRect.x + (selectionRect.width - fixedWidth) / 2f,
                    selectionRect.y,
                    fixedWidth,
                    fixedHeight
                );


                // Define the button style
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 10,
                    normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.1f, 0.6f, 0.9f)) },
                    hover = { background = MakeTex(2, 2, new Color(0.1f, 0.6f, 0.9f) * 1.2f) },
                    padding = new RectOffset(2, 2, 2, 2)
                };

                // Draw the button and handle click events
                if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("d_editicon.sml", "Edit this AudioClip")))
                {
                    EasyAudioCutter window = EditorWindow.GetWindow<EasyAudioCutter>("Easy Audio Cutter", true);
                    window.Initialize(audioClip);
                    window.Show();
                    window.Repaint();
                }
            }
        }

        // Utility method to create a solid color texture
        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        [MenuItem("Assets/Easy Audio Cutter", true)]
        private static bool ValidateEditAudioClipFromContext()
        {
            // Validate if the selected object is an AudioClip
            return Selection.activeObject is AudioClip;
        }
    }
}
