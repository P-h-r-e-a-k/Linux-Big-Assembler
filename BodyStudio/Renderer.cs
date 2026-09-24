using System.Linq;
using System.Numerics;

namespace LbaBodyStudio;

// How LBA1 lights a body (LIB_3D P_OB_ISO.ASM ComputeAnimNormal): every normal is turned by its bone's matrix and by the
// actor's facing, dotted with the light direction, and the result (0 .. 15) is added to the polygon's palette index. Flat
// polygons use one normal, Gouraud polygons one per corner, blended across the face.
public sealed class Lba1Shading
{
    public required double[][] BoneMatrices { get; init; }   // 3 x 3, row-major, one per bone (without the actor's own turn)
    public double ActorBeta { get; init; }                   // the actor's facing, 1024ths of a turn
    public double AlphaLight { get; init; }                  // the scene's light angles, 1024ths of a turn
    public double BetaLight { get; init; }

    public float[] Intensities(Body model)
    {
        static double Rad(double a) => a * Math.PI * 2 / 1024;
        // the light: (0, 0, 1) through the same matrix the bones use (turns about x, z, y composed as M = Rx * Rz * Ry)
        double sa = Math.Sin(Rad(AlphaLight)), ca = Math.Cos(Rad(AlphaLight)), sb = Math.Sin(Rad(BetaLight)), cb = Math.Cos(Rad(BetaLight));
        double lx = sb, ly = -sa * cb, lz = ca * cb;
        double sy = Math.Sin(Rad(ActorBeta)), cy = Math.Cos(Rad(ActorBeta));
        var result = new float[model.Normals.Count];
        for (int i = 0; i < result.Length; i++)
        {
            var n = model.Normals[i];
            var m = BoneMatrices[Math.Clamp(i < model.NormalBone.Length ? model.NormalBone[i] : 0, 0, BoneMatrices.Length - 1)];
            double x = m[0]*n.X + m[1]*n.Y + m[2]*n.Z, y = m[3]*n.X + m[4]*n.Y + m[5]*n.Z, z = m[6]*n.X + m[7]*n.Y + m[8]*n.Z;
            double wx = cy*x + sy*z, wz = -sy*x + cy*z;      // then the actor's own turn
            double dot = wx*lx + y*ly + wz*lz;
            result[i] = dot <= 0 || n.Range == 0 ? 0 : (float)Math.Floor(dot * 59 / n.Range);   // the light vector is (0, 0, NORMAL_UNIT - 5) = 59 long
        }
        return result;
    }
}

