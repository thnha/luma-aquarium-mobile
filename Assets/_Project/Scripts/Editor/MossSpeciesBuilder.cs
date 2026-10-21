using System.Collections.Generic;
using UnityEngine;

// Named aquarium mosses from one frond grower. Every pad grows around the local origin with its
// base at y = 0, so one prefab sits on the sand or on a wood/rock mount. Vertex colours carry the
// dark-base to young-tip gradient over a white Nature material. Each species uses a fixed seed, so
// rebuilding the catalog always produces the same mesh.
public static class MossSpeciesBuilder
{
    public enum Habit { Mat, Weeping, Flame, Fan }

    public sealed class Species
    {
        public string id, name, description; public int price; public Habit habit;
        public int fronds, branchlets; public float radius, height, length, leaf;
        public Color dark, light;
    }

    public static readonly Species[] All =
    {
        new Species { id="moss-mini-taiwan", name="Rêu Mini Taiwan", price=90, habit=Habit.Mat,
            description="Taxiphyllum alternans · Lá li ti mọc thành thảm dày · Gắn lên đá, lũa hoặc trải nền",
            fronds=40, branchlets=4, radius=.075f, height=.035f, length=.045f, leaf=.0055f,
            dark=new Color(.1f,.27f,.07f), light=new Color(.36f,.62f,.15f) },
        new Species { id="moss-weeping", name="Rêu Weeping", price=120, habit=Habit.Weeping,
            description="Vesicularia ferriei · Nhánh rủ như liễu, đẹp nhất trên lũa cao · Gắn lên đá, lũa hoặc nền",
            fronds=30, branchlets=4, radius=.085f, height=.055f, length=.085f, leaf=.006f,
            dark=new Color(.14f,.33f,.08f), light=new Color(.45f,.7f,.2f) },
        new Species { id="moss-flame", name="Rêu Flame", price=130, habit=Habit.Flame,
            description="Taxiphyllum 'Flame' · Nhánh vươn thẳng, xoắn như ngọn lửa xanh · Gắn lên đá, lũa hoặc nền",
            fronds=26, branchlets=0, radius=.05f, height=.095f, length=.095f, leaf=.0075f,
            dark=new Color(.07f,.27f,.1f), light=new Color(.25f,.55f,.18f) },
    };

    // Rebuilds the released "mini-fiss" item (kind 11) with its fin-shaped fronds; ID and kind are unchanged.
    public static readonly Species MiniFiss = new Species { id="mini-fiss", habit=Habit.Fan,
        fronds=90, branchlets=0, radius=.06f, height=.024f, length=.018f, leaf=.0065f,
        dark=new Color(.11f,.3f,.08f), light=new Color(.33f,.58f,.15f) };

    public static Mesh Build(Species sp,out float footprint)
    {
        var g = new Grower(sp);
        for (int n = 0; n < sp.fronds; n++)
        {
            switch (sp.habit)
            {
                case Habit.Mat: g.MatFrond(n); break;
                case Habit.Weeping: g.WeepingFrond(n); break;
                case Habit.Flame: g.FlameStrand(n); break;
                default: g.FanFrond(n); break;
            }
        }
        return g.Finish(out footprint);
    }

    private sealed class Grower
    {
        private const float Floor = .002f;
        private readonly Species sp;
        private readonly System.Random rng;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public Grower(Species species) { sp = species; rng = new System.Random(4271 + species.id.Length*97); }

        private float Next() => (float)rng.NextDouble();

        private static Vector3 Perpendicular(Vector3 t) => Vector3.Cross(t,Mathf.Abs(t.y)<.9f?Vector3.up:Vector3.right).normalized;

        // Young growth at the tips is lighter than the old base, with a little per-leaf scatter.
        private Color Tone(float t) => Color.Lerp(sp.dark,sp.light,Mathf.Clamp01(Mathf.Pow(t,.8f)*(.8f+.3f*Next())));

        // A walk from start along dir that bends under `pull` and never dips into the sand.
        private List<Vector3> Path(Vector3 start,Vector3 dir,float length,int segments,Vector3 pull,float wander)
        {
            var points = new List<Vector3> { start };
            Vector3 d = dir.normalized, p = start;
            for (int i = 1; i <= segments; i++)
            {
                var jitter = new Vector3(Next()-.5f,Next()-.5f,Next()-.5f)*(2*wander);
                d = (d + pull/segments + jitter).normalized;
                p += d*(length/segments);
                if (p.y < Floor) { p.y = Floor; if (d.y < 0) { d.y = 0; d.Normalize(); } }
                points.Add(p);
            }
            return points;
        }

