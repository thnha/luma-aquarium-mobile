using System.Collections.Generic;
using UnityEngine;

// Eight named driftwood species from one branch grower; ported from the approved 3D preview.
// Vertex colours carry each species' wood colour over the neutral DriftwoodGrey-v3 bark map.
// Each species is a parameter set (arrays are per branch depth, 0 = trunk). The same species
// and seed always produce the same mesh, so rebuilding the catalog never reshuffles the shop.
public static class DriftwoodSpeciesBuilder
{
    public enum Shape { Upright, Slanted, Lying, V }
    public enum Tone { Grain, Blend, TwoTone }

    public sealed class Species
    {
        public string id, name, description; public int price; public Shape shape;
        public int depth; public float[] len, rDec, start, up, wander; public int[] kids; public Vector2[] spread;
        public float r0, curl, ridgeK, ridge, sharp, twist, lump, lumpF, tip;
        public Color colA, colB; public Tone tone;
    }

    public static readonly Species[] All =
    {
        new Species { id="wood-linh-sam", name="Lũa Linh Sam", price=220, shape=Shape.Slanted,
            description="Gỗ cứng nâu đen, vân xoắn, chìm ngay · Có 3 vị trí gắn cây hoặc rêu",
            depth=2, len=new[]{.56f,.24f,.11f}, r0=.058f, rDec=new[]{.48f,.5f}, kids=new[]{4,2}, start=new[]{.3f,.35f},
            spread=new[]{new Vector2(22,50),new Vector2(25,50)}, up=new[]{.05f,.05f,.06f}, wander=new[]{.16f,.2f,.22f},
            curl=2.2f, ridgeK=9, ridge=.11f, sharp=2.5f, twist=9, lump=.14f, lumpF=9, tip=.12f,
            colA=new Color(.3f,.2f,.14f), colB=new Color(.12f,.08f,.06f), tone=Tone.Grain },
        new Species { id="wood-hai-son-tung", name="Lũa Hải Sơn Tùng", price=240, shape=Shape.Upright,
            description="Nhiều nhánh nhỏ khẳng khiu, hợp làm tán cây hoặc rừng cây · Có 3 vị trí gắn",
            depth=3, len=new[]{.5f,.24f,.13f,.065f}, r0=.028f, rDec=new[]{.55f,.6f,.62f}, kids=new[]{6,4,3}, start=new[]{.22f,.25f,.3f},
            spread=new[]{new Vector2(18,42),new Vector2(22,50),new Vector2(25,55)}, up=new[]{.07f,.09f,.1f,.1f}, wander=new[]{.18f,.22f,.28f,.3f},
            curl=3, ridgeK=5, ridge=.06f, sharp=1.5f, twist=4, lump=.08f, lumpF=14, tip=.12f,
            colA=new Color(.52f,.42f,.33f), colB=new Color(.32f,.25f,.19f), tone=Tone.Grain },
        new Species { id="wood-do-quyen", name="Lũa Đỗ Quyên", price=230, shape=Shape.V,
            description="Thân mịn uốn lượn, vàng sáng chuyển nâu đỏ · Hợp bố cục chữ V · Có 3 vị trí gắn",
            depth=2, len=new[]{.6f,.3f,.14f}, r0=.042f, rDec=new[]{.55f,.55f}, kids=new[]{3,2}, start=new[]{.35f,.35f},
            spread=new[]{new Vector2(28,58),new Vector2(28,55)}, up=new[]{.02f,.03f,.04f}, wander=new[]{.3f,.3f,.3f},
            curl=1.6f, ridgeK=4, ridge=.015f, sharp=1, twist=1, lump=.05f, lumpF=4, tip=.16f,
            colA=new Color(.78f,.6f,.34f), colB=new Color(.55f,.27f,.15f), tone=Tone.Blend },
        new Species { id="wood-spider", name="Lũa Spider", price=190, shape=Shape.Upright,
            description="Chùm rễ tua tủa tỏa từ một gốc nhỏ · Có 3 vị trí gắn cây hoặc rêu",
            depth=2, len=new[]{.09f,.4f,.16f}, r0=.045f, rDec=new[]{.36f,.5f}, kids=new[]{9,2}, start=new[]{.75f,.4f},
            spread=new[]{new Vector2(55,125),new Vector2(25,55)}, up=new[]{.04f,.02f,.05f}, wander=new[]{.05f,.34f,.36f},
            curl=2.5f, ridgeK=3, ridge=.03f, sharp=1, twist=2, lump=.07f, lumpF=10, tip=.08f,
            colA=new Color(.68f,.54f,.38f), colB=new Color(.45f,.33f,.22f), tone=Tone.Grain },
        new Species { id="wood-san-da", name="Lũa Săn Đá", price=250, shape=Shape.Upright,
            description="Gỗ nặng, gân guốc nứt nẻ như đá, hợp phối cùng đá · Có 3 vị trí gắn",
            depth=1, len=new[]{.44f,.32f}, r0=.07f, rDec=new[]{.6f}, kids=new[]{3}, start=new[]{.22f},
            spread=new[]{new Vector2(48,80)}, up=new[]{.03f,.04f}, wander=new[]{.14f,.18f},
            curl=1.8f, ridgeK=13, ridge=.22f, sharp=7, twist=5, lump=.24f, lumpF=7, tip=.2f,
            colA=new Color(.44f,.4f,.36f), colB=new Color(.27f,.23f,.2f), tone=Tone.Grain },
        new Species { id="wood-mopani", name="Lũa Mopani", price=260, shape=Shape.Lying,
            description="Khối gỗ dày hai màu sáng tối, chìm ngay · Có 3 vị trí gắn cây hoặc rêu",
            depth=1, len=new[]{.3f,.2f}, r0=.07f, rDec=new[]{.64f}, kids=new[]{3}, start=new[]{.2f},
            spread=new[]{new Vector2(45,85)}, up=new[]{.02f,.04f}, wander=new[]{.3f,.34f},
            curl=1.8f, ridgeK=3, ridge=.02f, sharp=1, twist=1, lump=.28f, lumpF=6, tip=.5f,
            colA=new Color(1,.84f,.58f), colB=new Color(.2f,.1f,.05f), tone=Tone.TwoTone },
        new Species { id="wood-tra-gung", name="Lũa Trà Gừng", price=120, shape=Shape.Upright,
            description="Nhánh nhỏ đan chằng chịt, gọn cho hồ mini · Có 3 vị trí gắn",
            depth=3, len=new[]{.26f,.15f,.09f,.05f}, r0=.016f, rDec=new[]{.62f,.62f,.65f}, kids=new[]{4,3,2}, start=new[]{.2f,.2f,.25f},
            spread=new[]{new Vector2(35,85),new Vector2(35,85),new Vector2(30,75)}, up=new[]{.02f,.02f,.02f,.02f}, wander=new[]{.38f,.42f,.45f,.45f},
            curl=4, ridgeK=4, ridge=.05f, sharp=1, twist=3, lump=.08f, lumpF=20, tip=.15f,
            colA=new Color(.5f,.36f,.24f), colB=new Color(.33f,.23f,.15f), tone=Tone.Grain },
        new Species { id="wood-bonsai", name="Lũa Bonsai", price=320, shape=Shape.Upright,
            description="Dáng cổ thụ thu nhỏ: thân uốn chữ S, tầng cành xòe ngang · Có 3 vị trí gắn",
            depth=3, len=new[]{.44f,.2f,.1f,.05f}, r0=.05f, rDec=new[]{.36f,.55f,.6f}, kids=new[]{5,4,3}, start=new[]{.5f,.2f,.2f},
            spread=new[]{new Vector2(68,92),new Vector2(25,60),new Vector2(20,50)}, up=new[]{.05f,-.01f,.01f,.03f}, wander=new[]{.28f,.15f,.2f,.25f},
            curl=1.1f, ridgeK=7, ridge=.09f, sharp=2, twist=6, lump=.15f, lumpF=10, tip=.14f,
            colA=new Color(.42f,.31f,.22f), colB=new Color(.24f,.17f,.12f), tone=Tone.Grain },
    };

