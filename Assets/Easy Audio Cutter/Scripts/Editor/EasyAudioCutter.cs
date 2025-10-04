using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace EasyAudioCutter
{
    public class EasyAudioCutter : EditorWindow
    {
        private enum Tab { Trim, Merge, AdjustVolume }
        private Tab currentTab = Tab.Trim;

        private AudioClip sourceClip;
        private float trimStart = 0f;
        private float trimEnd = 1f;
        private float fadeInDuration = 0f;
        private float fadeOutDuration = 0f;
        private AnimationCurve fadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve fadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0);

        private List<AudioClip> mergeClips = new List<AudioClip>();
        private List<AudioClip> volumeClips = new List<AudioClip>();

        private AudioSource previewAudioSource;
        private AudioClip previewClip;

        private float[] waveformSamples;
        private const int waveformWidth = 400;
        private const float minWaveformHeight = 2f;

        private bool loopPreview = false;
        private double previewStartTime = -1;

        private float volumeIncrease = 0f;

        [MenuItem("Window/Easy Audio Cutter")]
        public static void ShowWindow()
        {
            var window = GetWindow<EasyAudioCutter>("Easy Audio Cutter");
            window.minSize = new Vector2(400, 300);
        }

        public void Initialize(AudioClip clip)
        {
            sourceClip = clip;
            trimStart = 0f;
            trimEnd = sourceClip != null ? sourceClip.length : 1f;
            fadeInDuration = 0f;
            fadeOutDuration = 0f;
            fadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);
            fadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0);
            mergeClips.Clear();
            volumeClips.Clear();
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
                DestroyPreviewClip();
            }
            UpdateWaveform();
            Repaint();
        }

        private void OnEnable()
        {
            GameObject go = new GameObject("AudioPreviewPlayer");
            go.hideFlags = HideFlags.HideAndDontSave;
            previewAudioSource = go.AddComponent<AudioSource>();
            previewAudioSource.playOnAwake = false;
            previewAudioSource.loop = false; // Varsayılan olarak loop kapalı
        }

        private void OnDisable()
        {
            if (previewAudioSource != null)
                DestroyImmediate(previewAudioSource.gameObject);
            DestroyPreviewClip();
        }

        private void OnGUI()
        {
            if (previewAudioSource != null && previewAudioSource.isPlaying)
            {
                Repaint();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.07f, 0.1f, 0.2f));

            if (GUILayout.Button("Help", EasyAudioCutterTheme.ButtonStyle, GUILayout.Width(100)))
            {
                DrawHelpDialog();
            }
            EditorGUILayout.Space(50);

            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "Trim", "Merge", "Adjust Volume" });
            EditorGUILayout.Space();

            switch (currentTab)
            {
                case Tab.Trim: DrawTrimTab(); break;
                case Tab.Merge: DrawMergeTab(); break;
                case Tab.AdjustVolume: DrawAdjustVolumeTab(); break;
            }
        }

        private void DrawHelpDialog()
        {
            string message = "Easy Audio Cutter - Help\n\n" +
                            "**Trim Tab**\n" +
                            "• Allows trimming a specific section with fade in/out effects.\n" +
                            "• Offers preview and save options to test and export your edits.\n" +
                            "• Supports adjustable start and end times for precise trimming of audio clips.\n" +
                            "• Fade in/out durations can be customized to create smooth transitions.\n" +
                            "• Waveform visualization updates in real-time as you adjust trim or fade settings.\n" +
                            "• Tip: Use the curve editor to fine-tune fade effects for creative control.\n\n" +
                            "**Merge Tab**\n" +
                            "• Combines multiple audio files into a single clip.\n" +
                            "• All audio files must have the same format (e.g., same sample rate and channels) to ensure compatibility.\n" +
                            "• Provides preview functionality for merged audio before saving.\n" +
                            "• Add or remove clips dynamically using the '+' and 'X' buttons.\n" +
                            "• Tip: Verify audio formats beforehand to avoid errors during merging.\n\n" +
                            "**Adjust Volume Tab**\n" +
                            "• Increases or decreases the volume of selected audio files.\n" +
                            "• Use -1 to 1 range: negative values decrease volume, positive values increase it, and 0 leaves it unchanged.\n" +
                            "• Allows real-time preview of volume adjustments to hear changes instantly.\n" +
                            "• Saves modified clips with a '_Adjusted' suffix to preserve original files.\n" +
                            "• Tip: Avoid setting values close to -1 or 1 to prevent audio clipping or excessive reduction.\n" +
                            "• Supports multiple clips for batch volume adjustment.\n\n" +
                            "**General Information**\n" +
                            "• Saves all processed audio in WAV format for broad compatibility.\n" +
                            "• Curve fields in the Trim tab determine the shape and smoothness of fade transitions.\n" +
                            "• Compatible with Unity's audio system for seamless integration into your projects.\n" +
                            "• Developed and updated as of July 05, 2025, at 02:21 PM +03.\n" +
                            "• Requires an AudioClip to be assigned before performing any operations.\n" +
                            "• Note: Preview audio is temporary and cleared when the window is closed.\n" +
                            "• For best results, use high-quality audio files and test previews before saving.\n";

            EditorUtility.DisplayDialog("Easy Audio Cutter - Help", message, "OK");
        }

        private void DrawTrimTab()
        {
            EditorGUILayout.LabelField("Trim AudioClip", EasyAudioCutterTheme.HeaderStyle);
            EditorGUILayout.Space();

            AudioClip oldSource = sourceClip;
            sourceClip = (AudioClip)EditorGUILayout.ObjectField("Source Clip", sourceClip, typeof(AudioClip), false);

            if (sourceClip != oldSource)
            {
                trimStart = 0f;
                trimEnd = sourceClip != null ? sourceClip.length : 1f;
                fadeInDuration = 0f;
                fadeOutDuration = 0f;
                fadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);
                fadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0);
                UpdateWaveform();
            }

            if (sourceClip == null)
            {
                EditorGUILayout.HelpBox("Please assign an AudioClip to trim.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Length: {sourceClip.length:F2} seconds");
            EditorGUILayout.Space();

            float oldTrimStart = trimStart;
            float oldTrimEnd = trimEnd;
            float oldFadeIn = fadeInDuration;
            float oldFadeOut = fadeOutDuration;
            AnimationCurve oldFadeInCurve = new AnimationCurve(fadeInCurve.keys);
            AnimationCurve oldFadeOutCurve = new AnimationCurve(fadeOutCurve.keys);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start Time", EasyAudioCutterTheme.SliderLabelStyle);
            trimStart = EditorGUILayout.Slider(trimStart, 0f, sourceClip.length);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("End Time", EasyAudioCutterTheme.SliderLabelStyle);
            trimEnd = EditorGUILayout.Slider(trimEnd, 0f, sourceClip.length);
            EditorGUILayout.EndHorizontal();
            trimEnd = Mathf.Max(trimStart, trimEnd);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fade In Duration", EasyAudioCutterTheme.SliderLabelStyle);
            fadeInDuration = EditorGUILayout.Slider(fadeInDuration, 0f, trimEnd - trimStart);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fade Out Duration", EasyAudioCutterTheme.SliderLabelStyle);
            fadeOutDuration = EditorGUILayout.Slider(fadeOutDuration, 0f, trimEnd - trimStart);
            EditorGUILayout.EndHorizontal();

            AnimationCurve newFadeInCurve = EditorGUILayout.CurveField("Fade In Curve", fadeInCurve, Color.cyan, new Rect(0, 0, 1, 1), GUILayout.Height(20));
            AnimationCurve newFadeOutCurve = EditorGUILayout.CurveField("Fade Out Curve", fadeOutCurve, Color.magenta, new Rect(0, 0, 1, 1), GUILayout.Height(20));

            if (!AreCurvesEqual(fadeInCurve, newFadeInCurve))
            {
                fadeInCurve = newFadeInCurve;
                UpdateWaveform();
            }
            if (!AreCurvesEqual(fadeOutCurve, newFadeOutCurve))
            {
                fadeOutCurve = newFadeOutCurve;
                UpdateWaveform();
            }

            if (!Mathf.Approximately(oldTrimStart, trimStart) ||
                !Mathf.Approximately(oldTrimEnd, trimEnd) ||
                !Mathf.Approximately(oldFadeIn, fadeInDuration) ||
                !Mathf.Approximately(oldFadeOut, fadeOutDuration) ||
                !AreCurvesEqual(oldFadeInCurve, fadeInCurve) ||
                !AreCurvesEqual(oldFadeOutCurve, fadeOutCurve))
            {
                UpdateWaveform();
            }

            EditorGUILayout.Space();

            Rect waveformRect = GUILayoutUtility.GetRect(position.width - 20, 100);
            waveformRect.x += 10;
            waveformRect.width -= 20;
            DrawWaveform(waveformRect);

            EditorGUILayout.Space();

            loopPreview = EditorGUILayout.Toggle("Loop Preview", loopPreview);

            if (GUILayout.Button(previewAudioSource.isPlaying ? "Stop Preview" : "Play Preview", EasyAudioCutterTheme.ButtonStyle))
            {
                if (previewAudioSource.isPlaying)
                {
                    previewAudioSource.Stop();
                    DestroyPreviewClip();
                }
                else
                {
                    CreatePreviewClipAndPlay();
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Process and Save Trimmed Clip", EasyAudioCutterTheme.ButtonStyle))
            {
                ProcessAndSaveTrimmedClip();
            }

            if (previewAudioSource.isPlaying && previewClip != null)
            {
                float progress = previewAudioSource.time / previewClip.length;
                progress = Mathf.Clamp01(progress);

                EditorGUILayout.Space(5);
                Rect barRect = GUILayoutUtility.GetRect(100, 18);
                EditorGUI.ProgressBar(barRect, progress, "Preview Progress");
                EditorGUILayout.Space(5);
            }
        }

        private void DrawMergeTab()
        {
            EditorGUILayout.LabelField("Merge AudioClips", EasyAudioCutterTheme.HeaderStyle);
            EditorGUILayout.Space();

            for (int i = 0; i < mergeClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                mergeClips[i] = (AudioClip)EditorGUILayout.ObjectField(mergeClips[i], typeof(AudioClip), false);
                if (GUILayout.Button("X", EasyAudioCutterTheme.ButtonStyle, GUILayout.Width(20)))
                {
                    mergeClips.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add AudioClip", EasyAudioCutterTheme.ButtonStyle))
            {
                mergeClips.Add(null);
            }

            EditorGUILayout.Space();

            if (mergeClips.Count >= 2)
            {
                if (GUILayout.Button(previewAudioSource.isPlaying ? "Stop Preview" : "Play Merged Preview", EasyAudioCutterTheme.ButtonStyle))
                {
                    if (previewAudioSource.isPlaying)
                    {
                        previewAudioSource.Stop();
                        DestroyPreviewClip();
                    }
                    else
                    {
                        CreateMergedPreviewClipAndPlay();
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Process and Save Merged Clip", EasyAudioCutterTheme.ButtonStyle))
                {
                    ProcessAndSaveMergedClip();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Add at least two AudioClips to merge.", MessageType.Info);
            }
        }

        private void DrawAdjustVolumeTab()
        {
            EditorGUILayout.LabelField("Adjust Volume", EasyAudioCutterTheme.HeaderStyle);
            EditorGUILayout.Space();

            for (int i = 0; i < volumeClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                volumeClips[i] = (AudioClip)EditorGUILayout.ObjectField("Audio Clip " + (i + 1), volumeClips[i], typeof(AudioClip), false);
                if (GUILayout.Button("X", EasyAudioCutterTheme.ButtonStyle, GUILayout.Width(20)))
                {
                    volumeClips.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add AudioClip", EasyAudioCutterTheme.ButtonStyle))
            {
                volumeClips.Add(null);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Volume Adjustment", EasyAudioCutterTheme.SliderLabelStyle);
            volumeIncrease = EditorGUILayout.Slider(volumeIncrease, -1f, 1f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Note: -1 to 1 range: negative decreases, positive increases, 0 no change.", EditorStyles.helpBox);

            if (volumeClips.Count > 0)
            {
                if (GUILayout.Button("Apply Volume Adjustment", EasyAudioCutterTheme.ButtonStyle))
                {
                    ApplyVolumeIncrease();
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Process and Save Adjusted Clips", EasyAudioCutterTheme.ButtonStyle))
                {
                    ProcessAndSaveAdjustedClips();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Add at least one AudioClip to adjust volume.", MessageType.Info);
            }
        }

        private void DestroyPreviewClip()
        {
            if (previewClip != null)
            {
                DestroyImmediate(previewClip);
                previewClip = null;
                previewStartTime = -1;
            }
        }

        private void CreatePreviewClipAndPlay()
        {
            if (sourceClip == null || trimEnd <= trimStart) return;

            previewStartTime = EditorApplication.timeSinceStartup;

            int freq = sourceClip.frequency;
            int channels = sourceClip.channels;

            int startSample = Mathf.FloorToInt(trimStart * freq) * channels;
            int sampleLength = Mathf.FloorToInt((trimEnd - trimStart) * freq) * channels;

            float[] allData = new float[sourceClip.samples * channels];
            sourceClip.GetData(allData, 0);

            float[] trimmedData = new float[sampleLength];
            System.Array.Copy(allData, startSample, trimmedData, 0, sampleLength);

            ApplyFades(trimmedData, freq, channels);

            previewClip = AudioClip.Create("PreviewTrim", sampleLength / channels, channels, freq, false);
            previewClip.SetData(trimmedData, 0);
            previewAudioSource.clip = previewClip;
            previewAudioSource.loop = loopPreview;
            previewAudioSource.Play();
        }

        private void ProcessAndSaveTrimmedClip()
        {
            if (sourceClip == null) return;

            int freq = sourceClip.frequency;
            int channels = sourceClip.channels;

            int startSample = Mathf.FloorToInt(trimStart * freq) * channels;
            int sampleLength = Mathf.FloorToInt((trimEnd - trimStart) * freq) * channels;

            float[] srcData = new float[sourceClip.samples * channels];
            sourceClip.GetData(srcData, 0);

            float[] trimmedData = new float[sampleLength];
            System.Array.Copy(srcData, startSample, trimmedData, 0, sampleLength);

            ApplyFades(trimmedData, freq, channels);

            SaveWav(trimmedData, freq, channels, "Trimmed_" + sourceClip.name);
        }

        private void ApplyFades(float[] data, int freq, int channels)
        {
            int fadeInSamples = Mathf.FloorToInt(fadeInDuration * freq) * channels;
            for (int i = 0; i < fadeInSamples && i < data.Length; i++)
            {
                float t = (float)i / fadeInSamples;
                data[i] *= fadeInCurve.Evaluate(t);
            }

            int fadeOutSamples = Mathf.FloorToInt(fadeOutDuration * freq) * channels;
            int startFadeOut = data.Length - fadeOutSamples;
            for (int i = 0; i < fadeOutSamples && (startFadeOut + i) < data.Length; i++)
            {
                float t = (float)i / fadeOutSamples;
                data[startFadeOut + i] *= fadeOutCurve.Evaluate(t);
            }
        }

        private void CreateMergedPreviewClipAndPlay()
        {
            if (mergeClips.Count < 2) return;

            int freq = mergeClips[0].frequency;
            int channels = mergeClips[0].channels;

            List<float> mergedSamples = new List<float>();

            foreach (var clip in mergeClips)
            {
                if (clip == null || clip.frequency != freq || clip.channels != channels)
                {
                    EditorUtility.DisplayDialog("Error", "All clips must have same format.", "OK");
                    return;
                }

                float[] data = new float[clip.samples * channels];
                clip.GetData(data, 0);
                mergedSamples.AddRange(data);
            }

            float[] mergedData = mergedSamples.ToArray();
            int samples = mergedData.Length / channels;

            DestroyPreviewClip(); // Önceki clip'i temizle
            previewClip = AudioClip.Create("PreviewMerged", samples, channels, freq, false);
            previewClip.SetData(mergedData, 0);
            previewAudioSource.clip = previewClip;
            previewAudioSource.loop = loopPreview;
            previewAudioSource.Play();
        }

        private void ProcessAndSaveMergedClip()
        {
            if (mergeClips.Count < 2) return;

            int freq = mergeClips[0].frequency;
            int channels = mergeClips[0].channels;

            List<float> mergedSamples = new List<float>();

            foreach (var clip in mergeClips)
            {
                if (clip == null || clip.frequency != freq || clip.channels != channels)
                {
                    EditorUtility.DisplayDialog("Error", "All clips must have same format.", "OK");
                    return;
                }

                float[] data = new float[clip.samples * channels];
                clip.GetData(data, 0);
                mergedSamples.AddRange(data);
            }

            SaveWav(mergedSamples.ToArray(), freq, channels, "MergedAudio");
        }

        private void ApplyVolumeIncrease()
        {
            if (previewAudioSource.isPlaying)
            {
                previewAudioSource.Stop();
                DestroyPreviewClip();
            }

            foreach (var clip in volumeClips)
            {
                if (clip != null)
                {
                    float[] data = new float[clip.samples * clip.channels];
                    clip.GetData(data, 0);

                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] += data[i] * volumeIncrease;
                        if (data[i] > 1f) data[i] = 1f;
                        if (data[i] < -1f) data[i] = -1f;
                    }

                    int samples = data.Length / clip.channels;
                    AudioClip tempClip = AudioClip.Create(clip.name + "_Temp", samples, clip.channels, clip.frequency, false);
                    tempClip.SetData(data, 0);
                    previewAudioSource.clip = tempClip;
                    previewAudioSource.loop = false; // Loop'u kapat
                    previewAudioSource.Play();
                    // Sesin bir kez çalmasını beklemek için kısa bir gecikme bırak (isteğe bağlı)
                    // EditorApplication.delayCall += () => { if (previewAudioSource.isPlaying) previewAudioSource.Stop(); };
                }
            }
        }

        private void ProcessAndSaveAdjustedClips()
        {
            foreach (var clip in volumeClips)
            {
                if (clip != null)
                {
                    float[] data = new float[clip.samples * clip.channels];
                    clip.GetData(data, 0);

                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] += data[i] * volumeIncrease;
                        if (data[i] > 1f) data[i] = 1f;
                        if (data[i] < -1f) data[i] = -1f;
                    }

                    SaveWav(data, clip.frequency, clip.channels, clip.name + "_Adjusted");
                }
            }
            EditorUtility.DisplayDialog("Done", "Adjusted clips saved!", "OK");
        }

        private void SaveWav(float[] data, int frequency, int channels, string defaultName)
        {
            int samples = data.Length / channels;
            AudioClip clip = AudioClip.Create(defaultName, samples, channels, frequency, false);
            clip.SetData(data, 0);

            string path = EditorUtility.SaveFilePanelInProject("Save Audio", defaultName + ".wav", "wav", "Choose location");

            if (!string.IsNullOrEmpty(path))
            {
                byte[] bytes = WavUtility.FromAudioClip(clip, out _, true);
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
                EditorUtility.DisplayDialog("Done", "Audio saved!", "OK");
            }
        }

        private void UpdateWaveform()
        {
            if (sourceClip == null)
            {
                waveformSamples = null;
                return;
            }

            int freq = sourceClip.frequency;
            int channels = sourceClip.channels;

            int startSample = Mathf.FloorToInt(trimStart * freq) * channels;
            int lengthSamples = Mathf.FloorToInt((trimEnd - trimStart) * freq) * channels;

            float[] allData = new float[sourceClip.samples * channels];
            sourceClip.GetData(allData, 0);

            float[] trimmedData = new float[lengthSamples];
            System.Array.Copy(allData, startSample, trimmedData, 0, lengthSamples);

            ApplyFades(trimmedData, freq, channels);

            int samplesPerPixel = Mathf.Max(1, trimmedData.Length / waveformWidth);
            waveformSamples = new float[waveformWidth];
            for (int i = 0; i < waveformWidth; i++)
            {
                float max = 0f;
                int start = i * samplesPerPixel;
                int end = Mathf.Min(start + samplesPerPixel, trimmedData.Length);
                for (int j = start; j < end; j += channels)
                {
                    float val = Mathf.Abs(trimmedData[j]);
                    if (val > max) max = val;
                }
                waveformSamples[i] = max;
            }

            Repaint();
        }

        private void DrawWaveform(Rect rect)
        {
            if (waveformSamples == null || waveformSamples.Length == 0) return;

            EditorGUI.DrawRect(rect, new Color(0.05f, 0.08f, 0.15f));
            float midY = rect.y + rect.height / 2f;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.6f, 0f);

            float totalWaveformWidth = waveformSamples.Length;
            float xOffset = (rect.width - totalWaveformWidth) / 2f;

            for (int i = 0; i < waveformSamples.Length; i++)
            {
                float x = rect.x + xOffset + i;
                float height = Mathf.Max(minWaveformHeight, waveformSamples[i] * rect.height);
                Handles.DrawLine(
                    new Vector3(x, midY - height / 2),
                    new Vector3(x, midY + height / 2)
                );
            }

            float startX = rect.x + xOffset;
            float endX = rect.x + xOffset + waveformSamples.Length;

            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(startX, rect.y), new Vector3(startX, rect.y + rect.height));
            Handles.DrawLine(new Vector3(endX, rect.y), new Vector3(endX, rect.y + rect.height));

            Handles.EndGUI();
        }

        private bool AreCurvesEqual(AnimationCurve curve1, AnimationCurve curve2)
        {
            if (curve1.length != curve2.length) return false;
            for (int i = 0; i < curve1.length; i++)
            {
                if (curve1.keys[i].time != curve2.keys[i].time ||
                    curve1.keys[i].value != curve2.keys[i].value ||
                    curve1.keys[i].inTangent != curve2.keys[i].inTangent ||
                    curve1.keys[i].outTangent != curve2.keys[i].outTangent)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public static class EasyAudioCutterTheme
    {
        public static GUIStyle HeaderStyle { get; private set; }
        public static GUIStyle SectionStyle { get; private set; }
        public static GUIStyle ButtonStyle { get; private set; }
        public static GUIStyle SliderLabelStyle { get; private set; }
        public static GUIStyle SliderStyle { get; private set; } // Slider için yeni stil

        static EasyAudioCutterTheme()
        {
            SetupStyles();
        }

        private static void SetupStyles()
        {
            HeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            HeaderStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            HeaderStyle.fontSize = 14;

            SectionStyle = new GUIStyle(GUI.skin.box);
            SectionStyle.normal.background = MakeTex(1, 1, new Color(0.1f, 0.15f, 0.25f));
            SectionStyle.padding = new RectOffset(10, 10, 10, 10);

            ButtonStyle = new GUIStyle(GUI.skin.button);
            ButtonStyle.normal.textColor = Color.white;
            ButtonStyle.normal.background = MakeTex(1, 1, new Color(0.05f, 0.3f, 0.6f));
            ButtonStyle.hover.background = MakeTex(1, 1, new Color(0.07f, 0.4f, 0.8f));
            ButtonStyle.fontSize = 12;
            ButtonStyle.padding = new RectOffset(6, 6, 4, 4);

            SliderLabelStyle = new GUIStyle(EditorStyles.label);
            SliderLabelStyle.normal.textColor = Color.cyan;

            SliderStyle = new GUIStyle(); // Slider için temel stil
            SliderStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.2f, 0.35f));
            SliderStyle.hover.background = MakeTex(1, 1, new Color(0.2f, 0.25f, 0.4f));
            SliderStyle.padding = new RectOffset(4, 4, 2, 2);
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}