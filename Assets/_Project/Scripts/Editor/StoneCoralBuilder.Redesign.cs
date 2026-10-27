using System;
using UnityEngine;

// Stones for the first rock set (once plain Perlin spheres under one flat tint), the cliff (once stretched
// elephant skin) and the deeper elephant skin. Same seeded blob as the named stones: planar cuts give crisp
// faces, creases give grooves and veins, vertex colours carry the stone's colour. Redesign holds the pieces
// that are not shop stones of their own in All; their ids are the catalog ids they build.
public static partial class StoneCoralBuilder
{
    public static readonly Piece[] Redesign =
    {
        new Piece { id="river-pebbles", name="Đá cuội suối", form=Form.RiverPebbles,
            colA=new Color(.72f,.7f,.64f), colB=new Color(.42f,.44f,.45f), accent=new Color(.6f,.5f,.38f) },
        new Piece { id="dragon-stone", name="Đá tai mèo", form=Form.DragonStone,
            colA=new Color(.62f,.45f,.27f), colB=new Color(.38f,.26f,.16f), accent=new Color(.14f,.09f,.06f) },
        new Piece { id="lava-rock", name="Đá nham thạch", form=Form.Lava,
            colA=new Color(.3f,.28f,.27f), colB=new Color(.13f,.12f,.12f), accent=new Color(.03f,.03f,.03f) },
        new Piece { id="seiryu", name="Đá vân Seiryu", form=Form.Seiryu,
            colA=new Color(.56f,.6f,.62f), colB=new Color(.3f,.33f,.35f), accent=new Color(.92f,.93f,.9f) },
        new Piece { id="rock-cluster", name="Cụm đá tự nhiên", form=Form.Cluster,
            colA=new Color(.64f,.62f,.56f), colB=new Color(.38f,.37f,.34f), accent=new Color(.36f,.46f,.26f) },
        new Piece { id="mini-landscape", name="Hòn non bộ", form=Form.Penjing,
            colA=new Color(.66f,.6f,.5f), colB=new Color(.36f,.33f,.3f), accent=new Color(.3f,.44f,.24f) },
        new Piece { id="stone-cliff", name="Đá vách núi", form=Form.Cliff,
            colA=new Color(.62f,.6f,.56f), colB=new Color(.34f,.33f,.32f), accent=new Color(.36f,.46f,.26f) },
    };

    private static void Redesigned(Mesher m)
    {
        switch (m.piece.form)
        {
            case Form.RiverPebbles: RiverPebbles(m); break;
            case Form.DragonStone: DragonStone(m); break;
            case Form.Seiryu: Seiryu(m); break;
            case Form.Cluster: Cluster(m); break;
            case Form.Penjing: Penjing(m); break;
            case Form.Cliff: Cliff(m); break;
            default: DeepElephantSkin(m); break;
        }
    }

    // Random cutting planes: the radius each direction may reach before a flat face stops it.
    private static Func<Vector3,float> Cuts(Mesher m,int count,float minY,float maxY,float near,float far)
    {
        var planes = new Vector4[count];
        for (int c = 0; c < count; c++)
        {
            var n = new Vector3(m.Range(-1,1),m.Range(minY,maxY),m.Range(-1,1)).normalized;
            planes[c] = new Vector4(n.x,n.y,n.z,m.Range(near,far));
        }
        return d =>
        {
            float r = 10;
            foreach (var pl in planes)
            {
                float facing = d.x*pl.x + d.y*pl.y + d.z*pl.z;
                if (facing > .01f) r = Mathf.Min(r,pl.w/facing);
            }
            return r;
        };
    }

    // Pushes a sphere towards a rounded box: higher powers give flatter faces and firmer edges.
    private static float Boxy(Vector3 d,float power) =>
        Mathf.Pow(Mathf.Pow(Mathf.Abs(d.x),power) + Mathf.Pow(Mathf.Abs(d.y),power) + Mathf.Pow(Mathf.Abs(d.z),power),-1/power);

    // Đá cuội suối: a loose group of smooth flat river pebbles, one resting on top, shaded darker underneath.
    private static void RiverPebbles(Mesher m)
    {
        var palette = new[] { m.piece.colA, m.piece.colB, m.piece.accent, new Color(.55f,.57f,.56f), new Color(.66f,.62f,.55f), new Color(.36f,.37f,.36f) };
        for (int n = 0; n < 7; n++)
        {
            bool top = n == 6;
            float a = n*2.39996f + .7f, r = top ? 0 : .05f + .075f*Mathf.Sqrt(n/6f), size = top ? .06f : m.Range(.042f,.068f);
            var radii = new Vector3(size,size*m.Range(.4f,.52f),size*m.Range(.66f,.84f));
            var centre = top ? new Vector3(.012f,radii.y*1.6f,.005f) : new Vector3(Mathf.Cos(a)*r,radii.y*.75f,Mathf.Sin(a)*r*.8f);
            Color tint = palette[n%palette.Length]; Vector3 o = Offset(m);
            m.Blob(centre,radii,Quaternion.Euler(top ? 10 : m.Range(-8,8),m.Range(0,360),m.Range(-8,8)),16,24,
                d => 1 + .06f*(Noise(d*2.2f+o)-.5f),
                (d,p) => tint*(.86f+.16f*Fbm(p*55+o))*Mathf.Lerp(.72f,1.04f,Smooth(-.7f,.5f,d.y)));
        }
    }

