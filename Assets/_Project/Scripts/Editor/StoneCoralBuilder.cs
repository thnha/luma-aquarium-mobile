using System;
using System.Collections.Generic;
using UnityEngine;

// Named stones and corals grown from two seeded primitives: a welded, displaced blob (rocks,
// coral heads, polyp discs) and a welded tube (coral branches, rubble, stalks). Each piece is a
// form plus colours; vertex colours carry its colour. Stones and dead coral sit over the neutral
// RiverStone-v3 grain, live and artificial coral use a smooth untextured material. The same
// piece and seed always produce the same mesh, so rebuilding the catalog never reshuffles the shop.
public static partial class StoneCoralBuilder
{
    public enum Form { ElephantSkin, CoralRock, Lava, SeaPebbles, CoThach, TuyetSon, CoralRubble, CoralBlock, Mushroom, Zoanthid, PlasticBranch, PlasticFan,
        RiverPebbles, DragonStone, Seiryu, Cluster, Penjing, Cliff, DeepElephantSkin }

    public sealed class Piece
    {
        public string id, name, description; public int price; public Form form;
        public bool smooth; public Color colA, colB, accent;
        public bool IsCoral => form >= Form.CoralRubble && form <= Form.PlasticFan;
    }

    public static readonly Piece[] All =
    {
        new Piece { id="stone-elephant-skin", name="Đá da voi", price=180, form=Form.DeepElephantSkin,
            description="Mặt đá sần nhăn như da voi, xếp tầng tạo khe núi · Hợp hồ thủy sinh và hồ Koi",
            colA=new Color(.66f,.66f,.64f), colB=new Color(.4f,.4f,.39f), accent=new Color(.22f,.22f,.22f) },
        new Piece { id="stone-coral-rock", name="Đá san hô", price=150, form=Form.CoralRock,
            description="Xốp, nhẹ, nhiều lỗ; vừa làm nền vừa lọc, giữ pH cao ổn định · Hợp bể cá biển",
            colA=new Color(.93f,.89f,.79f), colB=new Color(.78f,.72f,.6f), accent=new Color(.48f,.42f,.32f) },
        new Piece { id="stone-lava-red", name="Đá nham thạch đỏ", price=110, form=Form.Lava,
            description="Xốp, lỗ li ti cho vi sinh trú ngụ và lọc nước · Lót nền hoặc phân tầng",
            colA=new Color(.5f,.24f,.17f), colB=new Color(.27f,.13f,.1f), accent=new Color(.08f,.06f,.06f) },
        new Piece { id="stone-sea-pebbles", name="Đá cuội biển", price=80, form=Form.SeaPebbles,
            description="Cuội tròn mượt nhiều màu, vẻ đẹp hoang sơ · Hợp hồ cá cảnh, thủy sinh lẫn hồ Koi",
            colA=new Color(.9f,.88f,.84f), colB=new Color(.3f,.3f,.32f), accent=new Color(.72f,.58f,.44f) },
        new Piece { id="stone-co-thach", name="Đá cổ thạch", price=260, form=Form.CoThach,
            description="Góc cạnh, vân sọc trắng vàng · Điểm nhấn cao cấp cho hòn non bộ",
            colA=new Color(.6f,.5f,.36f), colB=new Color(.38f,.33f,.27f), accent=new Color(.95f,.9f,.74f) },
        new Piece { id="stone-tuyet-son", name="Đá tuyết sơn", price=280, form=Form.TuyetSon,
            description="Nền đen vân trắng như tuyết phủ đỉnh núi · Điểm nhấn cao cấp cho hòn non bộ",
            colA=new Color(.2f,.2f,.21f), colB=new Color(.09f,.09f,.1f), accent=new Color(.93f,.93f,.9f) },
        new Piece { id="coral-rubble", name="San hô vụn", price=90, form=Form.CoralRubble,
            description="San hô thật dạng vụn · Lót nền hoặc làm vật liệu lọc tăng pH cho bể nước mặn",
            colA=new Color(.96f,.94f,.88f), colB=new Color(.84f,.8f,.7f), accent=new Color(.6f,.55f,.46f) },
        new Piece { id="coral-block", name="San hô khối", price=190, form=Form.CoralBlock,
            description="Khối san hô thật vân não · Dùng cho bể nước mặn, giúp ổn định pH",
            colA=new Color(.95f,.92f,.84f), colB=new Color(.72f,.65f,.52f), accent=new Color(.5f,.44f,.34f) },
        new Piece { id="coral-mushroom", name="San hô nấm", price=240, form=Form.Mushroom, smooth=true,
            description="San hô sống bể biển, đĩa màu rực rỡ · Cần ánh sáng và dòng chảy chuyên dụng",
            colA=new Color(.15f,.85f,.6f), colB=new Color(.55f,.18f,.75f), accent=new Color(1,.45f,.25f) },
        new Piece { id="coral-zoanthid", name="San hô đồng tiền", price=220, form=Form.Zoanthid, smooth=true,
            description="Cụm polyp tròn như đồng xu nhiều màu · San hô sống, cần ánh sáng và dòng chảy",
            colA=new Color(1,.55f,.12f), colB=new Color(.35f,.9f,.3f), accent=new Color(.85f,.25f,.6f) },
        new Piece { id="coral-artificial-branch", name="San hô nhân tạo nhánh", price=120, form=Form.PlasticBranch, smooth=true,
            description="Silicon an toàn, màu tươi, dễ vệ sinh · Mang phong cách biển vào hồ nước ngọt",
            colA=new Color(1,.42f,.62f), colB=new Color(.85f,.22f,.45f), accent=new Color(1,.86f,.9f) },
        new Piece { id="coral-artificial-fan", name="San hô nhân tạo quạt", price=130, form=Form.PlasticFan, smooth=true,
            description="Quạt biển silicon mềm, màu tươi, dễ vệ sinh · Hợp người mới chơi và hồ nước ngọt",
            colA=new Color(.95f,.2f,.28f), colB=new Color(1,.55f,.35f), accent=new Color(.6f,.08f,.18f) },
    };

