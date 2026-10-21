using UnityEngine;

// One continuous peduncle-to-lip profile for the shared Betta bind mesh. Measurements
// are in its longitudinal frame, rather than scaling the head around a separate pivot.
public readonly struct BettaBodyProfile
{
    const float ReferenceLength = .172214f;
    static readonly float[] Stations = { -.175f, -.15f, -.125f, -.10f, -.075f, -.055f, -.035f, -.02f, -.005f, .005f, .015f, .025f, .03f };
    // Source envelope measured on BODY cross sections; fins beyond it are feathered out.
    static readonly float[] SourceTop = { .014f, .0218f, .0318f, .0396f, .0395f, .0367f, .0307331f, .0253f, .0187f, .014f, .009f, .0017f, .001f };
    static readonly float[] SourceBottom = { -.023f, -.0342f, -.0372f, -.04f, -.043f, -.0435f, -.0394353f, -.0343f, -.0269f, -.0197f, -.0115f, -.0042f, -.002f };
    static readonly float[] SourceWidth = { .0045f, .0058f, .0078f, .0104f, .0121f, .0128f, .0126f, .0119f, .0109f, .0095f, .0082f, .002f, .001f };
    // The cheek flows into a fuller anterior shoulder. The belly is deepest just
    // behind the operculum, then lifts into the existing substantial peduncle.
    static readonly float[] TargetTop = { .0115f, .0225f, .030f, .0345f, .036f, .0355f, .0325f, .0278f, .0228f, .0178f, .0098f, .0017f, .001f };
    static readonly float[] TargetBottom = { -.0135f, -.0205f, -.0235f, -.0285f, -.034f, -.0375f, -.0405f, -.037f, -.0305f, -.0235f, -.013f, -.0042f, -.002f };
    static readonly float[] TargetWidth = { .0068f, .0095f, .0125f, .016f, .019f, .0205f, .0219f, .0182f, .0152f, .0122f, .0088f, .002f, .001f };

    // Outer opaque-skin sections measured every 5 mm; sampling only the centre
    // line misses the small belly/root lobes and leaves steps in the silhouette.
    static readonly float[] OutlineStations = { -0.1750000f, -0.1700000f, -0.1650000f, -0.1600000f, -0.1550000f, -0.1500000f, -0.1450000f, -0.1400000f, -0.1350000f, -0.1300000f, -0.1250000f, -0.1200000f, -0.1150000f, -0.1100000f, -0.1050000f, -0.1000000f, -0.0950000f, -0.0900000f, -0.0850000f, -0.0800000f, -0.0750000f, -0.0700000f, -0.0650000f, -0.0600000f, -0.0550000f, -0.0500000f, -0.0450000f, -0.0400000f, -0.0350000f };
    static readonly float[] OutlineTop = { 0.0175753f, 0.0159524f, 0.0171125f, 0.0185786f, 0.0201156f, 0.0217915f, 0.0237279f, 0.0259381f, 0.0280926f, 0.0300319f, 0.0317717f, 0.0334133f, 0.0350157f, 0.0365634f, 0.0381009f, 0.0396208f, 0.0403165f, 0.0401636f, 0.0399815f, 0.0397883f, 0.0395110f, 0.0391984f, 0.0388117f, 0.0378482f, 0.0367339f, 0.0357583f, 0.0342836f, 0.0324935f, 0.0307331f };
    static readonly float[] OutlineBottom = { -0.0163988f, -0.0229461f, -0.0277901f, -0.0320793f, -0.0335995f, -0.0342342f, -0.0348586f, -0.0354694f, -0.0360584f, -0.0366369f, -0.0372021f, -0.0377605f, -0.0383158f, -0.0388723f, -0.0394289f, -0.0399967f, -0.0405668f, -0.0411566f, -0.0417525f, -0.0423623f, -0.0429812f, -0.0436056f, -0.0442375f, -0.0443213f, -0.0435143f, -0.0426567f, -0.0417135f, -0.0406653f, -0.0394353f };

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
    static float Sample(float[] values, float s, float[] knots = null)
    {
        knots ??= Stations;
        int i = 0;
        while (i < knots.Length - 2 && s > knots[i + 1]) i++;
        float h = knots[i + 1] - knots[i];
        float Slope(int j)
        {
            if (j == 0) return (values[1] - values[0]) / (knots[1] - knots[0]);
            if (j == knots.Length - 1) return (values[j] - values[j - 1]) / (knots[j] - knots[j - 1]);
            float a = (values[j] - values[j - 1]) / (knots[j] - knots[j - 1]);
            float b = (values[j + 1] - values[j]) / (knots[j + 1] - knots[j]);
            if (a * b <= 0f) return 0f;
            float w1 = 2f * (knots[j + 1] - knots[j]) + knots[j] - knots[j - 1];
            float w2 = knots[j + 1] - knots[j] + 2f * (knots[j] - knots[j - 1]);
            return (w1 + w2) / (w1 / a + w2 / b);
        }
        float t = Mathf.Clamp01((s - knots[i]) / h), t2 = t * t, t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * values[i] + (t3 - 2f * t2 + t) * h * Slope(i)
            + (-2f * t3 + 3f * t2) * values[i + 1] + (t3 - t2) * h * Slope(i + 1);
    }

    // Fin membranes close to the skin follow the torso; their free area retains
    // the ray bones. Unlike the sculpt's tail feather, this covers the whole BODY.
    public float AttachmentWeight(Vector3 point)
    {
        Vector3 d = (point - origin) / size;
        float s = Vector3.Dot(d, forward), v = Vector3.Dot(d, up);
        float top = s <= -.035f ? Sample(OutlineTop, s, OutlineStations) : Sample(SourceTop, s);
        float bottom = s <= -.035f ? Sample(OutlineBottom, s, OutlineStations) : Sample(SourceBottom, s);
        float middle = (top + bottom) * .5f, radius = Mathf.Max(.001f, (top - bottom) * .5f);
        float width = Mathf.Max(.001f, Sample(SourceWidth, s));
        float radial = Mathf.Sqrt(d.x * d.x / (width * width) + (v - middle) * (v - middle) / (radius * radius));
        float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 1.3f) / .8f));
        weight *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-s - .18f) / .04f));
        return weight;
    }

    public Vector3 Map(Vector3 point)
    {
        Vector3 d = (point - origin) / size;
        float s = Vector3.Dot(d, forward), v = Vector3.Dot(d, up);
        float top = s <= -.035f ? Sample(OutlineTop, s, OutlineStations) : Sample(SourceTop, s);
        float bottom = s <= -.035f ? Sample(OutlineBottom, s, OutlineStations) : Sample(SourceBottom, s);
        float middle = (top + bottom) * .5f, radius = Mathf.Max(.001f, (top - bottom) * .5f);
        float width = Mathf.Max(.001f, Sample(SourceWidth, s));
        float radial = Mathf.Sqrt(d.x * d.x / (width * width) + (v - middle) * (v - middle) / (radius * radius));
        // A broad feather keeps the fuller torso from folding the surrounding
        // attachment surface back on itself as the displacement returns to zero.
        float influence = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 1.3f) / 2.1f));
        influence *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s + .21f) / .025f));
        // Shorten the snout as one smooth translation of its tissues, including
        // the lips beyond the transverse sculpt's feather. Retain the source jaw
        // opening and propagate this same field to eyes and membrane attachments.
        float snout = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .002f) / .023f))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .04f) / .02f)));
        Vector3 mouthShift = forward * (-.0018f * snout * influence * size);
        influence *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .027f) / .008f));
        if (influence <= 0f) return point + mouthShift;
        float newTop = Sample(TargetTop, s), newBottom = Sample(TargetBottom, s);
        float skull = Mathf.Exp(-Mathf.Pow((s + .005f) / .04f, 2f));
        float variation = 1f + (proportion - 1f) * .5f * skull;
        float newMiddle = (newTop + newBottom) * .5f;
        float newRadius = (newTop - newBottom) * .5f * variation;
        // Width/height scaling alone preserves the source's flat cheeks and belly
        // corners. Ease its normalized sections toward an oval, retaining some
        // radial depth for the operculum and other overlapping skin surfaces.
        // This shared field also moves fin roots, so no attachment is pulled apart.
        float roundness = .72f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s + .195f) / .025f))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .002f) / .018f)));
        float surface = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - .3f) / .35f))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 1.3f) / 1.3f)));
        float roundedRadius = Mathf.Lerp(radial, 1f, roundness * surface);
        float radialScale = radial > .0001f ? roundedRadius / radial : 1f;
        float newV = newMiddle + (v - middle) / radius * newRadius * radialScale;
        float newX = d.x * Sample(TargetWidth, s) / width * variation * radialScale;
        return point + mouthShift + (Vector3.right * (newX - d.x) + up * (newV - v)) * (size * influence);
    }
}