    public static Mesh Build(Species sp,int seed,out Vector3[] plantMounts,out float footprint)
    {
        var g = new Grower(sp, seed);
        float r0 = sp.r0;
        switch (sp.shape)
        {
            case Shape.Upright: g.Grow(Vector3.zero,new Vector3((g.Next()-.5f)*.2f,1,(g.Next()-.5f)*.2f),0,r0,1,1); break;
            case Shape.Slanted: g.Grow(new Vector3(-.1f,0,0),new Vector3(.8f,.6f,.05f),0,r0,1.1f,.35f); break;
            case Shape.Lying: g.Grow(new Vector3(-.25f,r0*.55f,0),new Vector3(1,.06f,.12f),0,r0,1.15f,0); break;
            default:
                g.Grow(new Vector3(-.015f,0,0),new Vector3(-.6f,.8f,.05f),0,r0*.8f,.95f,.5f);
                g.Grow(new Vector3(.015f,0,.01f),new Vector3(.62f,.78f,-.05f),0,r0*.8f,.95f,.5f); break;
        }
        return g.Finish(out plantMounts,out footprint);
    }

    private static float Hash(int x,int y,int z)
    {
        unchecked
        {
            int h = x*374761393 + y*668265263 + z*1274126177;
            h = (h ^ (int)((uint)h >> 13)) * 1274126177;
            return (float)((uint)(h ^ (int)((uint)h >> 16)) / 4294967295.0);
        }
    }

