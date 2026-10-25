using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
namespace DmiCad;

sealed class RasterFrame : IDisposable
{
    public required Bitmap Image { get; init; }
    public required int[] TriangleIds { get; init; }
    public void Dispose()=>Image.Dispose();
}
static class MeshRaster
{
    public static RasterFrame Render(Geometry geometry,int width,int height,Func<Vector3,(double x,double y,double z)> project)
    {
        if(width<1||height<1||width>2000||height>2000)throw new ArgumentOutOfRangeException(nameof(width));
        var depths=new double[width*height];Array.Fill(depths,double.PositiveInfinity);
        var ids=new int[width*height];Array.Fill(ids,-1);
        var pixels=new byte[width*height*4];
        double Edge((double x,double y,double z) a,(double x,double y,double z) b,double x,double y)=>(x-a.x)*(b.y-a.y)-(y-a.y)*(b.x-a.x);
        for(int index=0;index<geometry.Triangles.Count;index++)
        {
            var triangle=geometry.Triangles[index];var a=project(triangle[0]);var b=project(triangle[1]);var c=project(triangle[2]);
            if(!double.IsFinite(a.x+a.y+a.z+b.x+b.y+b.z+c.x+c.y+c.z))continue;
            double area=Edge(a,b,c.x,c.y);if(Math.Abs(area)<1e-10)continue;
            int x0=(int)Math.Clamp(Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x))),0,width-1);
            int x1=(int)Math.Clamp(Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))),0,width-1);
            int y0=(int)Math.Clamp(Math.Floor(Math.Min(a.y,Math.Min(b.y,c.y))),0,height-1);
            int y1=(int)Math.Clamp(Math.Ceiling(Math.Max(a.y,Math.Max(b.y,c.y))),0,height-1);
            var normal=Vector3.Cross(triangle[1]-triangle[0],triangle[2]-triangle[0]);
            double brightness=.5;
            if(normal.LengthSquared()>0)
            {
                normal=Vector3.Normalize(normal);
                brightness=.3+.7*Math.Abs(Vector3.Dot(normal,Vector3.Normalize(new Vector3(.35f,-.45f,.82f))));
            }
            byte red=(byte)Math.Clamp(75+110*brightness,0,255),green=(byte)Math.Clamp(110+105*brightness,0,255),blue=(byte)Math.Clamp(140+100*brightness,0,255);
            for(int y=y0;y<=y1;y++)
            for(int x=x0;x<=x1;x++)
            {
                double w0=Edge(b,c,x+.5,y+.5)/area,w1=Edge(c,a,x+.5,y+.5)/area,w2=1-w0-w1;
                if(w0< -1e-9||w1< -1e-9||w2< -1e-9)continue;
                double z=w0*a.z+w1*b.z+w2*c.z;
                int p=y*width+x;if(z>=depths[p])continue;
                depths[p]=z;ids[p]=index;
                int i=p*4;pixels[i]=blue;pixels[i+1]=green;pixels[i+2]=red;pixels[i+3]=255;
            }
        }
        var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        var bits=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{for(int row=0;row<height;row++)Marshal.Copy(pixels,row*width*4,IntPtr.Add(bits.Scan0,row*bits.Stride),width*4);}
        finally{bitmap.UnlockBits(bits);}
        return new RasterFrame{Image=bitmap,TriangleIds=ids};
    }
}
static class RasterChecks
{
    public static void Run()
    {
        var near=new[]{new Vector3(1,1,-5),new Vector3(9,1,-5),new Vector3(1,9,-5)};
        var far=new[]{new Vector3(1,1,5),new Vector3(9,1,5),new Vector3(1,9,5)};
        var geometry=new Geometry();geometry.Triangles.Add(near);geometry.Triangles.Add(far);
        using var first=MeshRaster.Render(geometry,12,12,p=>(p.X,p.Y,p.Z));
        if(first.TriangleIds[3*12+3]!=0||first.TriangleIds[11*12+11]!=-1)throw new Exception("Depth buffer did not preserve front geometry.");
        geometry.Triangles.Reverse();
        using var second=MeshRaster.Render(geometry,12,12,p=>(p.X,p.Y,p.Z));
        if(second.TriangleIds[3*12+3]!=1)throw new Exception("Visibility depends on triangle submission order.");
    }
}
