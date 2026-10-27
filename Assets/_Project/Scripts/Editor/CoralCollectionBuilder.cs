using System;
using System.Collections.Generic;
using UnityEngine;

// Baked geometry only: no runtime generation, textures or extra draw calls per polyp.
public static class CoralCollectionBuilder
{
    public sealed class Species
    {
        public string id, name, description;
        public int kind, price;
        public Species(string id,string name,int kind,int price,string description)
        { this.id=id;this.name=name;this.kind=kind;this.price=price;this.description=description; }
    }
    // Released IDs/kinds are intentionally retained. New species follow ALL previous large pieces.
    public static readonly Species[] All =
    {
        new Species("coral-artificial-branch","San hô sừng hươu hồng",50,120,"Tán nhánh hồng đào dày, đầu kem · Đế đá sống tự nhiên"),
        new Species("coral-artificial-fan","Quạt biển đỏ",51,130,"Quạt đỏ mảnh với mạng nhánh đan như ren · Điểm nhấn hậu cảnh"),
        new Species("coral-plating","San hô đĩa tầng xanh ngọc",89,280,"Các tầng đĩa xanh ngọc uốn lượn, viền kem · Tiểu cảnh trung tầng"),
        new Species("coral-purple-branch","San hô nhánh tím",90,260,"Tán nhánh tím oải hương dày, ngọn xanh tím · Đế đá tự nhiên"),
        new Species("coral-torch","San hô đuốc vàng",91,300,"Ba cụm tua vàng mềm, đầu ngà sáng · Tiểu cảnh trung tầng"),
        new Species("coral-mushroom","San hô nấm xanh viền tím",48,240,"Năm đĩa thịt xanh ngọc xếp cụm, gân tỏa và viền tím gợn sóng"),
        new Species("coral-zoanthid","San hô đồng tiền đa sắc",49,220,"Thảm polyp tâm cam, vòng xanh và tua tím · Phủ đá tiền cảnh"),
        new Species("coral-block","San hô não xanh vàng",47,190,"Khối xanh ngọc với gân nổi uốn lượn màu vàng · Điểm nhấn tiền cảnh"),
        new Species("coral-blue-tube","San hô ống xanh",92,270,"Cụm ống xanh cao thấp, miệng rỗng viền tím · Đế đá nhỏ"),
        new Species("coral-lobophyllia","San hô thịt hồng cam",93,290,"Các múi thịt hồng cam gấp nếp, viền xanh nhạt · Tiểu cảnh tiền cảnh")
    };
    public static Species Find(string id) => Array.Find(All,s=>s.id==id);
    private static Color C(float r,float g,float b) => new Color(r,g,b,1);
    private static readonly Color Peach=C(.98f,.38f,.38f), Cream=C(1,.87f,.66f), Purple=C(.5f,.19f,.77f), Teal=C(.06f,.65f,.5f);
    private static Vector3 Rad(float a) => new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
    private static float Wave(Vector3 p) => Mathf.Sin(p.x*97+p.z*23)*Mathf.Sin(p.y*83-p.z*67);