    // Đá tai mèo (Ohko): craggy ochre clay rock, sharp ridges and deep dark holes.
    private static void DragonStone(Mesher m)
    {
        var lumps = new[] { new Vector4(0,.11f,0,1), new Vector4(.13f,.07f,.05f,.66f), new Vector4(-.12f,.06f,-.04f,.58f) };
        foreach (var s in lumps)
        {
            Vector3 o = Offset(m), o2 = Offset(m); var cut = Cuts(m,6,-.3f,.9f,.7f,.9f);
            Func<Vector3,float> hole = d => Mathf.Clamp01(Smooth(.6f,.74f,Noise(d*5+o)) + .5f*Smooth(.66f,.78f,Noise(d*11+o2)));
            Func<Vector3,float> ridge = d => Crease(Noise(d*3.5f+o2),5);
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.15f,.14f,.12f)*s.w,Quaternion.Euler(m.Range(-12,12),m.Range(0,360),m.Range(-12,12)),36,48,
                d => Mathf.Min(1 + .32f*(Fbm(d*1.7f+o)-.5f) + .07f*ridge(d),cut(d)) - .2f*hole(d),
                (d,p) => Color.Lerp(Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*14+o))*(1+.22f*ridge(d)),m.piece.accent,hole(d)*.9f));
        }
    }

    // Đá vân Seiryu: faceted blue-grey stone with vertical grooves and bold white calcite veins.
    private static void Seiryu(Mesher m)
    {
        var parts = new[] { new Vector4(0,.16f,0,1), new Vector4(.12f,.08f,.06f,.58f) };
        foreach (var s in parts)
        {
            Vector3 o = Offset(m), o2 = Offset(m); var cut = Cuts(m,12,-.2f,.7f,.55f,.8f);
            Func<Vector3,float> groove = d => Crease(Noise(new Vector3(d.x*7,d.y*1.6f,d.z*7)+o2),7);
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.14f,.22f,.12f)*s.w,Quaternion.Euler(m.Range(-6,6),m.Range(0,360),m.Range(-10,10)),56,72,
                d => Mathf.Min(1 + .2f*(Fbm(d*1.5f+o)-.5f),cut(d)) - .05f*groove(d),
                (d,p) =>
                {
                    Color body = Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*16+o))*(1-.4f*groove(d));
                    // Veins run diagonally across the stone, wandering a little; a finer second set crosses them.
                    float main = Crease(Noise(new Vector3(p.x*9+p.y*6,p.y*3,p.z*9)+o),5);
                    float fine = Crease(Noise(new Vector3(p.x*22,p.y*22-p.x*14,p.z*22)+o2),9);
                    return Color.Lerp(body,m.piece.accent,Mathf.Max(Smooth(.4f,.7f,main),.8f*Smooth(.5f,.8f,fine)));
                });
        }
    }

    // Cụm đá tự nhiên: an Iwagumi trio, one upright main stone and two smaller companions, moss in the high spots.
    private static void Cluster(Mesher m)
    {
        var stones = new[] { new Vector4(0,.14f,0,1), new Vector4(.15f,.07f,.06f,.6f), new Vector4(-.13f,.05f,.05f,.45f) };
        foreach (var s in stones)
        {
            Vector3 o = Offset(m); var cut = Cuts(m,8,-.2f,.8f,.6f,.85f);
            float lean = s.w < 1 ? m.Range(-14,14) : m.Range(-6,6);
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.12f,.2f,.11f)*s.w,Quaternion.Euler(m.Range(-4,4),m.Range(0,360),lean),30,40,
                d => Mathf.Min(1 + .24f*(Fbm(d*1.6f+o)-.5f),cut(d)) - .025f*Crease(Noise(d*6+o),6),
                (d,p) =>
                {
                    Color body = Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*18+o));
                    float moss = Smooth(.55f,.85f,d.y)*Smooth(.45f,.6f,Noise(p*22+o));
                    return Color.Lerp(body,m.piece.accent,moss*.8f);
                });
        }
    }

    // Hòn non bộ: a range of ridged peaks on a low base, banded strata and moss on the summits.
    private static void Penjing(Mesher m)
    {
        Vector3 ob = Offset(m);
        Func<Vector3,Vector3,Color> rock = (d,p) =>
        {
            float band = .5f + .5f*Mathf.Sin(p.y*120 + Fbm(p*6+ob)*6);
            Color body = Color.Lerp(m.piece.colB,m.piece.colA,.35f + .5f*Fbm(p*14+ob))*(.88f + .14f*band);
            float moss = Smooth(.6f,.9f,d.y)*Smooth(.4f,.6f,Noise(p*25+ob));
            return Color.Lerp(body,m.piece.accent,moss*.85f);
        };
        m.Blob(new Vector3(0,.02f,0),new Vector3(.26f,.045f,.14f),Quaternion.Euler(0,m.Range(-10,10),0),18,40,
            d => 1 + .2f*(Fbm(d*2+ob)-.5f),rock);
        // x, z, height, half width
        var peaks = new[] { new Vector4(0,0,.3f,.1f), new Vector4(.1f,.03f,.21f,.08f), new Vector4(-.11f,.02f,.17f,.085f),
            new Vector4(.19f,-.02f,.12f,.065f), new Vector4(-.2f,-.03f,.1f,.07f), new Vector4(.04f,-.06f,.13f,.06f) };
        foreach (var k in peaks)
        {
            Vector3 o = Offset(m); var cut = Cuts(m,8,.15f,.9f,.6f,.85f);
            m.Blob(new Vector3(k.x,k.z*.62f,k.y),new Vector3(k.w*.82f,k.z*.62f,k.w*.7f),Quaternion.Euler(m.Range(-5,5),m.Range(0,360),m.Range(-6,6)),30,36,
                d => Mathf.Min(1 + .22f*(Fbm(d*1.8f+o)-.5f),cut(d)) - .06f*Crease(Noise(new Vector3(d.x*6,d.y*1.3f,d.z*6)+o),6),
                rock);
        }
    }

    // Đá vách núi: a wall of stacked flat ledges, each layer set back a little, moss on the ledges.
    private static void Cliff(Mesher m)
    {
        // row heights from the sand; each row holds a few boxy slabs that shrink and step back as they rise
        int rows = 8;
        for (int row = 0; row < rows; row++)
        {
            float rise = row/(float)(rows-1), y = .028f + row*.052f, span = Mathf.Lerp(.32f,.2f,rise);
            int count = row < 4 ? 3 : 2;
            for (int c = 0; c < count; c++)
            {
                float x = (c-(count-1)*.5f)*span/count*1.9f + m.Range(-.015f,.015f) + (row%2==0 ? .012f : -.012f);
                float z = rise*.02f + m.Range(-.012f,.012f);
                var radii = new Vector3(span/count*m.Range(1.25f,1.45f),m.Range(.03f,.036f),m.Range(.05f,.06f)*(1-.25f*rise));
                Vector3 o = Offset(m); var cut = Cuts(m,6,-.15f,.25f,.75f,.95f);
                m.Blob(new Vector3(x,y,z),radii,Quaternion.Euler(m.Range(-3,3),m.Range(-7,7),m.Range(-5,5)),24,40,
                    d => Mathf.Min(Boxy(d,5)*(1 + .12f*(Fbm(d*1.8f+o)-.5f)),cut(d)) - .04f*Crease(Noise(new Vector3(d.x*6,d.y*2,d.z*6)+o),6),
                    (d,p) =>
                    {
                        Color body = Color.Lerp(m.piece.colB,m.piece.colA,Fbm(p*12+o))*(d.y < -.5f ? .6f : 1);
                        float moss = Smooth(.6f,.85f,d.y)*Smooth(.42f,.58f,Noise(p*24+o));
                        return Color.Lerp(body,m.piece.accent,moss*.85f);
                    });
            }
        }
    }

    // Đá da voi, deeper: flatter slabs with clearly cut wrinkles and darker creases.
    private static void DeepElephantSkin(Mesher m)
    {
        var slabs = new[] { new Vector4(0,.1f,0,1), new Vector4(.15f,.06f,.07f,.6f), new Vector4(-.09f,.2f,-.02f,.62f) };
        foreach (var s in slabs)
        {
            Vector3 o = Offset(m), o2 = Offset(m); float k = s.w;
            Func<Vector3,float> wrinkle = d => Crease(Noise(d*7+o),8)*.65f + Crease(Noise(d*14+o2),10)*.35f;
            m.Blob(new Vector3(s.x,s.y,s.z),new Vector3(.17f,.11f,.13f)*k,Quaternion.Euler(m.Range(-14,14),m.Range(0,360),m.Range(-18,18)),
                k>.9f?48:36,k>.9f?64:48,
                d => Boxy(d,2.6f)*(1 + .2f*(Fbm(d*1.6f+o)-.5f)) - .14f*wrinkle(d),
                (d,p) => Color.Lerp(m.piece.colB,m.piece.colA,.3f+.6f*Fbm(p*24+o))*(1-.6f*wrinkle(d)));
        }
    }
}
