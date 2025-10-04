namespace EasyAudioCutter
{
    using System.IO;
    using UnityEngine;

    public static class WavUtility
    {
        public static byte[] FromAudioClip(AudioClip clip, out int length, bool stream = false)
        {
            length = 0;
            if (clip == null) return null;

            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            byte[] bytes = ConvertAudioClipDataToInt16(samples);

            using (var streamMem = new MemoryStream())
            using (var writer = new BinaryWriter(streamMem))
            {
                int hz = clip.frequency;
                short channels = (short)clip.channels;

                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + bytes.Length);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16); // PCM
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(hz);
                writer.Write(hz * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);

                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(bytes.Length);
                writer.Write(bytes);

                length = (int)streamMem.Length;
                return streamMem.ToArray();
            }
        }

        private static byte[] ConvertAudioClipDataToInt16(float[] data)
        {
            short[] intData = new short[data.Length];
            byte[] bytesData = new byte[data.Length * 2];
            const float rescaleFactor = 32767f;

            for (int i = 0; i < data.Length; i++)
            {
                intData[i] = (short)(Mathf.Clamp(data[i], -1f, 1f) * rescaleFactor);
                byte[] byteArr = System.BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            return bytesData;
        }
    }

}