    public static Mesh Build(Species species,int seed,out float footprint)
    {
        if(species==null)throw new ArgumentNullException(nameof(species));
        var m=new Geometry(seed);
        Rock(m);
        switch(Array.IndexOf(All,species))
        {
            case 0: Branching(m,false);break;
            case 1: Fan(m);break;
            case 2: Plates(m);break;
            case 3: Branching(m,true);break;
            case 4: Torch(m);break;
            case 5: Mushrooms(m);break;
            case 6: Zoanthids(m);break;
            case 7: Brain(m);break;
            case 8: BlueTubes(m);break;
            case 9: Lobes(m);break;
            default:throw new ArgumentException("Unknown coral species",nameof(species));
        }
        var mesh=m.Finish(species.id);
        footprint=Mathf.Clamp(Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.z)*.5f,.12f,.32f);
        return mesh;
    }

    private static void Rock(Geometry m)
    {
        for(int n=0;n<5;n++)
        {
            float a=n*2.4f;var center=Rad(a)*(n==0?0:.046f);center.y=.023f;
            m.Ellipsoid(center,new Vector3(n==0?.078f:.045f,.033f,.048f),10,18,
                d=>1+.13f*Mathf.Sin(d.x*9+d.z*13)*Mathf.Cos(d.y*11),
                p=> {float f=.5f+.5f*Wave(p*1.3f);var c=Color.Lerp(C(.18f,.23f,.19f),C(.48f,.38f,.4f),f);c.a=0;return c;});
        }
        // Coralline nodules break up the rock silhouette and hide stem intersections.
        for(int n=0;n<18;n++)
        {
            float a=n*2.4f;var p=Rad(a)*m.Range(.035f,.085f)+Vector3.up*m.Range(.018f,.046f);
            m.Ellipsoid(p,Vector3.one*m.Range(.004f,.009f),4,7,d=>1,
                x=> {var c=n%3==0?C(.44f,.22f,.4f):C(.24f,.32f,.18f);c.a=0;return c;});
        }
    }

    private static void Branching(Geometry m,bool violet)
    {
        for(int n=0;n<7;n++)
        {
            float a=n*2.39996f;
            var start=Rad(a)*.031f+Vector3.up*.039f;
            var dir=(Vector3.up+Rad(a)*m.Range(.23f,.6f)).normalized;
            Grow(m,start,dir,m.Range(.105f,.16f)*(violet?.83f:1),.014f,0,violet,a);
        }
    }
    private static void Grow(Geometry m,Vector3 start,Vector3 dir,float length,float radius,int depth,bool violet,float phase)
    {
        var bend=Rad(phase+.8f)*length*.12f;
        Vector3 Tip(float t)=>start+dir*length*t+bend*Mathf.Sin(t*Mathf.PI);
        Color body=violet?Purple:Peach, tip=violet?C(.57f,.64f,1):Cream;
        m.Tube(Tip,t=>radius*Mathf.Lerp(1,depth==2?.72f:.6f,t)*(1+.04f*Mathf.Sin(t*18+phase)),5,6,
            (p,t)=>Color.Lerp(body*.78f,Color.Lerp(body,tip,Mathf.SmoothStep(.05f,1,Mathf.Pow(t,4))),.4f+.6f*t));
        if(depth<2)
            for(int k=0;k<(depth==0?4:3);k++)
            {
                float t=.36f+k*(depth==0?.17f:.25f),a=phase+k*2.4f;
                var child=(dir*.75f+Rad(a)*(violet?.55f:.85f)+Vector3.up*.35f).normalized;
                Grow(m,Tip(t),child,length*m.Range(.58f,.74f)*(violet?.88f:1),radius*(violet?.69f:.63f),depth+1,violet,a);
            }
        // Small corallites are geometry along the branch, rather than painted dots.
        for(int k=0;k<3;k++)
        {
            float t=.15f+.8f*k/3f;var axis=Rad(phase+k*2.4f);
            var p=Tip(t)+axis*radius*.65f;
            m.Ellipsoid(p,Vector3.one*radius*.3f,3,5,d=>1,x=>Color.Lerp(body,tip,.25f+.35f*t));
        }
    }

    private static void Fan(Geometry m)
    {
        var red=C(.89f,.12f,.19f);var light=C(1,.36f,.25f);
        m.Tube(t=>new Vector3(.012f*Mathf.Sin(t*3),.035f+t*.11f,0),t=>Mathf.Lerp(.012f,.004f,t),8,8,(p,t)=>Color.Lerp(red*.55f,red,t));
        // Polar lattice: 17 curved radial ribs linked by staggered tangential bridges.
        Vector3 Point(float t,float a)=>new Vector3(Mathf.Sin(a)*(.035f+.235f*t)+.005f*Mathf.Sin(t*35+a*17)*t,.09f+Mathf.Cos(a)*(.035f+.34f*t)+.004f*Mathf.Sin(t*31+a*23)*t,.012f*Mathf.Sin(a*4+t*7));
        for(int ray=0;ray<25;ray++)
        {
            float a=Mathf.Lerp(-1.22f,1.22f,ray/24f);
            m.Tube(t=>Point(t,a),t=>Mathf.Lerp(.0034f,.0011f,t),14,5,(p,t)=>Color.Lerp(red,light,t*.6f));
            for(int rung=1;rung<=14;rung++)
            {
                if(ray==24)continue;
                float t=rung/15f,aa=a+2.44f/24;
                Vector3 p=Point(t,a),q=Point(Mathf.Clamp01(t+(ray%2==0?.028f:-.028f)),aa);
                m.Tube(s=>Vector3.Lerp(p,q,s)+Vector3.up*(.003f*Mathf.Sin(s*Mathf.PI)),s=>.00115f,3,4,(x,s)=>Color.Lerp(red,light,t*.5f));
            }
        }
    }

    private static void Plates(Geometry m)
    {
        for(int level=0;level<5;level++)
        {
            int l=level;float size=.151f-level*.017f;float phase=l*1.7f;
            var center=new Vector3(Mathf.Sin(phase)*.022f,.065f+level*.035f,Mathf.Cos(phase)*.012f);
            // Closed thin ruffled cup, with a softly folded cream growth edge.
            m.Surface(14,80,(r,a)=>
            {
                float theta=a*Mathf.PI*2;
                float radius=size*r*(1+.09f*Mathf.Sin(theta*7+phase));
                float height=.036f*r*r+.014f*Mathf.Sin(theta*6+phase)*Mathf.Pow(r,3);
                return center+Rad(theta)*radius+Vector3.up*height;
            },(r,a,p)=>Color.Lerp(C(.035f,.37f,.29f),C(.18f,.75f,.56f),r)*(.88f+.12f*Mathf.Sin(a*360+r*28)),false);
            m.Tube(t=> {float a=t*Mathf.PI*2;return center+Rad(a)*size*(1+.09f*Mathf.Sin(a*7+phase))+Vector3.up*(.036f+.014f*Mathf.Sin(a*6+phase));},t=>.0023f,80,5,(p,t)=>Cream);
        }
    }

    private static void Torch(Geometry m)
    {
        for(int head=0;head<3;head++)
        {
            float ha=head*2.4f;var center=Rad(ha)*.051f+Vector3.up*(.075f+(head%2)*.025f);
            m.Tube(t=>Vector3.Lerp(new Vector3(0,.03f,0),center,t),t=>Mathf.Lerp(.016f,.023f,t),7,8,(p,t)=>C(.34f,.18f,.19f));
            for(int n=0;n<47;n++)
            {
                float a=n*2.4f+head,r=Mathf.Sqrt((n+.5f)/47);var radial=Rad(a);
                Vector3 start=center+radial*r*.023f;
                float height=.125f*(1-.38f*r)+m.Range(0,.025f);
                Vector3 At(float t)=>start+Vector3.up*(height*t)+radial*(.068f*r*t*t)+Rad(a+1.4f)*(.012f*Mathf.Sin(t*3));
                m.Tube(At,t=>Mathf.Lerp(.0032f,.0019f,t),7,5,(p,t)=>Color.Lerp(C(.62f,.24f,.025f),C(1,.69f,.12f),t));
                m.Ellipsoid(At(1),new Vector3(.0036f,.0047f,.0036f),5,7,d=>1,p=>Cream);
            }
        }
    }

    private static void Mushrooms(Geometry m)
    {
        for(int n=0;n<5;n++)
        {
            float a=n*2.4f,r=n==0?0:.073f,size=n==0?.064f:m.Range(.047f,.06f);
            var center=Rad(a)*r+Vector3.up*(n==0?.107f:.073f);
            var rotation=Quaternion.Euler(-23+Mathf.Cos(a)*12,0,Mathf.Sin(a)*17);
            m.Surface(18,72,(u,v)=>
            {
                float theta=v*Mathf.PI*2;float rr=size*u*(1+.065f*Mathf.Sin(theta*9));
                float h=.016f*Mathf.Sin(u*Mathf.PI)-.007f*Mathf.Exp(-u*u*80)+.003f*Mathf.Sin(theta*54)*u+.005f*Mathf.Sin(theta*9)*Mathf.Pow(u,4);
                return center+rotation*(Rad(theta)*rr+Vector3.up*h);
            },(u,v,p)=>
            {
                var c=Color.Lerp(Teal,C(.4f,.14f,.56f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.98f,u)));
                c*=.78f+.22f*Mathf.Pow(.5f+.5f*Mathf.Sin(v*Mathf.PI*2*54),3);
                return u<.08f?Cream*.65f:c;
            },false);
        }
    }

    private static void Zoanthids(Geometry m)
    {
        for(int n=0;n<25;n++)
        {
            float a=n*2.4f,r=Mathf.Sqrt((n+.5f)/25);float size=m.Range(.016f,.022f);
            var center=Rad(a)*r*.105f+Vector3.up*(.045f+.051f*Mathf.Sqrt(1-r*r));
            m.Tube(t=>center-Vector3.up*.025f+Vector3.up*t*.01f,t=>size*.65f,3,7,(p,t)=>C(.25f,.11f,.29f));
            var rotation=Quaternion.Euler(-25+Mathf.Cos(a)*10,0,Mathf.Sin(a)*14);
            m.Surface(6,24,(u,v)=>center+rotation*(Rad(v*Mathf.PI*2)*(size*u)+Vector3.up*(.005f*Mathf.Sin(u*Mathf.PI))),
                (u,v,p)=>u<.16f?C(.23f,.07f,.1f):u<.64f?C(1,.4f,.04f):u<.9f?C(.3f,.85f,.3f):Purple,false);
            for(int k=0;k<14;k++)
            {
                var radial=Rad(k*Mathf.PI*2/14);var start=center+rotation*(radial*size*.96f);
                m.Tube(t=>start+rotation*(radial*(size*.28f*t)+Vector3.up*(size*.4f*Mathf.Sin(t*Mathf.PI*.8f))),t=>.0014f*(1-.55f*t),3,3,(p,t)=>Color.Lerp(Purple,C(.45f,.45f,.67f),t));
            }
        }
    }

    private static void Brain(Geometry m)
    {
        // Winding isocontours of a warped continuous field sculpt the ridges, not a flat painted map.
        float Field(Vector3 d)=>Mathf.Sin(d.x*5+1.2f*Mathf.Sin(d.z*3+d.y*2)) + .65f*Mathf.Sin(d.y*5-d.z*4+Mathf.Sin(d.x*3));
        m.Ellipsoid(new Vector3(0,.098f,0),new Vector3(.128f,.1f,.115f),90,128,
            d=>1+.04f*Mathf.Cos(Field(d)*Mathf.PI*2.2f),
            p=>
            {
                var d=Vector3.Scale(p-new Vector3(0,.098f,0),new Vector3(1/.128f,10,1/.115f)).normalized;
                float ridge=Mathf.Pow(.5f+.5f*Mathf.Cos(Field(d)*Mathf.PI*2.2f),4);
                return Color.Lerp(C(.015f,.3f,.29f),C(.84f,.83f,.23f),ridge);
            });
    }

    private static void BlueTubes(Geometry m)
    {
        for(int n=0;n<12;n++)
        {
            float a=n*2.4f,r=n==0?0:m.Range(.035f,.085f),h=n==0?.28f:m.Range(.13f,.265f),radius=m.Range(.018f,.026f);
            var start=Rad(a)*r+Vector3.up*.033f;var lean=Rad(a)*.026f;
            Vector3 At(float t)=>start+Vector3.up*(h*t)+lean*t*t;
            // Outer wall folds over a lip into a real dark hollow bore. No sealed sphere at the opening.
            m.Surface(18,28,(u,v)=>
            {
                float t=u<=.75f?u/.75f:1-(u-.75f)/.25f*.26f;
                float rad=u<=.75f?radius*(.82f+.18f*t):radius*Mathf.Lerp(1,.72f,(u-.75f)/.25f);
                float theta=v*Mathf.PI*2;rad*=1+.045f*Mathf.Sin(theta*9+t*23);
                return At(t)+Rad(theta)*rad;
            },(u,v,p)=>u>.8f?Color.Lerp(Purple*.7f,C(.045f,.08f,.2f),(u-.8f)*5):Color.Lerp(C(.04f,.37f,.67f),C(.4f,.52f,.94f),Mathf.Pow(u/.8f,4)),true);
        }
    }

    private static void Lobes(Geometry m)
    {
        for(int n=0;n<5;n++)
        {
            float angle=n*2.4f;var center=Rad(angle)*(n==0?0:.065f)+Vector3.up*(n==0?.095f:.068f);
            var rotation=Quaternion.Euler(-24+Mathf.Cos(angle)*14,n*31,Mathf.Sin(angle)*20);
            m.Surface(24,64,(u,v)=>
            {
                float theta=v*Mathf.PI*2,phi=u*Mathf.PI*2;
                float ring=.034f*(1+.15f*Mathf.Sin(theta*3+n));
                float thickness=.018f*(1+.08f*Mathf.Sin(theta*42));
                float radius=ring+thickness*Mathf.Cos(phi);
                float height=thickness*Mathf.Sin(phi)+.009f*Mathf.Sin(theta*3+n);
                return center+rotation*(Rad(theta)*radius+Vector3.up*height);
            },(u,v,p)=>
            {
                float outer=.5f+.5f*Mathf.Cos(u*Mathf.PI*2);
                var c=Color.Lerp(C(.87f,.26f,.2f),C(.52f,.85f,.81f),Mathf.Pow(outer,16)*.8f);
                c*=.76f+.24f*(.5f+.5f*Mathf.Sin(v*Mathf.PI*2*42));
                return u>.38f&&u<.65f?c*.62f:c;
            },true);
        }
    }

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<Color> colors=new List<Color>();
        private readonly List<Vector3> normals=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Vector2> uv=new List<Vector2>();
        private readonly System.Random random;
        public Geometry(int seed){random=new System.Random(seed);}
        public float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        public void Surface(int rows,int sides,Func<float,float,Vector3> point,Func<float,float,Vector3,Color> paint,bool reverse)
        {
            int first=vertices.Count;
            for(int row=0;row<=rows;row++)for(int col=0;col<=sides;col++)
            {
                float u=row/(float)rows,v=col/(float)sides;
                // Tiny central ring avoids zero-area pole faces and unstable normals.
                float sample=Mathf.Clamp(u,.0001f,.9999f);
                var p=point(sample,v);
                var du=(point(Mathf.Min(.99999f,sample+.00005f),v)-point(Mathf.Max(.00001f,sample-.00005f),v)).normalized;
                var dv=(point(sample,v+.00005f)-point(sample,v-.00005f)).normalized;
                var normal=Vector3.Cross(dv,du).normalized*(reverse?-1:1);
                normals.Add(normal.sqrMagnitude>.5f?normal:Vector3.up);
                vertices.Add(p);colors.Add(paint(u,v,p));uv.Add(new Vector2(u,v));
            }
            for(int row=0;row<rows;row++)for(int col=0;col<sides;col++)
            {
                int a=first+row*(sides+1)+col,b=a+1,c=a+sides+1,d=c+1;
                if(reverse)triangles.AddRange(new[]{a,c,b,b,c,d});
                else triangles.AddRange(new[]{a,b,c,b,d,c});
            }
        }
        public void Ellipsoid(Vector3 center,Vector3 size,int rings,int sides,Func<Vector3,float> shape,Func<Vector3,Color> paint)
        {
            Surface(rings,sides,(u,v)=>
            {
                float phi=u*Mathf.PI,theta=v*Mathf.PI*2;var d=new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
                return center+Vector3.Scale(d*shape(d),size);
            },(u,v,p)=>paint(p),false);
        }
        public void Tube(Func<float,Vector3> path,Func<float,float> radius,int rings,int sides,Func<Vector3,float,Color> paint)
        {
            var initialTangent=(path(.001f)-path(0)).normalized;
            var initialNormal=Vector3.Cross(initialTangent,Mathf.Abs(initialTangent.y)>.9f?Vector3.right:Vector3.up).normalized;
            Surface(rings,sides,(u,v)=>
            {
                var tangent=(path(Mathf.Min(1,u+.001f))-path(Mathf.Max(0,u-.001f))).normalized;
                var normal=Quaternion.FromToRotation(initialTangent,tangent)*initialNormal;
                var binormal=Vector3.Cross(tangent,normal);float a=v*Mathf.PI*2;
                return path(u)+(normal*Mathf.Cos(a)+binormal*Mathf.Sin(a))*radius(u);
            },(u,v,p)=>paint(p,u),false);
            float end=radius(1);
            Ellipsoid(path(1),Vector3.one*end,4,sides,d=>1,p=>paint(p,1));
        }
        public Mesh Finish(string name)
        {
            float floor=float.PositiveInfinity;foreach(var v in vertices)floor=Mathf.Min(floor,v.y);
            for(int i=0;i<vertices.Count;i++)vertices[i]-=Vector3.up*floor;
            var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.SetNormals(normals);mesh.RecalculateBounds();
            return mesh;
        }
    }
}
