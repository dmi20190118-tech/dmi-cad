using System.Text.Json;
namespace DmiCad;

public sealed class SketchPlaneChoice
{
    public int FaceIndex { get; set; }
    public string Plane { get; set; } = "";
    public double Offset { get; set; }
    public double AreaMm2 { get; set; }
    public bool OutwardReverse { get; set; }
    public double[] Center { get; set; } = [];
    public override string ToString()=>$"면 {FaceIndex+1} · {Plane} · 위치 {Offset:G9} mm · 면적 {AreaMm2:G9} mm²";
}
sealed partial class ModelingWindow
{
    void ChooseSketchPlane()
    {
        if(sketchPlanes.Count==0){MessageBox.Show(this,"먼저 솔리드를 만드세요. XY/XZ/YZ와 평행한 평면 면이 필요합니다.");return;}
        using var dialog=new Form{Text="면에서 스케치 — 현재 면 위치 복사",Width=650,Height=380,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
        var list=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true};
        list.Items.AddRange(sketchPlanes.Cast<object>().ToArray());list.SelectedIndex=0;
        var hint=new Label{Text="면 위치가 이전 돌출 끝 평면과 유일하게 일치하면 연결합니다.\n연결할 수 없는 면은 고정 위치로 복사합니다. 속성에서 연결 여부를 확인하세요.",Dock=DockStyle.Top,Height=55,Padding=new Padding(8)};
        var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,Padding=new Padding(8)};
        var kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=95};
        kind.Items.AddRange(new object[]{"원","사각형"});kind.SelectedIndex=0;
        var cut=new CheckBox{Text="잘라내기용",Checked=true,AutoSize=true,Margin=new Padding(10,5,10,0)};
        var ok=new Button{Text="스케치 시작",AutoSize=true,DialogResult=DialogResult.OK};
        var cancel=new Button{Text="취소",DialogResult=DialogResult.Cancel};
        row.Controls.AddRange(new Control[]{kind,cut,ok,cancel});
        dialog.Controls.Add(list);dialog.Controls.Add(hint);dialog.Controls.Add(row);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
        if(dialog.ShowDialog(this)==DialogResult.OK&&list.SelectedItem is SketchPlaneChoice choice)
            BeginFromFace(choice,kind.SelectedIndex==0?"circle":"rectangle",cut.Checked);
    }
    void BeginFromFace(SketchPlaneChoice choice,string kind,bool cut)
    {
        if(!sketchPlanes.Contains(choice))throw new InvalidOperationException("모델이 변경됐습니다. 면 목록을 다시 여세요.");
        selected=-1;Begin(kind);
        plane.SelectedIndex=choice.Plane=="XY"?0:choice.Plane=="XZ"?1:2;
        offset.Value=(decimal)choice.Offset;operation.SelectedIndex=cut?1:0;
        reverse.Checked=cut?!choice.OutwardReverse:choice.OutwardReverse;
        // Global plane coordinates remain authoritative; no face-local origin is invented.
        if(choice.Center.Length==3)
        {
            u.Value=(decimal)Math.Clamp(choice.Plane=="YZ"?choice.Center[1]:choice.Center[0],-1000000,1000000);
            v.Value=(decimal)Math.Clamp(choice.Plane=="XY"?choice.Center[1]:choice.Center[2],-1000000,1000000);
        }
        var candidates=document.Features.Where(f=>f.Operation=="add"&&!f.Midplane&&f.Plane==choice.Plane&&Math.Abs(document.EndOffset(f)-choice.Offset)<1e-7&&f.Reverse==choice.OutwardReverse).ToList();
        if(candidates.Count==1){pendingPlaneSourceId=candidates[0].Id;planeLink.Checked=true;}
        RefreshPlaneLink();
        propertyHint.Text=planeLink.Checked?"돌출 끝 평면에 연결했습니다.\n원본 깊이가 바뀌면 평면도 이동합니다.\n좌표·치수 입력 후 치수 적용.":"선택 면의 위치를 복사했습니다.\n좌표·치수 입력 후 치수 적용.\n모델 변경 후 평면 위치를 확인하세요.";
        status.Text=$"면 {choice.FaceIndex+1}: {choice.Plane}, {choice.Offset:G9} mm — "+(planeLink.Checked?"돌출 끝 평면 연결됨":"고정 위치 복사됨");
    }
    async Task CheckFaceSketchWorkflow()
    {
        var top=sketchPlanes.First(p=>p.Plane=="XY"&&Math.Abs(p.Offset-10)<1e-8&&!p.OutwardReverse);
        int count=document.Features.Count;
        StartFacePick();viewport.SetView("top");viewport.Update();
        Point pixel=viewport.FindFacePixel(top.FaceIndex);
        if(pixel.X<0)throw new Exception("Top face was not visible for picking.");
        viewport.ClickForCheck(pixel);
        if(pickedPlane?.FaceIndex!=top.FaceIndex||viewport.SelectedFace!=top.FaceIndex)throw new Exception("Screen face picking failed.");
        await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));preview.Save(Path.Combine("preview","face-selection-0.4.1.png"),System.Drawing.Imaging.ImageFormat.Png);}
        StartPickedSketch();
        if(pickedPlane!=null||facePickActive)throw new Exception("Picked sketch retained a stale face target.");
        if(!planeLink.Checked||plane.Enabled||offset.Enabled)throw new Exception("Plane association did not lock numeric snapshot controls.");
        if(plane.SelectedIndex!=0||offset.Value!=10||!reverse.Checked||operation.SelectedIndex!=1)
            throw new Exception("Exact top face plane or inward cut direction was not applied.");
        u.Value=75;v.Value=50;radius.Value=3;ApplyDimensions();FinishSketchSession();
        OpenFeatureProperties(false,1);throughAll.Checked=true;
        await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));preview.Save(Path.Combine("preview","property-manager-0.4.1.png"),System.Drawing.Imaging.ImageFormat.Png);}
        await ConfirmProperty();
        if(document.Features.Count!=count+1||document.Features[^1].Offset!=10||!document.Features[^1].Reverse||!document.Features[^1].ThroughAll)
            throw new Exception("Face-started through cut failed.");
        string linkedId=document.Features[^1].Id;
        if(document.Features[^1].PlaneSourceId!=document.Features[0].Id)throw new Exception("Plane reference identity not committed.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==0);
        OpenFeatureProperties(true);depth.Value=15;await ConfirmProperty();
        var linked=document.Features.Single(f=>f.Id==linkedId);
        if(document.PlaneOffset(linked)!=15)throw new Exception("Linked plane did not move with thickness.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==document.Features.Count-1);
        OpenFeatureProperties(true);
        if(offset.Value!=15||!planeLink.Checked)throw new Exception("Properties displayed a stale linked offset.");
        await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));preview.Save(Path.Combine("preview","property-manager-0.4.1.png"),System.Drawing.Imaging.ImageFormat.Png);}
        CancelProperty();
        string file=Path.Combine(temp,"face-sketch.dmipart");
        await File.WriteAllTextAsync(file,JsonSerializer.Serialize(document.Copy(),PartDocument.Json));
        var reopened=PartDocument.Parse(await File.ReadAllTextAsync(file));
        if(reopened.PlaneOffset(reopened.Features[^1])!=15||reopened.Features[^1].PlaneSourceId!=reopened.Features[0].Id||!await Build(reopened))throw new Exception("Face plane disk reopen failed.");
        await Undo(false);await Undo(false);viewport.SetView("iso");
        StartFacePick();CancelFacePick();if(facePickActive||viewport.SelectedFace!=-1)throw new Exception("Face pick cancel failed.");
        // An old selection object must never be reused after regeneration.
        if(sketchPlanes.Contains(top))throw new Exception("Stale face list survived regeneration.");
    }
}
