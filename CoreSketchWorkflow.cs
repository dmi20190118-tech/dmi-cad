using System.Numerics;
using System.Text.Json;
namespace DmiCad;
sealed partial class ModelingWindow
{
    bool sketchSession,faceSketch,faceOutwardReverse;
    double modelVolume;
    string CurrentPlaneLabel()
    {
        string name=plane.SelectedIndex==1?"XZ":plane.SelectedIndex==2?"YZ":"XY";
        return $"스케치 평면: {name} · 위치 {offset.Value:G9} mm"+(planeLink.Checked?" · 돌출 끝 평면 연결":"");
    }
    void EndSketchSession()
    {
        sketchSession=false;faceSketch=false;faceOutwardReverse=false;editingExisting=false;
        sketch.SetReference(Array.Empty<double[][]>(),"스케치 평면");
    }
    void SetFaceSketchReference(SketchPlaneChoice choice)
    {
        if(geometry==null)return;
        double[] UV(Vector3 p)=>choice.Plane=="XY"?new double[]{p.X,p.Y}:choice.Plane=="XZ"?new double[]{p.X,p.Z}:new double[]{p.Y,p.Z};
        var triangles=geometry.Triangles.Where((_,i)=>geometry.FaceIds.Count>i&&geometry.FaceIds[i]==choice.FaceIndex)
            .Select(t=>t.Select(UV).ToArray()).ToArray();
        sketch.SetReference(triangles,CurrentPlaneLabel());sketch.Fit();
    }
    static IEnumerable<TreeNode> AllNodes(TreeNode root)
    {
        yield return root;
        foreach(TreeNode child in root.Nodes)foreach(var node in AllNodes(child))yield return node;
    }
    void SelectBasePlaneForCheck(string name)
    {
        var node=AllNodes(tree.Nodes[0]).First(n=>n.Tag is string s&&s=="plane:"+name);
        node.Parent?.Expand();tree.SelectedNode=node;
    }
    void ClickPlanarFaceForCheck(double z)
    {
        var face=sketchPlanes.First(p=>p.Plane=="XY"&&Math.Abs(p.Offset-z)<1e-8&&!p.OutwardReverse);
        viewport.SetView("top");viewport.Update();
        var pixel=viewport.FindFacePixel(face.FaceIndex);
        if(pixel.X<0)throw new Exception("Expected face not visible.");
        viewport.ClickForCheck(pixel);
        if(pickedPlane?.FaceIndex!=face.FaceIndex)throw new Exception("Ordinary face click did not select the face.");
    }
    async Task CaptureCoreCheck(string name)
    {
        await Task.Delay(100);Update();Directory.CreateDirectory("preview");
        using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));
        bitmap.Save(Path.Combine("preview",name),System.Drawing.Imaging.ImageFormat.Png);
    }
    async Task CheckCoreSketchWorkflow()
    {
        if(!await Commit(new PartDocument{Name="기본 모델링 흐름 검증"}))throw new Exception("Cannot reset workflow document.");
        SelectBasePlaneForCheck("XZ");Begin("rectangle");Begin("circle");Begin("rectangle");
        if(plane.SelectedIndex!=1||offset.Value!=0)throw new Exception("Base-plane sketch tool change lost plane.");
        CancelProperty();RefreshWorkflowGuide();guidePlanes[0].PerformClick();RefreshWorkflowGuide();
        if(!guideStart.Enabled||!guideTarget.Text.Contains("XY"))throw new Exception("Guide did not identify the selected base plane.");
        guideStart.PerformClick();guideRectangle.PerformClick();
        sketch.ClickWorldForCheck(0,0);sketch.ClickWorldForCheck(60,40);
        if(!sketch.Closed)throw new Exception("Mouse rectangle did not close.");
        RefreshWorkflowGuide();guideBoss.PerformClick();depth.Value=10;await ConfirmProperty();
        if(document.Features.Count!=1||Math.Abs(modelVolume-24000)>1e-5)throw new Exception("Plane → mouse rectangle → base extrusion failed.");
        // No special 'face select' mode is armed. Normal click then ordinary sketch tool.
        ClickPlanarFaceForCheck(10);RefreshWorkflowGuide();
        if(!guideStart.Enabled||!guideStart.Text.Contains("이 면")||!guideTarget.Text.Contains("10"))throw new Exception("Guide did not identify the selected model face.");
        await CaptureCoreCheck("selected-face-1.0-preview3.png");
        guideStart.PerformClick();guideCircle.PerformClick();
        if(offset.Value!=10||!planeLink.Checked||!faceSketch||sketch.ReferenceTriangles.Count==0)throw new Exception("Face context lost on sketch tool change.");
        sketch.ClickWorldForCheck(40,20);sketch.ClickWorldForCheck(45,20);
        if(!sketch.Closed)throw new Exception("Mouse circle did not close.");
        RefreshWorkflowGuide();
        if(!guideBoss.Visible||!guideTarget.Text.Contains("닫힌 스케치")||!guideTarget.Text.Contains("10"))
            throw new Exception("Closed sketch did not expose the next boss/cut actions.");
        await CaptureCoreCheck("core-sketch-1.0-preview3.png");
        guideBoss.PerformClick();depth.Value=8;RefreshWorkflowGuide();
        if(!guideConfirm.Enabled||!guideStage.Text.Contains("③"))throw new Exception("Guide did not reach feature confirmation.");
        if(reverse.Checked)throw new Exception("Boss pointed inside instead of outward.");
        await ConfirmProperty();
        double bossVolume=24000+Math.PI*25*8;
        if(document.Features.Count!=2||Math.Abs(modelVolume-bossVolume)>1e-5||document.Features[1].PlaneSourceId!=document.Features[0].Id)
            throw new Exception("Selected face → new mouse sketch → outward boss failed.");
        ClickPlanarFaceForCheck(18);RefreshWorkflowGuide();guideStart.PerformClick();guideCircle.PerformClick();
        sketch.ClickWorldForCheck(40,20);sketch.ClickWorldForCheck(42,20);
        RefreshWorkflowGuide();guideCut.PerformClick();throughAll.Checked=true;
        if(!reverse.Checked||offset.Value!=18)throw new Exception("Cut did not point inside from selected top face.");
        await ConfirmProperty();
        double cutVolume=bossVolume-Math.PI*4*18;
        if(document.Features.Count!=3||Math.Abs(modelVolume-cutVolume)>1e-5)throw new Exception("Boss face → sketch → through cut failed.");
        string path=Path.Combine(temp,"core-workflow.dmipart");
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(document.Copy(),PartDocument.Json));
        var reopened=PartDocument.Parse(await File.ReadAllTextAsync(path));
        if(!await Build(reopened)||Math.Abs(modelVolume-cutVolume)>1e-5)throw new Exception("Core workflow reopen changed geometry.");
        await Undo(false);if(Math.Abs(modelVolume-bossVolume)>1e-5)throw new Exception("Core workflow undo failed.");
        await Undo(true);if(Math.Abs(modelVolume-cutVolume)>1e-5)throw new Exception("Core workflow redo failed.");
        viewport.SetView("iso");managerTabs.SelectedIndex=0;tabs.SelectedIndex=1;
        await CaptureCoreCheck("core-workflow-1.0-preview3.png");
    }
}
