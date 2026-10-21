using System.Collections.Generic;
using UnityEngine;

// The imported BODY includes opaque back/belly vertices weighted to fin rays.
// Transfer those influences to the spine on cloned meshes, and blend membrane
// attachments onto the same spine so fin posing cannot notch the torso.
public static class BettaBodySkinning
{
    public static BoneWeight[] Fit(Mesh source, Transform[] bones, Vector3 head, Vector3 tail, bool[] skin = null)
    {
        BoneWeight[] result = source.boneWeights;
        Vector3[] positions = source.vertices;
        var fin = new bool[bones.Length];
        var spine = new List<(int index, float position)>();
        Vector3 forward = (head - tail).normalized;
        Matrix4x4[] bindposes = source.bindposes;
        for (int i = 0; i < bones.Length; i++)
        {
            string name = bones[i].name;
            fin[i] = name.StartsWith("Dorsal_") || name.StartsWith("Anal_")
                || name.StartsWith("Pelvic_") || name.StartsWith("Pectoral_")
                || name.StartsWith("Tail_") && name != "Tail_Base";
            if (name == "Head" || name == "Tail_Base" || name.StartsWith("Spine_"))
                spine.Add((i, Vector3.Dot(bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero) - head, forward)));
        }
        if (spine.Count < 2) return result;
        spine.Sort((a, b) => a.position.CompareTo(b.position));
        var profile = new BettaBodyProfile(head, tail, 1f);
        var indices = new int[6];
        var values = new float[6];
        int count = 0;
        void Add(int index, float value)
        {
            if (value <= 0f) return;
            int j = 0;
            while (j < count && indices[j] != index) j++;
            if (j == count) { indices[count] = index; values[count++] = 0f; }
            values[j] += value;
        }
        for (int i = 0; i < result.Length; i++)
        {
            float amount = skin != null && skin[i] ? 1f : profile.AttachmentWeight(positions[i]);
            if (amount <= 0f) continue;
            BoneWeight old = result[i];
            count = 0;
            float transfer = 0f;
            void Keep(int index, float value)
            {
                float moved = fin[index] ? value * amount : 0f;
                Add(index, value - moved);
                transfer += moved;
            }
            Keep(old.boneIndex0, old.weight0); Keep(old.boneIndex1, old.weight1);
            Keep(old.boneIndex2, old.weight2); Keep(old.boneIndex3, old.weight3);
            if (transfer <= 0f) continue;
            float s = Vector3.Dot(positions[i] - head, forward);
            int right = 1;
            while (right < spine.Count - 1 && s > spine[right].position) right++;
            var a = spine[right - 1]; var b = spine[right];
            float t = Mathf.InverseLerp(a.position, b.position, s);
            Add(a.index, transfer * (1f - t)); Add(b.index, transfer * t);

            // Unity's four-influence mesh format: keep the largest weights and
            // normalize after merging any pre-existing influence on the spine.
            for (int j = 0; j < Mathf.Min(4, count); j++)
                for (int k = j + 1; k < count; k++)
                    if (values[k] > values[j])
                    {
                        (values[j], values[k]) = (values[k], values[j]);
                        (indices[j], indices[k]) = (indices[k], indices[j]);
                    }
            float total = 0f;
            for (int j = 0; j < Mathf.Min(4, count); j++) total += values[j];
            for (int j = count; j < 4; j++) { indices[j] = 0; values[j] = 0f; }
            result[i] = new BoneWeight
            {
                boneIndex0 = indices[0], weight0 = values[0] / total,
                boneIndex1 = indices[1], weight1 = values[1] / total,
                boneIndex2 = indices[2], weight2 = values[2] / total,
                boneIndex3 = indices[3], weight3 = values[3] / total
            };
        }
        return result;
    }
}
