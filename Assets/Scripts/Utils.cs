using System.Collections;
using UnityEngine;

public class Utils
{
    public static IEnumerator LerpAsync(float a, float b, float time, System.Action<float> callback)
    {
        for (float timer = 0, t = 0, nv; timer < time; timer += Time.deltaTime, t = timer / time)
        {
            nv = a + (b - a) * t;
            callback.Invoke(nv);

            yield return new WaitForEndOfFrame();
        }

        callback.Invoke(b);
    }
}