        // A flat diamond leaf. The Nature shader draws both faces, so one side is enough.
        private void Leaf(Vector3 p,Vector3 dir,Vector3 normal,float length,float width,Color color,float t)
        {
            Vector3 side = Vector3.Cross(normal,dir);
            side = side.sqrMagnitude < 1e-6f ? Perpendicular(dir) : side.normalized;
            int i = vertices.Count;
            vertices.Add(p); vertices.Add(p + dir*(length*.45f) + side*(width*.5f));
            vertices.Add(p + dir*length); vertices.Add(p + dir*(length*.45f) - side*(width*.5f));
            uvs.Add(new Vector2(.5f,t)); uvs.Add(new Vector2(1,t)); uvs.Add(new Vector2(.5f,t)); uvs.Add(new Vector2(0,t));
            colors.Add(color*.85f); colors.Add(color); colors.Add(color*1.1f); colors.Add(color);
            triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }

        // The wiry stem shows as a thin dark ribbon between the leaves.
        private void Stem(List<Vector3> points,float width)
        {
            int start = vertices.Count, n = points.Count;
            for (int i = 0; i < n; i++)
            {
                float t = i/(float)(n-1);
                Vector3 tangent = (points[Mathf.Min(n-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
                Vector3 side = Perpendicular(tangent)*(width*.5f*(1-.6f*t));
                vertices.Add(points[i]-side); vertices.Add(points[i]+side);
                uvs.Add(new Vector2(0,t)); uvs.Add(new Vector2(1,t));
                Color c = sp.dark*(.75f+.2f*t); colors.Add(c); colors.Add(c);
                if (i < n-1) { int v = start+i*2; triangles.AddRange(new[]{v,v+2,v+1,v+1,v+2,v+3}); }
            }
        }

        // Leaves in two ranks along a path, angled forward like a feather; tFrom/tTo place the
        // path within the whole frond for colour and leaf size.
        private void Pinnate(List<Vector3> points,Vector3 normal,float leaf,float spread,float tFrom,float tTo)
        {
            int n = points.Count;
            for (int i = 1; i < n; i++)
            {
                float t = Mathf.Lerp(tFrom,tTo,i/(float)(n-1));
                Vector3 tangent = (points[Mathf.Min(n-1,i+1)]-points[i-1]).normalized;
                Vector3 side = Vector3.Cross(normal,tangent);
                side = side.sqrMagnitude < 1e-6f ? Perpendicular(tangent) : side.normalized;
                float size = leaf*(1-.45f*t)*(.85f+.3f*Next());
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 dir = (tangent*(1-spread) + side*(s*spread) + normal*.15f).normalized;
                    Leaf(points[i],dir,normal,size,size*.5f,Tone(t),t);
                }
            }
            Leaf(points[n-1],(points[n-1]-points[n-2]).normalized,normal,leaf*.6f,leaf*.3f,Tone(tTo),tTo);
        }

        // Feathery side shoots alternate along a frond and shorten toward its tip, giving the
        // triangular frond silhouette of Taxiphyllum and Vesicularia mosses.
        private void Branchlets(List<Vector3> main,Vector3 normal,float mainLength,Vector3 pull)
        {
            int n = main.Count;
            for (int b = 0; b < sp.branchlets; b++)
            {
                float t = (b+.6f)/(sp.branchlets+.5f)*.8f;
                int i = Mathf.Clamp(Mathf.RoundToInt(t*(n-1)),1,n-2);
                Vector3 tangent = (main[i+1]-main[i-1]).normalized;
                Vector3 side = Vector3.Cross(normal,tangent);
                side = side.sqrMagnitude < 1e-6f ? Perpendicular(tangent) : side.normalized;
                float s = b%2 == 0 ? 1 : -1;
                var path = Path(main[i],tangent*.55f + side*s,mainLength*.38f*(1-t*.7f),3,pull*.4f,.08f);
                Stem(path,.0009f);
                Pinnate(path,normal,sp.leaf*.8f,.75f,t,Mathf.Min(1,t+.35f));
            }
        }

        // Mini Taiwan: a low mound of overlapping horizontal fronds.
        public void MatFrond(int n)
        {
            float a = n*2.39996f + Next()*.4f, core = Mathf.Sqrt(Next());
            var radial = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 start = radial*(core*sp.radius*.35f) + Vector3.up*(sp.height*.55f*(1-core*.6f)*(.4f+.6f*Next()));
            float length = sp.length*(.7f+.5f*Next());
            var pull = Vector3.down*.35f;
            var main = Path(start,radial + Vector3.up*(.3f+.25f*Next()),length,6,pull,.06f);
            var normal = Vector3.up;
            Stem(main,.0014f);
            Pinnate(main,normal,sp.leaf,.7f,0,1);
            Branchlets(main,normal,length,pull);
        }

        // Weeping: fronds leave the top of a mound, arch outward and droop toward the sand in tiers.
        public void WeepingFrond(int n)
        {
            float a = n*2.39996f + Next()*.4f, core = Mathf.Sqrt(Next());
            var radial = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 start = radial*(core*sp.radius*.22f) + Vector3.up*(sp.height*(.55f+.45f*Next())*(1-core*.4f));
            float length = sp.length*(.75f+.4f*Next());
            var pull = Vector3.down*(1.5f+.4f*Next());
            var main = Path(start,radial*.8f + Vector3.up*.6f,length,8,pull,.04f);
            var normal = Vector3.up;
            Stem(main,.0015f);
            Pinnate(main,normal,sp.leaf,.65f,0,1);
            Branchlets(main,normal,length,Vector3.down*2.2f);
        }

        // Flame: upright strands that twist as they climb, leaves pressed close to the stem.
        public void FlameStrand(int n)
        {
            float a = n*2.39996f, r = Mathf.Sqrt(Next())*sp.radius*.6f;
            Vector3 root = new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            float height = sp.height*(.6f+.45f*Next()), sway = .012f+.012f*Next(), turn = 3+2*Next(), phase = Next()*Mathf.PI*2;
            // Every strand leans a little the same way, like one flame licking upward.
            var lean = new Vector3(.18f,0,.06f);
            var points = new List<Vector3>();
            const int segments = 14;
            for (int i = 0; i <= segments; i++)
            {
                float t = i/(float)segments, phi = phase + t*turn;
                Vector3 outward = root.normalized*(r*.35f*t);
                points.Add(root + outward + Vector3.up*(height*t) + lean*(height*t*t) +
                    new Vector3(Mathf.Cos(phi),0,Mathf.Sin(phi))*(sway*t));
            }
            Stem(points,.0016f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i/(float)segments;
                Vector3 tangent = (points[Mathf.Min(segments,i+1)]-points[i-1]).normalized, side = Perpendicular(tangent);
                Vector3 other = Vector3.Cross(tangent,side);
                float size = sp.leaf*(1-.45f*t)*(.85f+.3f*Next());
                for (int k = 0; k < 2; k++)
                {
                    float phi = i*2.39996f + k*Mathf.PI;
                    Vector3 radial = side*Mathf.Cos(phi) + other*Mathf.Sin(phi);
                    Vector3 dir = (tangent*.8f + radial*.45f).normalized;
                    Leaf(points[i],dir,radial,size,size*.45f,Tone(t),t);
                }
            }
            Leaf(points[segments],(points[segments]-points[segments-1]).normalized,Perpendicular((points[segments]-points[segments-1]).normalized),
                sp.leaf*.6f,sp.leaf*.25f,Tone(1),1);
        }

        // Mini Fiss: short fronds of two flat leaf ranks, like a fish fin, packed into a velvet dome.
        public void FanFrond(int n)
        {
            float a = n*2.39996f + Next()*.3f, core = Mathf.Sqrt(Next());
            var radial = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 start = radial*(core*sp.radius*.75f);
            // Fronds lean outward at the rim and stand up in the middle, rounding the cushion.
            float lean = .25f+.9f*core;
            float length = sp.length*(.6f+.6f*Next())*(1.3f-.5f*core);
            var main = Path(start,radial*lean + Vector3.up + new Vector3(Next()-.5f,0,Next()-.5f)*.4f,length,4,Vector3.zero,.05f);
            // The fan opens sideways to the frond, so leaves face out of the cushion.
            var normal = radial*Mathf.Cos(a*3) + Vector3.Cross(Vector3.up,radial)*Mathf.Sin(a*3);
            Stem(main,.0008f);
            Pinnate(main,normal.normalized,sp.leaf,.85f,0,1);
        }

        public Mesh Finish(out float footprint)
        {
            float reach = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                // Leaves on fronds that reach the sand lie flat on it instead of poking through.
                var v = vertices[i]; if (v.y < 0) vertices[i] = new Vector3(v.x,0,v.z);
                reach = Mathf.Max(reach,Mathf.Abs(v.x),Mathf.Abs(v.z));
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            footprint = Mathf.Clamp(reach*.9f,.06f,.12f);
            return mesh;
        }
    }
}
