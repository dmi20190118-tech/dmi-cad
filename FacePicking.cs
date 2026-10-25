using System.Numerics;
using System.Text.Json;
namespace DmiCad;

static class FaceMesh
{
    public static Geometry Read(JsonElement triangles)
    {
        if(triangles.ValueKind!=JsonValueKind.Array||triangles.GetArrayLength()>250000)throw new InvalidDataException("면 표시 메시 제한을 초과했습니다.");
        var geometry=new Geometry();
        foreach(var row in triangles.EnumerateArray())
        {
            if(row.GetArrayLength()!=10)throw new InvalidDataException("면 표시 데이터 구조가 올바르지 않습니다.");
            int id=row[0].GetInt32();if(id<0)throw new InvalidDataException("면 식별자가 올바르지 않습니다.");
            var points=new Vector3[3];
            for(int i=0;i<3;i++)points[i]=new Vector3(row[1+i*3].GetSingle(),row[2+i*3].GetSingle(),row[3+i*3].GetSingle());
            geometry.Triangles.Add(points);geometry.FaceIds.Add(id);
        }
        geometry.Finish();return geometry;
    }
}
sealed partial class Viewport
{
    RasterFrame? PickFrame()
    {
        if(Geometry is not Geometry geometry||geometry.FaceIds.Count!=geometry.Triangles.Count||Width<2||Height<2)return null;
        float ratio=Math.Min(1f,1600f/Math.Max(Width,Height));
        int w=Math.Max(1,(int)(Width*ratio)),h=Math.Max(1,(int)(Height*ratio));
        float scale=Math.Min(Width,Height)*.65f/geometry.Extent*zoom;
        return MeshRaster.Render(geometry,w,h,point=>
        {
            var q=point-geometry.Center;var p=Rotate(q.X,q.Y,q.Z);
            return ((Width/2f+pan.X+p.x*scale)*ratio,(Height/2f+pan.Y-p.y*scale)*ratio,p.z);
        });
    }
    public int PickFace(Point point)
    {
        if(!ClientRectangle.Contains(point))return -1;
        using var frame=PickFrame();if(frame==null||Geometry==null)return -1;
        float ratio=Math.Min(1f,1600f/Math.Max(Width,Height));
        int x=Math.Clamp((int)(point.X*ratio),0,frame.Image.Width-1),y=Math.Clamp((int)(point.Y*ratio),0,frame.Image.Height-1);
        int triangle=frame.TriangleIds[y*frame.Image.Width+x];
        return triangle>=0?Geometry.FaceIds[triangle]:-1;
    }
    public Point FindFacePixel(int id)
    {
        using var frame=PickFrame();if(frame==null||Geometry==null)return new Point(-1,-1);
        float ratio=Math.Min(1f,1600f/Math.Max(Width,Height));
        for(int y=2;y<frame.Image.Height-2;y++)
        for(int x=2;x<frame.Image.Width-2;x++)
        {
            int t=frame.TriangleIds[y*frame.Image.Width+x];
            if(t<0||Geometry.FaceIds[t]!=id)continue;
            bool inside=true;
            foreach(int d in new[]{-2,2,-2*frame.Image.Width,2*frame.Image.Width})
            {int other=frame.TriangleIds[y*frame.Image.Width+x+d];if(other<0||Geometry.FaceIds[other]!=id)inside=false;}
            if(inside)return new Point((int)Math.Ceiling(x/ratio),(int)Math.Ceiling(y/ratio));
        }
        return new Point(-1,-1);
    }
    public void ClickForCheck(Point pixel)=>OnMouseClick(new MouseEventArgs(MouseButtons.Left,1,pixel.X,pixel.Y,0));
}
sealed partial class ModelingWindow
{
    bool facePickActive;
    SketchPlaneChoice? pickedPlane;
    void StartFacePick()
    {
        if(geometry==null||sketchPlanes.Count==0){MessageBox.Show(this,"먼저 솔리드를 만드세요.");return;}
        if(propertyActive)CancelProperty();
        facePickActive=true;pickedPlane=null;viewport.SelectedFace=-1;viewport.Wire=false;
        tabs.SelectedIndex=1;commandTabs.SelectedIndex=1;viewport.Invalidate();
        status.Text="모델의 평평한 면을 클릭하세요. 주황색 확인 후 '선택면 스케치'. Esc: 취소.";
    }
    void FaceWasPicked(int id)
    {
        if(!facePickActive){viewport.SelectedFace=-1;viewport.Invalidate();return;}
        pickedPlane=sketchPlanes.FirstOrDefault(p=>p.FaceIndex==id);
        if(pickedPlane==null)
        {
            viewport.SelectedFace=-1;viewport.Invalidate();
            status.Text=id<0?"선택된 면이 없습니다. 모델의 면을 클릭하세요.":"이 면은 곡면 또는 경사진 면입니다. XY/XZ/YZ와 평행한 면을 선택하세요.";
            return;
        }
        status.Text=$"선택: {pickedPlane}. '선택면 스케치'를 누르세요. 연결 가능 여부는 스케치 속성에서 확인하세요.";
    }
    void StartPickedSketch()
    {
        if(!facePickActive||pickedPlane==null){MessageBox.Show(this,"먼저 '화면 면 선택'을 누르고 평평한 면을 클릭하세요.");return;}
        var choice=pickedPlane;CancelFacePick();BeginFromFace(choice,"circle",true);
    }
    void CancelFacePick(){facePickActive=false;pickedPlane=null;viewport.SelectedFace=-1;viewport.Invalidate();}
}
