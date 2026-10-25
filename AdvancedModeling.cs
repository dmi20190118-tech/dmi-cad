namespace DmiCad;

sealed partial class ModelingWindow
{
    void RefreshFeatureKind()
    {
        bool revolve=featureKind.SelectedIndex==1,fillet=featureKind.SelectedIndex==2;
        if(revolve||fillet){midplane.Checked=false;throughAll.Checked=false;}
        if(fillet){pendingPlaneSourceId="";planeLink.Checked=false;operation.SelectedIndex=0;}
        RefreshPlaneLink();
        angle.Enabled=revolve;filletRadius.Enabled=fillet;
        depth.Enabled=!revolve&&!fillet&&!throughAll.Checked;
        operation.Enabled=!fillet;
        midplane.Enabled=!revolve&&!fillet&&!throughAll.Checked;
        throughAll.Enabled=!revolve&&!fillet&&operation.SelectedIndex==1;
        reverse.Enabled=!fillet&&!midplane.Checked;
        if(fillet){plane.Enabled=false;offset.Enabled=false;planeLink.Enabled=false;}
    }
    static PartSketch Profile(params double[][] points)=>new(){DimensionMode="polygon",Points=points.ToList()};
    async Task<bool> LoadShowcase(bool prompt)
    {
        if(prompt&&!CanReplace())return false;
        var part=new PartDocument{Name="1.0 필렛 플레이트"};
        var plate=new PartFeature{Name="플레이트 100×70×12",Depth=12,Sketch=Profile([0,0],[100,0],[100,70],[0,70])};
        part.Features.Add(plate);
        part.Features.Add(new PartFeature{Name="전체 모서리 R3",FeatureKind="fillet",FilletRadius=3});
        part.Features.Add(new PartFeature{Name="큰 관통 구멍",Operation="cut",ThroughAll=true,Sketch=new PartSketch{Kind="circle",Center=[30,35],Radius=11}});
        part.Features.Add(new PartFeature{Name="원형 보스",PlaneSourceId=plate.Id,Offset=12,Depth=14,Sketch=new PartSketch{Kind="circle",Center=[73,35],Radius=16}});
        part.Features.Add(new PartFeature{Name="보스 관통 구멍",Operation="cut",ThroughAll=true,Sketch=new PartSketch{Kind="circle",Center=[73,35],Radius=7}});
        return await Commit(part);
    }
    async Task<bool> LoadTurnedShowcase(bool prompt)
    {
        if(prompt&&!CanReplace())return false;
        var part=new PartDocument{Name="1.0 회전 중공 축"};
        part.Features.Add(new PartFeature{Name="단차 단면 회전",FeatureKind="revolve",Plane="XZ",AngleDegrees=360,
            Sketch=Profile([10,0],[22,0],[22,8],[15,8],[15,45],[10,45])});
        return await Commit(part);
    }
    async Task CheckAdvancedWorkflow()
    {
        if(!await LoadTurnedShowcase(false))throw new Exception("Revolved showcase failed.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==0);
        OpenFeatureProperties(true);angle.Value=270;await ConfirmProperty();
        if(document.Features[0].AngleDegrees!=270||document.Features[0].FeatureKind!="revolve")throw new Exception("Revolve property edit failed.");
        await Undo(false);
        viewport.SetView("iso");await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));preview.Save(Path.Combine("preview","revolve-1.0-preview3.png"),System.Drawing.Imaging.ImageFormat.Png);}
        if(!await LoadShowcase(false))throw new Exception("Fillet showcase failed.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==1);
        OpenFeatureProperties(true);filletRadius.Value=2;await ConfirmProperty();
        if(document.Features[1].FilletRadius!=2||document.Features[1].FeatureKind!="fillet")throw new Exception("Fillet property edit failed.");
        await Undo(false);
        string file=Path.Combine(temp,"showcase.dmipart");
        await File.WriteAllTextAsync(file,System.Text.Json.JsonSerializer.Serialize(document.Copy(),PartDocument.Json));
        var reopened=PartDocument.Parse(await File.ReadAllTextAsync(file));
        if(reopened.Features[1].FeatureKind!="fillet"||reopened.Features[1].FilletRadius!=3||!await Build(reopened))throw new Exception("Advanced feature reopen failed.");
        viewport.SetView("iso");BuildTree();managerTabs.SelectedIndex=0;tabs.SelectedIndex=1;
    }
}
