using UnityEngine;

// One continuous peduncle-to-lip profile for the shared Betta bind mesh. Measurements
// are in its longitudinal frame, rather than scaling the head around a separate pivot.
public readonly struct BettaBodyProfile
{
    const float ReferenceLength = .172214f;
    static readonly float[] Stations = { -.175f, -.15f, -.125f, -.10f, -.075f, -.055f, -.035f, -.02f, -.005f, .005f, .015f, .025f, .03f };
    // Source envelope measured on BODY cross sections; fins beyond it are feathered out.
    static readonly float[] SourceTop = { .014f, .0218f, .0318f, .0396f, .0395f, .0367f, .0307f, .0253f, .0187f, .014f, .009f, .0017f, .001f };
    static readonly float[] SourceBottom = { -.023f, -.0342f, -.0372f, -.04f, -.043f, -.0435f, -.0394f, -.0343f, -.0269f, -.0197f, -.0115f, -.0042f, -.002f };
    static readonly float[] SourceWidth = { .0045f, .0058f, .0078f, .0104f, .0121f, .0128f, .0126f, .0119f, .0109f, .0095f, .0082f, .002f, .001f };
    // Spread volume along the torso and across its width instead of raising a
    // short hump behind the head. The posterior envelope eases into the peduncle.
    static readonly float[] TargetTop = { .012f, .0215f, .0285f, .033f, .0355f, .0358f, .034f, .030f, .024f, .0185f, .01f, .0017f, .001f };
    static readonly float[] TargetBottom = { -.014f, -.0195f, -.025f, -.030f, -.034f, -.036f, -.036f, -.034f, -.029f, -.023f, -.014f, -.0042f, -.002f };
    static readonly float[] TargetWidth = { .0045f, .0065f, .009f, .0125f, .016f, .0176f, .0175f, .0165f, .0148f, .0125f, .010f, .002f, .001f };

    readonly Vector3 origin, forward, up;
    readonly float size, proportion;

    public BettaBodyProfile(Vector3 head, Vector3 tail, float headProportion)
    {
        origin = head;
        forward = (head - tail).normalized;
        up = Vector3.Cross(forward, Vector3.right).normalized;
        size = Vector3.Distance(head, tail) / ReferenceLength;
        proportion = headProportion;
    }

    // Monotone cubic Hermite interpolation: continuous slope without extra humps
    // between the measured stations, especially where the back meets the forehead.
    static float Sample(float[] values, float s)
    {
        int i = 0;
        while (i < Stations.Length - 2 && s > Stations[i + 1]) i++;
        float h = Stations[i + 1] - Stations[i];
        float Slope(int j)
        {
            if (j == 0) return (values[1] - values[0]) / (Stations[1] - Stations[0]);
            if (j == Stations.Length - 1) return (values[j] - values[j - 1]) / (Stations[j] - Stations[j - 1]);
            float a = (values[j] - values[j - 1]) / (Stations[j] - Stations[j - 1]);
            float b = (values[j + 1] - values[j]) / (Stations[j + 1] - Stations[j]);
            if (a * b <= 0f) return 0f;
            float w1 = 2f * (Stations[j + 1] - Stations[j]) + Stations[j] - Stations[j - 1];
            float w2 = Stations[j + 1] - Stations[j] + 2f * (Stations[j] - Stations[j - 1]);
            return (w1 + w2) / (w1 / a + w2 / b);
        }
        float t = Mathf.Clamp01((s - Stations[i]) / h), t2 = t * t, t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * values[i] + (t3 - 2f * t2 + t) * h * Slope(i)
            + (-2f * t3 + 3f * t2) * values[i + 1] + (t3 - t2) * h * Slope(i + 1);
    }

    public Vector3 Map(Vector3 point)
    {
        Vector3 d = (point - origin) / size;
        float s = Vector3.Dot(d, forward), v = Vector3.Dot(d, up);
        float top = Sample(SourceTop, s), bottom = Sample(SourceBottom, s);
        float middle = (top + bottom) * .5f, radius = Mathf.Max(.001f, (top - bottom) * .5f);
        float width = Mathf.Max(.001f, Sample(SourceWidth, s));
        float radial = Mathf.Sqrt(d.x * d.x / (width * width) + (v - middle) * (v - middle) / (radius * radius));
        float influence = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 1.3f) / 1.3f));
        influence *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s + .19f) / .015f));
        influence *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .027f) / .008f));
        if (influence <= 0f) return point;
        float newTop = Sample(TargetTop, s), newBottom = Sample(TargetBottom, s);
        float skull = Mathf.Exp(-Mathf.Pow((s + .005f) / .04f, 2f));
        float variation = 1f + (proportion - 1f) * .5f * skull;
        float newMiddle = (newTop + newBottom) * .5f;
        float newRadius = (newTop - newBottom) * .5f * variation;
        float newV = newMiddle + (v - middle) / radius * newRadius;
        float newX = d.x * Sample(TargetWidth, s) / width * variation;
        return point + (Vector3.right * (newX - d.x) + up * (newV - v)) * (size * influence);
    }
}