    // Smooth value noise in 0..1; the stone and coral builder shares it.
    public static float Noise(float x,float y,float z)
    {
        int X = Mathf.FloorToInt(x), Y = Mathf.FloorToInt(y), Z = Mathf.FloorToInt(z);
        float fx = x-X, fy = y-Y, fz = z-Z;
        fx = fx*fx*(3-2*fx); fy = fy*fy*(3-2*fy); fz = fz*fz*(3-2*fz);
        return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Hash(X,Y,Z),Hash(X+1,Y,Z),fx),Mathf.Lerp(Hash(X,Y+1,Z),Hash(X+1,Y+1,Z),fx),fy),
            Mathf.Lerp(Mathf.Lerp(Hash(X,Y,Z+1),Hash(X+1,Y,Z+1),fx),Mathf.Lerp(Hash(X,Y+1,Z+1),Hash(X+1,Y+1,Z+1),fx),fy),fz);
    }

    private sealed class Grower
    {
        private readonly Species sp;
        private uint state;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>(), seams = new List<int>();
        private readonly List<Vector3> mounts = new List<Vector3>();
        private static readonly Vector3 LightSide = new Vector3(.6f,.5f,-.6f).normalized;

        public Grower(Species species,int seed) { sp = species; state = (uint)(seed*7919 + species.id.Length*131); }

        // mulberry32, as in the preview.
        public float Next()
        {
            unchecked
            {
                state += 0x6D2B79F5; uint t = state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (float)((t ^ (t >> 14)) / 4294967296.0);
            }
        }

        private static Vector3 Perpendicular(Vector3 t) => Vector3.Cross(t,Mathf.Abs(t.y)<.9f?Vector3.up:Vector3.right).normalized;

        public void Grow(Vector3 p0,Vector3 dir,int depth,float rStart,float lenScale,float upMul)
        {
            float length = sp.len[depth]*lenScale*(.8f+.4f*Next()), rEnd = rStart*sp.tip;
            float step = Mathf.Min(.02f,Mathf.Max(.01f,rStart*.9f));
            int n = Mathf.Max(4,Mathf.CeilToInt(length/step));
            float o0 = Next()*100, o1 = Next()*100, o2 = Next()*100;
            var points = new List<Vector3> { p0 }; var radii = new List<float> { rStart };
            Vector3 d = dir.normalized;
            for (int i = 1; i <= n; i++)
            {
                float s = i/(float)n, c = s*sp.curl;
                var wander = new Vector3(Noise(c+o0,o1,0)-.5f,Noise(o1,c+o2,1)-.5f,Noise(o2,2,c+o0)-.5f)*(2*sp.wander[depth]*8/n);
                d = (d + wander + Vector3.up*(sp.up[depth]*upMul*8/n)).normalized;
                float r = Mathf.Lerp(rStart,rEnd,Mathf.Pow(s,.9f));
                Vector3 p = points[i-1] + d*(length/n);
                // Branches rest on the sand instead of sinking through it.
                if (p.y < r*.55f) { p.y = r*.55f; if (d.y < 0) { d.y = 0; d.Normalize(); } }
                points.Add(p); radii.Add(r);
            }
            Tube(points,radii,depth==0);
            if (depth <= 1) foreach (float t in depth==0 ? new[]{.3f,.62f,.85f} : new[]{.45f,.75f})
            {
                int i = Mathf.Clamp(Mathf.FloorToInt(t*n+.5f),1,n-1);
                mounts.Add(points[i] + Vector3.up*radii[i]*.9f);
            }
            if (depth >= sp.depth) return;
            int kids = sp.kids[depth]; float first = sp.start[depth], phase = Next()*Mathf.PI*2;
            for (int c = 0; c < kids; c++)
            {
                float t = first + (1-first-.06f)*((c+.2f+Next()*.6f)/kids);
                int i = Mathf.Clamp(Mathf.FloorToInt(t*n+.5f),1,n-1);
                Vector3 tangent = (points[i+1]-points[i-1]).normalized, side = Perpendicular(tangent);
                float phi = phase + c*2.39996f + Next()*.8f;
                Vector3 perp = side*Mathf.Cos(phi) + Vector3.Cross(tangent,side)*Mathf.Sin(phi);
                float angle = Mathf.Lerp(sp.spread[depth].x,sp.spread[depth].y,Next())*Mathf.Deg2Rad;
                Vector3 childDir = tangent*Mathf.Cos(angle) + perp*Mathf.Sin(angle);
                Grow(points[i],childDir,depth+1,Mathf.Max(.0028f,radii[i]*sp.rDec[depth]*(.85f+.3f*Next())),1-t*.35f,1);
            }
        }

        private void Tube(List<Vector3> points,List<float> radii,bool baseCap)
        {
            // Round every thick end so blunt tips and side-branch roots never show an open ring;
            // hairline twigs already taper shut and skip the extra rings.
            Vector3 endDir = (points[points.Count-1]-points[points.Count-2]).normalized, end = points[points.Count-1];
            float rLast = radii[radii.Count-1];
            if (rLast > .003f) for (int k = 1; k <= 4; k++)
            {
                float f = k/4f*Mathf.PI/2;
                points.Add(end + endDir*(rLast*Mathf.Sin(f)*.9f)); radii.Add(rLast*Mathf.Cos(f)+1e-5f);
            }
            if (!baseCap && radii[0] > .005f)
            {
                Vector3 startDir = (points[1]-points[0]).normalized, start = points[0]; float rFirst = radii[0];
                for (int k = 1; k <= 3; k++)
                {
                    float f = k/3f*Mathf.PI/2;
                    points.Insert(0,start - startDir*(rFirst*Mathf.Sin(f)*.45f)); radii.Insert(0,rFirst*Mathf.Cos(f)+1e-5f);
                }
            }
            int n = points.Count, baseIndex = vertices.Count;
            float rMax = 0; foreach (float r in radii) rMax = Mathf.Max(rMax,r);
            int sides = Mathf.Clamp(Mathf.FloorToInt(rMax*320+.5f),5,18);
            float uRepeat = Mathf.Max(1,Mathf.FloorToInt(radii[0]*40+.5f)), distance = 0;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = (points[Mathf.Min(n-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
                // Parallel transport keeps the bark grain from twisting where the branch bends.
                normal = i == 0 ? Perpendicular(tangent) : (normal - tangent*Vector3.Dot(normal,tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent,normal);
                if (i > 0) distance += Vector3.Distance(points[i],points[i-1]);
                for (int j = 0; j <= sides; j++)
                {
                    float a = j/(float)sides*Mathf.PI*2;
                    Vector3 radial = normal*Mathf.Cos(a) + binormal*Mathf.Sin(a), q = points[i] + radial*radii[i];
                    float lumps = Noise(q.x*sp.lumpF+11,q.y*sp.lumpF+3,q.z*sp.lumpF+7);
                    float grain = Noise(q.x*40,q.y*40+distance*20,q.z*40);
                    float ridge = 1-2*Mathf.Pow(Mathf.Abs(Mathf.Sin(.5f*sp.ridgeK*a+sp.twist*distance+2.5f*grain)),sp.sharp);
                    float shape = 1 + sp.ridge*ridge + sp.lump*(lumps*2-1);
                    Vector3 p = points[i] + radial*(radii[i]*Mathf.Max(.35f,shape));
                    vertices.Add(p); uvs.Add(new Vector2(j/(float)sides*uRepeat,distance*3));
                    float tone = sp.tone == Tone.TwoTone ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,.53f,Noise(p.x*5+9,p.y*5,p.z*5)*.75f+(Vector3.Dot(radial,LightSide)*.5f+.5f)*.35f))
                        : sp.tone == Tone.Blend ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.7f,Noise(p.x*2.2f+5,p.y*2.2f,p.z*2.2f)))
                        : .35f+.5f*grain;
                    float groove = ridge < 0 ? 1+.45f*ridge*Mathf.Min(1,sp.ridge*5) : 1, dirt = 1-.25f*Mathf.Max(0,.06f-p.y)/.06f;
                    colors.Add(Color.Lerp(sp.colB,sp.colA,tone)*(groove*dirt*1.05f));
                    if (j == sides) { seams.Add(baseIndex+i*(sides+1)); seams.Add(baseIndex+i*(sides+1)+sides); }
                    if (i < n-1 && j < sides)
                    {
                        int v = baseIndex+i*(sides+1)+j;
                        triangles.AddRange(new[]{v,v+sides+1,v+1,v+1,v+sides+1,v+sides+2});
                    }
                }
            }
            if (!baseCap) return;
            int center = vertices.Count;
            vertices.Add(points[0] - (points[1]-points[0]).normalized*radii[0]*.15f); uvs.Add(new Vector2(.5f,0));
            colors.Add(sp.colB*.9f);
            for (int j = 0; j < sides; j++) triangles.AddRange(new[]{center,baseIndex+j,baseIndex+j+1});
        }

        public Mesh Finish(out Vector3[] plantMounts,out float footprint)
        {
            var bounds = new Bounds(vertices[0],Vector3.zero);
            foreach (var v in vertices) bounds.Encapsulate(v);
            // Centre on the trunk footprint and stand on the sand.
            var offset = new Vector3(-bounds.center.x,-Mathf.Min(0,bounds.min.y),-bounds.center.z);
            for (int i = 0; i < vertices.Count; i++) vertices[i] += offset;
            var mesh = new Mesh { indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            // The UV seam duplicates each ring's first vertex; share one normal so it doesn't show as a stripe.
            var normals = mesh.normals;
            for (int k = 0; k < seams.Count; k += 2)
            {
                Vector3 shared = (normals[seams[k]]+normals[seams[k+1]]).normalized;
                normals[seams[k]] = normals[seams[k+1]] = shared;
            }
            mesh.normals = normals; mesh.RecalculateBounds();
            // Keep mounts far enough apart that two attached plants never overlap.
            var chosen = new List<Vector3>();
            foreach (var m in mounts)
                if (chosen.Count < 3 && chosen.TrueForAll(c => Vector3.Distance(c,m) >= .06f)) chosen.Add(m);
            plantMounts = chosen.ConvertAll(m => m + offset).ToArray();
            footprint = Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.5f,.14f,.32f);
            return mesh;
        }
    }
}