    public static Mesh Build(Piece piece,int seed,out float footprint)
    {
        var m = new Mesher(piece,seed);
        switch (piece.form)
        {
            case Form.ElephantSkin: ElephantSkin(m); break;
            case Form.CoralRock: CoralRock(m); break;
            case Form.Lava: Lava(m); break;
            case Form.SeaPebbles: SeaPebbles(m); break;
            case Form.CoThach: Angular(m,false); break;
            case Form.TuyetSon: Angular(m,true); break;
            case Form.CoralRubble: CoralRubble(m); break;
            case Form.CoralBlock: CoralBlock(m); break;
            case Form.Mushroom: Mushroom(m); break;
            case Form.Zoanthid: Zoanthid(m); break;
            case Form.PlasticBranch: PlasticBranch(m); break;
            case Form.PlasticFan: PlasticFan(m); break;
            default: Redesigned(m); break;
        }
        return m.Finish(out footprint);
    }

    private static float Noise(Vector3 p) => DriftwoodSpeciesBuilder.Noise(p.x,p.y,p.z);
    private static float Fbm(Vector3 p)
    {
        return (Noise(p)*.53f + Noise(p*2.03f+Vector3.one*17)*.27f + Noise(p*4.1f+Vector3.one*31)*.13f + Noise(p*8.3f+Vector3.one*5)*.07f);
    }
    // 1 on the noise mid-line, falling off either side: thin connected creases.
    private static float Crease(float n,float sharp) => Mathf.Pow(1-Mathf.Abs(2*n-1),sharp);
    private static float Smooth(float a,float b,float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    private static Vector3 Offset(Mesher m) => new Vector3(m.Range(0,50),m.Range(0,50),m.Range(0,50));

    // Đá da voi: three leaning slabs whose skin is a network of fine wrinkles.
    private static void ElephantSkin(Mesher m)
    {
        var slabs = new[] { new Vector4(0,.1f,0,1), new Vector4(.15f,.06f,.07f,.6f), new Vector4(-.09f,.2f,-.02f,.62f) };
        foreach (var s in slabs)
        {
            Vector3 o = Offset(m), o2 = Offset(m); float k = s.w;
            Func<Vector3,float> wrinkle = d => Crease(Noise(d*7+o),9)*.6f + Crease(Noise(d*15+o2),12)*.4f;
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.17f,.11f,.13f)*k,Quaternion.Euler(m.Range(-14,14),m.Range(0,360),m.Range(-18,18)),
                k>.9f?32:24,k>.9f?44:32,
                d => 1 + .26f*(Fbm(d*1.6f+o)-.5f) - .07f*wrinkle(d),
                (d,p) => Color.Lerp(m.piece.colB,m.piece.colA,.3f+.6f*Fbm(p*24+o)) * (1-.5f*wrinkle(d)));
        }
    }

    // Đá san hô: two merged porous lumps pitted all over.
    private static void CoralRock(Mesher m)
    {
        var lumps = new[] { new Vector4(0,.08f,0,1), new Vector4(.1f,.06f,.05f,.68f), new Vector4(-.07f,.05f,.07f,.55f) };
        foreach (var s in lumps)
        {
            Vector3 o = Offset(m);
            Func<Vector3,float> pit = d => Smooth(.6f,.76f,Noise(d*10+o)) + .6f*Smooth(.66f,.8f,Noise(d*21+o*.7f));
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.14f,.1f,.12f)*s.w,Quaternion.Euler(0,m.Range(0,360),0),30,40,
                d => 1 + .42f*(Fbm(d*1.7f+o)-.5f) - .12f*pit(d),
                (d,p) => Color.Lerp(Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*20+o)),m.piece.accent,Mathf.Clamp01(pit(d))*.75f));
        }
    }

    // Đá nham thạch đỏ: a cluster of rough red-black lumps full of tiny pores.
    private static void Lava(Mesher m)
    {
        for (int n = 0; n < 4; n++)
        {
            float a = n*2.39996f, r = n==0 ? 0 : .1f, k = n==0 ? 1 : .62f + .1f*m.Next();
            Vector3 o = Offset(m);
            Func<Vector3,float> pore = d => Smooth(.62f,.74f,Noise(d*24+o));
            m.Blob(new Vector3(Mathf.Cos(a)*r,.055f*k,Mathf.Sin(a)*r*.8f),new Vector3(.11f,.085f,.1f)*k,Quaternion.Euler(m.Range(-15,15),m.Range(0,360),0),
                n==0?28:22,n==0?36:30,
                d => 1 + .5f*(Fbm(d*1.9f+o)-.5f) - .08f*pore(d),
                (d,p) => Color.Lerp(Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*30+o)),m.piece.accent,pore(d)*.85f));
        }
    }

    // Đá cuội biển: a heap of flat tumbled pebbles in mixed colours.
    private static void SeaPebbles(Mesher m)
    {
        var palette = new[] { m.piece.colA, m.piece.colB, m.piece.accent, new Color(.56f,.57f,.58f), new Color(.62f,.42f,.33f) };
        for (int n = 0; n < 9; n++)
        {
            float a = n*2.39996f, r = .03f+.1f*Mathf.Sqrt(n/9f), size = m.Range(.035f,.06f);
            var radii = new Vector3(size,size*m.Range(.38f,.5f),size*m.Range(.65f,.85f));
            var centre = n == 8 ? new Vector3(.01f,.065f,0) : new Vector3(Mathf.Cos(a)*r,radii.y*.8f,Mathf.Sin(a)*r*.85f);
            Color tint = palette[n%palette.Length]; Vector3 o = Offset(m);
            m.Blob(centre,radii,Quaternion.Euler(n==8?8:m.Range(-6,6),m.Range(0,360),m.Range(-6,6)),14,20,
                d => 1 + .05f*(Noise(d*2.5f+o)-.5f),
                (d,p) => tint*(.88f+.18f*Noise(p*70+o)));
        }
    }

    // Đá cổ thạch / Đá tuyết sơn: faceted upright stones cut by planes, painted with veins.
    private static void Angular(Mesher m,bool peak)
    {
        var parts = peak
            ? new[] { new Vector4(0,.2f,0,1), new Vector4(.11f,.12f,.05f,.62f), new Vector4(-.1f,.1f,-.04f,.55f) }
            : new[] { new Vector4(0,.15f,0,1), new Vector4(.13f,.08f,.04f,.62f) };
        Vector3 axis = (peak ? new Vector3(1,.55f,.25f) : new Vector3(.25f,1,.15f)).normalized;
        foreach (var s in parts)
        {
            Vector3 o = Offset(m);
            var planes = new List<Vector4>();
            for (int c = 0; c < 9; c++)
            {
                var normal = new Vector3(m.Range(-1,1),peak ? m.Range(.1f,.9f) : m.Range(-.4f,.8f),m.Range(-1,1)).normalized;
                planes.Add(new Vector4(normal.x,normal.y,normal.z,m.Range(.58f,.82f)));
            }
            Vector3 radii = (peak ? new Vector3(.12f,.27f,.1f) : new Vector3(.13f,.2f,.1f))*s.w;
            m.Blob(new Vector3(s.x,s.y,s.z)*(peak ? 1.1f : 1),radii,Quaternion.Euler(0,m.Range(0,360),m.Range(-8,8)),30,40,
                d =>
                {
                    float r = 1 + .22f*(Fbm(d*1.5f+o)-.5f);
                    foreach (var pl in planes)
                    {
                        float facing = d.x*pl.x + d.y*pl.y + d.z*pl.z;
                        if (facing > .01f) r = Mathf.Min(r,pl.w/facing);
                    }
                    return r - .02f*Crease(Noise(d*6+o),6);
                },
                (d,p) =>
                {
                    float warp = Fbm(p*7+o);
                    float vein = Smooth(peak ? .7f : .78f,.97f,Mathf.Sin(Vector3.Dot(p,axis)*(peak ? 70 : 55) + warp*7));
                    Color body = Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*18+o));
                    if (!peak) body = Color.Lerp(body,new Color(.82f,.64f,.3f),.6f*Smooth(.55f,.8f,Mathf.Sin(Vector3.Dot(p,axis)*18+warp*4)));
                    else vein = Mathf.Max(vein,Smooth(.62f,.9f,d.y)*Smooth(.45f,.65f,Noise(p*30+o)));
                    return Color.Lerp(body,m.piece.accent,vein);
                });
        }
    }

    // San hô vụn: bleached branch fragments heaped on the sand, some with a side nub.
    private static void CoralRubble(Mesher m)
    {
        Func<Vector3,float,Color> paint = (p,s) =>
            Color.Lerp(Color.Lerp(m.piece.colB,m.piece.colA,Noise(p*90)),m.piece.accent,Smooth(.7f,.82f,Noise(p*170+Vector3.one*9)));
        for (int n = 0; n < 24; n++)
        {
            float a = n*2.39996f, r = .11f*Mathf.Sqrt((n+.5f)/24), radius = m.Range(.007f,.012f), length = m.Range(.045f,.09f);
            float yaw = m.Range(0,Mathf.PI*2);
            var dir = new Vector3(Mathf.Cos(yaw),m.Range(-.05f,.12f),Mathf.Sin(yaw)).normalized;
            var start = new Vector3(Mathf.Cos(a)*r,radius + (n>=16 ? m.Range(.008f,.016f) : 0),Mathf.Sin(a)*r) - dir*length*.5f;
            var bend = new Vector3(m.Range(-1,1),.2f,m.Range(-1,1))*.012f;
            var points = Path(start,dir*length,bend,5);
            m.Tube(points,Radii(5,radius,radius*.75f),7,paint,true);
            if (n%2 == 0)
            {
                var root = points[2];
                var side = Vector3.Cross(dir,Vector3.up).normalized*(n%4==0 ? 1 : -1) + Vector3.up*.6f;
                m.Tube(Path(root,side.normalized*length*.35f,Vector3.zero,3),Radii(3,radius*.7f,radius*.55f),6,paint,false);
            }
        }
    }

    // San hô khối: a dome of real brain coral with meandering ridges and valleys.
    private static void CoralBlock(Mesher m)
    {
        Vector3 o = Offset(m);
        Func<Vector3,float> valley = d => Smooth(.1f,.85f,-Mathf.Sin(Noise(d*3.2f+o)*42));
        m.Blob(new Vector3(0,.05f,0),new Vector3(.15f,.11f,.13f),Quaternion.Euler(0,m.Range(0,360),0),52,72,
            d => 1 + .12f*(Fbm(d*1.3f+o)-.5f) - .06f*valley(d),
            (d,p) => Color.Lerp(Color.Lerp(m.piece.colB,m.piece.colA,.4f+.6f*Noise(p*60)),m.piece.accent,valley(d)*.7f));
    }

    // Live rock base shared by the living corals: lumpy, coralline-tinted.
    private static void LiveRock(Mesher m,Vector3 radii)
    {
        Vector3 o = Offset(m);
        m.Blob(new Vector3(0,radii.y*.6f,0),radii,Quaternion.Euler(0,m.Range(0,360),0),24,32,
            d => 1 + .35f*(Fbm(d*1.8f+o)-.5f),
            (d,p) => Color.Lerp(new Color(.36f,.32f,.3f),new Color(.62f,.42f,.52f),Smooth(.45f,.7f,Fbm(p*14+o))));
    }

    // San hô nấm: five domed discs with radial stripes on short stalks over live rock.
    private static void Mushroom(Mesher m)
    {
        var rock = new Vector3(.12f,.065f,.1f);
        LiveRock(m,rock);
        for (int n = 0; n < 5; n++)
        {
            float a = n*2.39996f + .4f, r = n==0 ? 0 : .055f;
            float f = r/rock.x, surface = rock.y*.6f + rock.y*Mathf.Sqrt(1-f*f)*.95f;
            var foot = new Vector3(Mathf.Cos(a)*r,surface,Mathf.Sin(a)*r*.85f);
            var up = (Vector3.up + new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(n==0 ? 0 : .55f)).normalized;
            float size = m.Range(.04f,.052f), stalk = m.Range(.018f,.03f);
            var top = foot + up*stalk;
            m.Tube(Path(foot - up*.02f,up*(stalk+.02f),Vector3.zero,3),Radii(3,.01f,.008f),8,(p,s) => m.piece.colB*.55f,false);
            Color centre = n%2==0 ? m.piece.colA : m.piece.accent, rim = n%2==0 ? m.piece.colB : m.piece.colA;
            m.Blob(top,new Vector3(size,size*.22f,size),Quaternion.FromToRotation(Vector3.up,up)*Quaternion.Euler(0,m.Range(0,360),0),10,28,
                d => 1 + .07f*Mathf.Sin(Mathf.Atan2(d.z,d.x)*7)*(1-Mathf.Abs(d.y)),
                (d,p) =>
                {
                    float radial = Mathf.Sqrt(d.x*d.x+d.z*d.z), stripe = .5f+.5f*Mathf.Sin(Mathf.Atan2(d.z,d.x)*26);
                    if (d.y < 0) return rim*.5f;
                    Color c = Color.Lerp(centre,rim,Smooth(.25f,.95f,radial))*(.85f+.25f*stripe);
                    return radial < .12f ? c*.45f : c;
                });
        }
    }

    // San hô đồng tiền: a mat of short polyp columns, each crowned with a coin-like disc.
    private static void Zoanthid(Mesher m)
    {
        var rock = new Vector3(.13f,.05f,.11f);
        LiveRock(m,rock);
        for (int n = 0; n < 20; n++)
        {
            float a = n*2.39996f, f = Mathf.Sqrt((n+.5f)/20)*.85f;
            float x = Mathf.Cos(a)*f*rock.x, z = Mathf.Sin(a)*f*rock.z;
            float surface = rock.y*.6f + rock.y*Mathf.Sqrt(Mathf.Max(0,1-f*f))*.85f;
            var up = (Vector3.up + new Vector3(x,0,z)*3.5f + new Vector3(m.Range(-.2f,.2f),0,m.Range(-.2f,.2f))).normalized;
            float height = m.Range(.014f,.03f), size = m.Range(.011f,.015f);
            var foot = new Vector3(x,surface - .01f,z);
            var top = foot + up*(height+.01f);
            bool second = n%3 == 0;
            Color body = second ? m.piece.accent : m.piece.colA, rim = second ? new Color(1,.9f,.2f) : m.piece.colB;
            m.Tube(Path(foot,up*(height+.01f),Vector3.zero,3),Radii(3,size*.6f,size*.55f),8,(p,s) => Color.Lerp(new Color(.45f,.36f,.28f),body*.7f,s),false);
            m.Blob(top,new Vector3(size,size*.28f,size),Quaternion.FromToRotation(Vector3.up,up),6,16,
                d => 1 + .12f*Mathf.Sin(Mathf.Atan2(d.z,d.x)*14)*(1-Mathf.Abs(d.y)),
                (d,p) =>
                {
                    float radial = Mathf.Sqrt(d.x*d.x+d.z*d.z);
                    if (d.y < 0) return body*.6f;
                    return radial < .22f ? body*.3f : radial < .62f ? body : rim;
                });
        }
    }

    // San hô nhân tạo nhánh: three staghorn trunks forking twice, with pale rounded tips.
    private static void PlasticBranch(Mesher m)
    {
        m.Blob(new Vector3(0,.008f,0),new Vector3(.07f,.018f,.06f),Quaternion.identity,10,24,d => 1,(d,p) => m.piece.colB*.8f);
        for (int t = 0; t < 3; t++)
        {
            float a = t*2.1f + .3f;
            var dir = (Vector3.up*1.6f + new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))).normalized;
            Branch(m,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.02f,dir,0,.014f,.15f,false);
        }
    }

    // San hô nhân tạo quạt: a flat fan of fine branches spread in one plane.
    private static void PlasticFan(Mesher m)
    {
        m.Blob(new Vector3(0,.008f,0),new Vector3(.035f,.016f,.03f),Quaternion.identity,8,18,d => 1,(d,p) => m.piece.accent);
        Branch(m,Vector3.zero,Vector3.up,0,.009f,.16f,true);
    }

    private static void Branch(Mesher m,Vector3 start,Vector3 dir,int depth,float radius,float length,bool fan)
    {
        int maxDepth = fan ? 3 : 2;
        var bend = fan ? new Vector3(m.Range(-.01f,.01f),0,m.Range(-.006f,.006f)) : new Vector3(m.Range(-1,1),.6f,m.Range(-1,1))*length*.15f;
        var points = Path(start,dir*length,bend,fan ? 5 : 6);
        float tip = radius*(depth==maxDepth ? (fan ? .5f : .75f) : .8f);
        Piece piece = m.piece;
        Func<Vector3,float,Color> paint = fan
            ? (Func<Vector3,float,Color>)((p,s) => Color.Lerp(piece.colA,piece.colB,Smooth(.05f,.32f,p.y)))
            : (p,s) => Color.Lerp(Color.Lerp(piece.colB,piece.colA,(depth+s)/(maxDepth+1)),piece.accent,depth==maxDepth ? Smooth(.75f,1,s) : 0);
        m.Tube(points,Radii(points.Count,radius,tip),fan ? 5 : 9,paint,depth==0);
        if (depth >= maxDepth) return;
        int kids = fan ? (depth==0 ? 5 : 3) : (depth==0 ? 3 : 2);
        for (int c = 0; c < kids; c++)
        {
            float t = .35f + .55f*(c+.5f)/kids;
            int i = Mathf.Clamp(Mathf.RoundToInt(t*(points.Count-1)),1,points.Count-1);
            Vector3 along = (points[i]-points[i-1]).normalized, child;
            if (fan) child = Quaternion.AngleAxis((c%2==0 ? 1 : -1)*m.Range(28,52),Vector3.forward)*along + Vector3.forward*m.Range(-.08f,.08f);
            else
            {
                float phi = c*2.39996f + m.Range(0,1);
                child = Quaternion.AngleAxis(m.Range(25,42),Vector3.Cross(along,new Vector3(Mathf.Cos(phi),0,Mathf.Sin(phi))).normalized)*along;
                child.y = Mathf.Max(child.y,.25f);
            }
            Branch(m,points[i],child.normalized,depth+1,Mathf.Max(.0022f,radius*.72f),length*(fan ? .62f : .68f)*(1-t*.3f),fan);
        }
    }

    private static List<Vector3> Path(Vector3 start,Vector3 span,Vector3 bend,int count)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < count; i++) { float s = i/(float)(count-1); points.Add(start + span*s + bend*Mathf.Sin(s*Mathf.PI)); }
        return points;
    }

    private static List<float> Radii(int count,float r0,float r1)
    {
        var radii = new List<float>();
        for (int i = 0; i < count; i++) radii.Add(Mathf.Lerp(r0,r1,i/(float)(count-1)));
        return radii;
    }

    private sealed class Mesher
    {
        public readonly Piece piece;
        private readonly System.Random random;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public Mesher(Piece p,int seed)
        {
            piece = p; int hash = seed*7919;
            unchecked { foreach (char ch in p.id) hash = hash*31 + ch; }
            random = new System.Random(hash);
        }

        public float Next() => (float)random.NextDouble();
        public float Range(float a,float b) => a + (b-a)*Next();

        // A welded sphere: shape(direction) scales each radius, paint(direction, position) colours it.
        // Whatever dips below the sand is squashed into a flat resting base.
        public void Blob(Vector3 centre,Vector3 radii,Quaternion rotation,int rings,int segments,Func<Vector3,float> shape,Func<Vector3,Vector3,Color> paint)
        {
            int top = vertices.Count;
            Add(Vector3.up,centre,radii,rotation,shape,paint);
            for (int r = 1; r < rings; r++)
            {
                float phi = r*Mathf.PI/rings;
                for (int s = 0; s < segments; s++)
                {
                    float theta = s*Mathf.PI*2/segments;
                    Add(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta)),centre,radii,rotation,shape,paint);
                }
            }
            int bottom = vertices.Count;
            Add(Vector3.down,centre,radii,rotation,shape,paint);
            Func<int,int,int> at = (r,s) => top + 1 + (r-1)*segments + (s%segments);
            for (int s = 0; s < segments; s++)
            {
                triangles.AddRange(new[]{ top, at(1,s+1), at(1,s) });
                for (int r = 1; r < rings-1; r++)
                    triangles.AddRange(new[]{ at(r,s), at(r,s+1), at(r+1,s), at(r,s+1), at(r+1,s+1), at(r+1,s) });
                triangles.AddRange(new[]{ at(rings-1,s), at(rings-1,s+1), bottom });
            }
        }

        private void Add(Vector3 d,Vector3 centre,Vector3 radii,Quaternion rotation,Func<Vector3,float> shape,Func<Vector3,Vector3,Color> paint)
        {
            Vector3 p = centre + rotation*Vector3.Scale(d*shape(d),radii);
            if (p.y < 0) p.y *= .15f;
            vertices.Add(p); colors.Add(paint(d,p));
        }

        // A welded tube along points with parallel-transported rings, a rounded tip and an optional base cap.
        public void Tube(List<Vector3> points,List<float> radii,int sides,Func<Vector3,float,Color> paint,bool baseCap)
        {
            points = new List<Vector3>(points); radii = new List<float>(radii);
            int count = points.Count;
            Vector3 endDir = (points[count-1]-points[count-2]).normalized, end = points[count-1]; float rEnd = radii[count-1];
            for (int k = 1; k <= 3; k++)
            {
                float f = k/3f*Mathf.PI/2;
                points.Add(end + endDir*(rEnd*Mathf.Sin(f))); radii.Add(rEnd*Mathf.Cos(f));
            }
            int n = points.Count - 1, start = vertices.Count;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = (points[Mathf.Min(n,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
                normal = i == 0 ? Vector3.Cross(tangent,Mathf.Abs(tangent.y)<.9f?Vector3.up:Vector3.right).normalized
                    : (normal - tangent*Vector3.Dot(normal,tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent,normal);
                float s = Mathf.Min(1,i/(float)(count-1));
                for (int j = 0; j < sides; j++)
                {
                    float a = j*Mathf.PI*2/sides;
                    Vector3 p = points[i] + (normal*Mathf.Cos(a) + binormal*Mathf.Sin(a))*radii[i];
                    vertices.Add(p); colors.Add(paint(p,s));
                }
            }
            int apex = vertices.Count;
            vertices.Add(points[n]); colors.Add(paint(points[n],1));
            for (int i = 0; i < n; i++) for (int j = 0; j < sides; j++)
            {
                int v = start + i*sides + j, next = start + i*sides + (j+1)%sides;
                if (i < n-1) triangles.AddRange(new[]{ v, next, v+sides, next, next+sides, v+sides });
                else triangles.AddRange(new[]{ v, next, apex });
            }
            if (!baseCap) return;
            int centre = vertices.Count;
            vertices.Add(points[0]); colors.Add(paint(points[0],0));
            for (int j = 0; j < sides; j++) triangles.AddRange(new[]{ centre, start + (j+1)%sides, start + j });
        }

        public Mesh Finish(out float footprint)
        {
            var bounds = new Bounds(vertices[0],Vector3.zero);
            foreach (var v in vertices) bounds.Encapsulate(v);
            // Centre on the footprint and stand on the sand.
            var offset = new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            var uvs = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++) { vertices[i] += offset; uvs.Add(new Vector2(vertices[i].x + vertices[i].z,vertices[i].y)); }
            var mesh = new Mesh { indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            footprint = Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.5f,.12f,.32f);
            return mesh;
        }
    }
}