public static class Renderer
{
    // Every colour here and in Render's parameters is a packed 0xAARRGGBB (see Argb), alpha 0xFF: what System.Drawing.Color.ToArgb() used to give.
    // KeyBackground and KeyGrid are the colours a marker is made transparent by (Lba1ActorImages.Transparent): renders that become markers keep them; a picture shown as it is passes its own.
    public static readonly uint KeyBackground=Argb.Pack(25,30,39),KeyGrid=Argb.Pack(44,52,64);
    public static readonly uint ViewBackground=Argb.Pack(232,240,250),ViewGrid=Argb.Pack(203,221,240);
    // The rest of the app's own light theme (Theme.axaml), so Body Studio's Avalonia windows don't look like a different program.
    public static readonly uint PanelBackground=Argb.Pack(0xE8,0xF0,0xFA),FieldBackground=Argb.Pack(0xFF,0xFF,0xFF),ButtonBackground=Argb.Pack(0xD6,0xE6,0xF7),
        ButtonBorder=Argb.Pack(0x9F,0xBE,0xE0),ButtonHover=Argb.Pack(0xC3,0xDB,0xF5),Accent=Argb.Pack(0x1B,0x6E,0xC2),
        Border=Argb.Pack(0xA9,0xC3,0xE0),Text=Argb.Pack(0x10,0x24,0x3E),TextMuted=Argb.Pack(0x4E,0x6B,0x8A);
    // The picture is a plain BGRA buffer (FlatImage); everything is drawn straight into the pixel array, nothing is anti-aliased.
    public static FlatImage Render(Body model,uint[] palette,int width,int height,float yaw,bool wire,bool bones=false,bool headOnly=false,Vector3[]? pose=null,Lba1Shading? shading=null,uint? background=null,uint? gridLine=null)
    {
        width=Math.Max(1,width);height=Math.Max(1,height);
        var pixels=new int[width*height];Array.Fill(pixels,unchecked((int)(background??KeyBackground)));
        var neutral=model.World();var world=pose??neutral;float h=Math.Max(1,neutral.Max(v=>v.Y)-neutral.Min(v=>v.Y));float minY=neutral.Min(v=>v.Y);
        if(headOnly){minY+=h*.82f;h*=.18f;}
        float visibleWidth=headOnly?h*.85f:neutral.Max(v=>v.X)-neutral.Min(v=>v.X);
        float scale=Math.Min(height*0.80f/h,width*0.80f/Math.Max(h*0.65f,visibleWidth));
        var focus=headOnly&&model.Bones.Count>14?new Vector3(world[model.Bones[14].Pivot].X,0,0):Vector3.Zero;
        var rotated=world.Select(v=>new Vector3((v.X-focus.X)*MathF.Cos(yaw)+(v.Z-focus.Z)*MathF.Sin(yaw),v.Y-minY,-(v.X-focus.X)*MathF.Sin(yaw)+(v.Z-focus.Z)*MathF.Cos(yaw))).ToArray();
        Vector2 Screen(Vector3 v)=>new(width/2f+v.X*scale,height*0.90f-v.Y*scale+v.Z*scale*0.08f);
        // the ground grid goes under the body: the polygons (and their depth buffer) are drawn over it
        int grid=unchecked((int)(gridLine??KeyGrid));
        for(int x=-5;x<=5;x++)Line(pixels,width,height,width/2f+x*h*scale/6,height*0.92f,width/2f+x*h*scale/6,height*0.97f,grid);
        Line(pixels,width,height,20,height*0.92f,width-20,height*0.92f,grid);
        // Polygon-average painter sorting loses narrow lettering at oblique angles.
        // Rasterize the actual surfaces with interpolated depth instead.
        var depth=Enumerable.Repeat(float.PositiveInfinity,width*height).ToArray();
        float Edge(Vector2 a,Vector2 b,float x,float y)=>(x-a.X)*(b.Y-a.Y)-(y-a.Y)*(b.X-a.X);
        var lit=shading!=null&&model.Game==1&&model.Normals.Count>0?shading.Intensities(model):null;
        // A generated body carries game lighting (Body.Lit): preview it with a light from above-left of the viewer, the game adds up to its
        // maximum number of ramp steps to each polygon's start colour.
        float[]? previewLight=null;
        if(lit==null&&(model.Lit||model.Faces.Any(x=>x.Material>=0)))
        {
            var normals=model.VertexNormals();var toLight=Vector3.Normalize(new Vector3(-0.35f,0.55f,-0.75f));float max=LightModel.Max(model.Game);
            previewLight=normals.Select(n=>{var r=new Vector3(n.X*MathF.Cos(yaw)+n.Z*MathF.Sin(yaw),n.Y,-n.X*MathF.Sin(yaw)+n.Z*MathF.Cos(yaw));return Math.Clamp(Vector3.Dot(r,toLight),0,1)*max;}).ToArray();
        }
        int Colour(int index)=>unchecked((int)palette[Math.Clamp(index,0,255)]);
        foreach(var f in model.Faces)
        {
            int colour=Colour(f.Colour);
            bool faceLit=previewLight!=null&&LightModel.IsLit(f,model.Game,model.Lit);
            // the game's lighting: flat faces take one intensity, Gouraud faces one per corner (blended below)
            float[]? corner=null;
            if(lit!=null&&f.Material>=7)
            {
                corner=new float[f.Points.Length];
                for(int k=0;k<corner.Length;k++)
                {
                    int normal=f.PointNormals!=null?f.PointNormals[k]:f.FaceNormal;
                    corner[k]=normal>=0&&normal<lit.Length?lit[normal]:0;
                }
                if(f.Material<9)colour=Colour(f.Colour+(int)corner[0]);
            }
            for(int t=1;t<f.Points.Length-1;t++)
            {
                var a=rotated[f.Points[0]];var b=rotated[f.Points[t]];var c=rotated[f.Points[t+1]];
                var pa=Screen(a);var pb=Screen(b);var pc=Screen(c);float area=Edge(pa,pb,pc.X,pc.Y);
                if(Math.Abs(area)<.001f)continue;
                int x0=Math.Max(0,(int)MathF.Floor(Math.Min(pa.X,Math.Min(pb.X,pc.X)))),x1=Math.Min(width-1,(int)MathF.Ceiling(Math.Max(pa.X,Math.Max(pb.X,pc.X))));
                int y0=Math.Max(0,(int)MathF.Floor(Math.Min(pa.Y,Math.Min(pb.Y,pc.Y)))),y1=Math.Min(height-1,(int)MathF.Ceiling(Math.Max(pa.Y,Math.Max(pb.Y,pc.Y))));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float wa=Edge(pb,pc,x+.5f,y+.5f)/area,wb=Edge(pc,pa,x+.5f,y+.5f)/area,wc=1-wa-wb;
                    if(wa<-.0001f||wb<-.0001f||wc<-.0001f)continue;
                    float z=wa*a.Z+wb*b.Z+wc*c.Z;int index=y*width+x;
                    if(z<=depth[index])
                    {
                        depth[index]=z;
                        if(f.Texture!=null&&model.TexturePage!=null&&f.Texture.Handle<model.Textures.Length)
                        {
                            // textured polygon: (U, V) are 8.8 fixed point pixels of the 256 x 256 page; the table entry gives the offset and a repeat mask per axis
                            var uv=f.Texture.UV;int a0=0,b0=t,c0=t+1;
                            float u=wa*uv[a0*2]+wb*uv[b0*2]+wc*uv[c0*2],v=wa*uv[a0*2+1]+wb*uv[b0*2+1]+wc*uv[c0*2+1];
                            uint info=model.Textures[f.Texture.Handle];int mask=(int)(info>>16);
                            int tx=((int)u>>8)&(mask&0xFF),ty=((int)v>>8)&((mask>>8)&0xFF);
                            int texel=model.TexturePage[((int)(info&0xFFFF)+ty*256+tx)&0xFFFF];
                            float shade=faceLit?wa*previewLight[f.Points[0]]+wb*previewLight[f.Points[t]]+wc*previewLight[f.Points[t+1]]:0;
                            pixels[index]=Colour(texel+(int)Math.Round(shade));
                        }
                        else if(faceLit){float shade=wa*previewLight[f.Points[0]]+wb*previewLight[f.Points[t]]+wc*previewLight[f.Points[t+1]];pixels[index]=Colour(f.Colour+(int)Math.Round(shade));}
                        else if(corner!=null&&f.Material>=9){float shade=wa*corner[0]+wb*corner[t]+wc*corner[t+1];pixels[index]=Colour(f.Colour+(int)Math.Round(shade));}
                        else pixels[index]=colour;
                    }
                }
            }
        }
        // Lines and spheres (hair buns, hands, necklaces) go through the same depth buffer as the polygons, so a
        // bun behind the head stays behind it instead of being painted over the face.
        void Plot(int x,int y,float z,int colour){if(x<0||y<0||x>=width||y>=height)return;int i=y*width+x;if(z<=depth[i]){depth[i]=z;pixels[i]=colour;}}
        foreach(var l in model.Lines)
        {
            var a=rotated[l.A];var b=rotated[l.B];var pa=Screen(a);var pb=Screen(b);int colour=Colour(l.Colour);
            int steps=Math.Max(1,(int)MathF.Ceiling(Math.Max(Math.Abs(pb.X-pa.X),Math.Abs(pb.Y-pa.Y))));
            for(int k=0;k<=steps;k++)
            {
                float t=k/(float)steps;int x=(int)MathF.Round(pa.X+(pb.X-pa.X)*t),y=(int)MathF.Round(pa.Y+(pb.Y-pa.Y)*t);float z=a.Z+(b.Z-a.Z)*t-2f;
                Plot(x,y,z,colour);Plot(x+1,y,z,colour);
            }
        }
        foreach(var s in model.Spheres)
        {
            var c=rotated[s.Point];var p=Screen(c);float r=Math.Max(1f,s.Radius*scale);int colour=Colour(s.Colour);
            for(int y=(int)MathF.Floor(p.Y-r);y<=(int)MathF.Ceiling(p.Y+r);y++)for(int x=(int)MathF.Floor(p.X-r);x<=(int)MathF.Ceiling(p.X+r);x++)
            {
                float dx=x+.5f-p.X,dy=y+.5f-p.Y,d2=dx*dx+dy*dy;if(d2>r*r)continue;
                Plot(x,y,c.Z-MathF.Sqrt(r*r-d2)/scale,colour);
            }
        }
        // the wireframe: every polygon's edges, a translucent light blue over the shaded picture
        if(wire){int pen=unchecked((int)Argb.Pack(110,185,210));foreach(var f in model.Faces)for(int j=0;j<f.Points.Length;j++){var a=Screen(rotated[f.Points[j]]);var b=Screen(rotated[f.Points[(j+1)%f.Points.Length]]);Line(pixels,width,height,a.X,a.Y,b.X,b.Y,pen,130);}}
        if(bones)
        {
            // the skeleton: a 2-pixel amber line from each bone's pivot to its parent's, a dot on the pivot, and the bone's number beside it
            int pen=unchecked((int)Argb.Pack(255,195,74));
            for(int i=1;i<model.Bones.Count;i++)
            {
                var b=model.Bones[i];var parent=model.Bones[b.Parent];var a=Screen(rotated[b.Pivot]);var z=Screen(rotated[parent.Pivot]);
                Line(pixels,width,height,a.X,a.Y,z.X,z.Y,pen);Line(pixels,width,height,a.X+1,a.Y,z.X+1,z.Y,pen);Line(pixels,width,height,a.X,a.Y+1,z.X,z.Y+1,pen);
                FillCircle(pixels,width,height,a.X,a.Y,3,pen);
                Digits(pixels,width,height,i.ToString(),(int)MathF.Round(a.X)+5,(int)MathF.Round(a.Y)+4,pen);
            }
        }
        return FlatImage.FromArgb(width,height,pixels);
    }

    // ---- drawing straight into the pixel buffer (what System.Drawing.Graphics did before) ----
    // One pixel; `alpha` below 255 blends the colour over what is there.
    static void Put(int[] pixels,int width,int height,int x,int y,int colour,int alpha=255)
    {
        if(x<0||y<0||x>=width||y>=height)return;
        int i=y*width+x;
        if(alpha>=255){pixels[i]=colour;return;}
        int d=pixels[i],inverse=255-alpha;
        int r=(((colour>>16)&0xFF)*alpha+((d>>16)&0xFF)*inverse)/255,g=(((colour>>8)&0xFF)*alpha+((d>>8)&0xFF)*inverse)/255,b=((colour&0xFF)*alpha+(d&0xFF)*inverse)/255;
        pixels[i]=unchecked((int)0xFF000000)|(r<<16)|(g<<8)|b;
    }
    // Bresenham's line, end points rounded to pixels.
    static void Line(int[] pixels,int width,int height,float fx0,float fy0,float fx1,float fy1,int colour,int alpha=255)
    {
        int x0=(int)MathF.Round(fx0),y0=(int)MathF.Round(fy0),x1=(int)MathF.Round(fx1),y1=(int)MathF.Round(fy1);
        int dx=Math.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.Abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;
        while(true)
        {
            Put(pixels,width,height,x0,y0,colour,alpha);
            if(x0==x1&&y0==y1)break;
            int e2=2*err;
            if(e2>=dy){err+=dy;x0+=sx;}
            if(e2<=dx){err+=dx;y0+=sy;}
        }
    }
    static void FillCircle(int[] pixels,int width,int height,float cx,float cy,float radius,int colour)
    {
        for(int y=(int)MathF.Floor(cy-radius);y<=(int)MathF.Ceiling(cy+radius);y++)for(int x=(int)MathF.Floor(cx-radius);x<=(int)MathF.Ceiling(cx+radius);x++)
        {
            float dx=x+.5f-cx,dy=y+.5f-cy;if(dx*dx+dy*dy<=radius*radius)Put(pixels,width,height,x,y,colour);
        }
    }
    // A 3 x 5 pixel digit font (each row is 3 bits, top row first), drawn at twice its size: the bone-number labels.
    static readonly byte[][] DigitFont=
    [
        [0b111,0b101,0b101,0b101,0b111],[0b010,0b110,0b010,0b010,0b111],[0b111,0b001,0b111,0b100,0b111],[0b111,0b001,0b111,0b001,0b111],[0b101,0b101,0b111,0b001,0b001],
        [0b111,0b100,0b111,0b001,0b111],[0b111,0b100,0b111,0b101,0b111],[0b111,0b001,0b001,0b001,0b001],[0b111,0b101,0b111,0b101,0b111],[0b111,0b101,0b111,0b001,0b111],
    ];
    static void Digits(int[] pixels,int width,int height,string text,int x,int y,int colour,int size=2)
    {
        foreach(var ch in text)
        {
            if(ch>='0'&&ch<='9')
            {
                var glyph=DigitFont[ch-'0'];
                for(int row=0;row<5;row++)for(int col=0;col<3;col++)
                {
                    if((glyph[row]&(4>>col))==0)continue;
                    for(int sy=0;sy<size;sy++)for(int sx=0;sx<size;sx++)Put(pixels,width,height,x+col*size+sx,y+row*size+sy,colour);
                }
            }
            x+=4*size;
        }
    }
}
