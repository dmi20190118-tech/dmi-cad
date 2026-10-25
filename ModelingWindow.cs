using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace DmiCad;

sealed partial class ModelingWindow : Form
{
    readonly SketchEditor sketch=new();
    readonly Viewport viewport=new(){Model=new Model{Visible=false}};
    readonly TreeView tree=new(){Dock=DockStyle.Fill,HideSelection=false};
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    readonly TextBox report=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
    readonly ToolStripStatusLabel status=new("스케치를 그리거나 치수로 정의하세요.");
    readonly NumericUpDown u=N(0,-1000000,1000000),v=N(0,-1000000,1000000),width=N(100,.001m,2000000),
        height=N(70,.001m,2000000),radius=N(5,.001m,1000000),depth=N(10,.001m,10000),
        offset=N(0,-1000000,1000000),vertex=N(1,1,500),grid=N(1,.001m,1000);
    readonly ComboBox featureKind=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly NumericUpDown angle=N(360,.001m,360),filletRadius=N(2,.001m,10000);
    readonly TextBox featureName=new(){Text="돌출 1",MaxLength=200,Dock=DockStyle.Fill};
    string pendingPlaneSourceId="";
    readonly CheckBox planeLink=new(){Text="돌출 끝 평면 연결",AutoSize=true};
    readonly Label planeLinkInfo=new(){AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
    readonly CheckBox throughAll=new(){Text="끝까지 관통",AutoSize=true};
    readonly CheckBox midplane=new(){Text="중간 평면",AutoSize=true};
    readonly CheckBox reverse=new(){Text="반대 방향",AutoSize=true};
    readonly ComboBox plane=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly ComboBox operation=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly Stack<PartDocument> undo=new(),redo=new();
    readonly string temp=Path.Combine(Path.GetTempPath(),"DMI-CAD-"+Guid.NewGuid().ToString("N"));
    PartDocument document=new();
    Geometry? geometry;
    List<SketchPlaneChoice> sketchPlanes=new();
    string? projectPath;
    string dimensionMode="rectangle";
    bool rebuilding,dirty,sync;
    int selected=-1;
    public ModelingWindow()
    {
        Directory.CreateDirectory(temp);
        Text="디엠아이(DMI) CAD 1.0 개발판 — 정밀 스케치 / 솔리드";
        Width=1500;Height=960;MinimumSize=new Size(1120,760);Font=new Font("Segoe UI",9);
        KeyPreview=true;
        BuildShell();
        StartWorkflowGuide();
        viewport.FacePicked+=FaceWasPicked;
        operation.SelectedIndexChanged+=(_,_)=>{if(faceSketch&&featureKind.SelectedIndex==0)reverse.Checked=faceOutwardReverse^(operation.SelectedIndex==1);};
        foreach(var input in new[]{u,v,width,height,radius,vertex})
            input.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter&&propertyActive&&propertyKind=="sketch"){e.SuppressKeyPress=true;ApplyDimensions();}};
        sketch.Changed+=(_,_)=>ShowSketchDimensions();
        grid.ValueChanged+=(_,_)=>sketch.Grid=(double)grid.Value;
        vertex.ValueChanged+=(_,_)=>{if(sync||sketch.Sketch.Kind=="circle"||!sketch.Closed)return;int i=(int)vertex.Value-1;if(i<sketch.Sketch.Points.Count){u.Value=(decimal)sketch.Sketch.Points[i][0];v.Value=(decimal)sketch.Sketch.Points[i][1];}};
        tree.AfterSelect+=(_,e)=>
        {
            if(e.Node?.Tag is int i)SelectFeature(i);
            else
            {
                selected=-1;
                if(e.Node?.Tag is string planeTag&&planeTag.StartsWith("plane:"))
                {
                    EndSketchSession();CancelFacePick();pendingPlaneSourceId="";planeLink.Checked=false;RefreshPlaneLink();
                    plane.SelectedIndex=planeTag=="plane:XY"?0:planeTag=="plane:XZ"?1:2;offset.Value=0;
                    status.Text="기준면 선택됨. 스케치 탭에서 도형을 시작하세요.";
                }
            }
        };
        KeyDown+=async(_,e)=>{if(e.Control&&e.KeyCode==Keys.Z){e.SuppressKeyPress=true;await Undo(false);}else if(e.Control&&e.KeyCode==Keys.Y){e.SuppressKeyPress=true;await Undo(true);}else if(e.Control&&e.KeyCode==Keys.S){e.SuppressKeyPress=true;SaveProject();}else if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;CancelFacePick();CancelProperty();}else if(e.KeyCode==Keys.F1){e.SuppressKeyPress=true;Help();}};
        FormClosing+=(_,e)=>{if(rebuilding){e.Cancel=true;return;}if(dirty&&MessageBox.Show(this,"저장하지 않은 모델 변경을 닫을까요?","변경 확인",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
        FormClosed+=(_,_)=>{guideTimer.Stop();guideTimer.Dispose();tips.Dispose();try{Directory.Delete(temp,true);}catch{}};
        BuildTree();tabs.SelectedIndex=1;status.Text="스케치 탭에서 도형을 선택해 시작하세요.";
    }
    readonly ToolTip tips=new(){AutoPopDelay=15000};
    static NumericUpDown N(decimal value,decimal min,decimal max)=>new(){Minimum=min,Maximum=max,Value=value,DecimalPlaces=3,Increment=1,ThousandsSeparator=false};
    void Try(Action action){try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"확인 필요");}}
    bool CanReplace()=>!dirty||MessageBox.Show(this,"저장하지 않은 모델 변경을 버리고 진행할까요?","변경 확인",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
    void Begin(string mode)
    {
        if(pickedPlane!=null&&!editingExisting){var choice=pickedPlane;BeginFromFace(choice,mode,false);return;}
        bool keepEdit=sketchSession&&editingExisting;
        if(!sketchSession)
        {
            pendingPlaneSourceId="";planeLink.Checked=false;RefreshPlaneLink();faceSketch=false;
            throughAll.Checked=false;reverse.Checked=false;midplane.Checked=false;selected=-1;
            sketch.SetReference(Array.Empty<double[][]>(),CurrentPlaneLabel());
        }
        sketchSession=true;if(!keepEdit)featureKind.SelectedIndex=0;dimensionMode=mode;editingExisting=keepEdit;
        sketch.Begin(mode);sketch.Fit();tabs.SelectedIndex=0;
        if(shellReady)OpenSketchProperties();
        if(!keepEdit)featureName.Text=(operation.SelectedIndex==1?"잘라내기 ":"돌출 ")+(document.Features.Count+1);
        status.Text="선택 평면 유지됨. 도형을 그린 뒤 스케치 종료 → 돌출 보스 또는 컷.";
    }
    void ShowSketchDimensions()
    {
        if(!sketch.Closed)return;sync=true;
        var s=sketch.Sketch;
        if(s.Kind=="circle"){u.Value=(decimal)s.Center[0];v.Value=(decimal)s.Center[1];radius.Value=(decimal)Math.Clamp(s.Radius,.001,1000000);dimensionMode="circle";}
        else if(s.Points.Count>0)
        {
            double minU=s.Points.Min(p=>p[0]),minV=s.Points.Min(p=>p[1]),w=s.Points.Max(p=>p[0])-minU,h=s.Points.Max(p=>p[1])-minV;
            u.Value=(decimal)minU;v.Value=(decimal)minV;width.Value=(decimal)Math.Clamp(w,.001,2000000);height.Value=(decimal)Math.Clamp(h,.001,2000000);
        }
        vertex.Value=1;sync=false;
    }
    void ApplyDimensions()
    {
        Try(()=>
        {
            PartSketch s;
            if(dimensionMode=="circle")s=new PartSketch{Kind="circle",Center=[(double)u.Value,(double)v.Value],Radius=(double)radius.Value};
            else if(dimensionMode=="rectangle")
            {
                double x=(double)u.Value,y=(double)v.Value,w=(double)width.Value,h=(double)height.Value;
                s=new PartSketch {DimensionMode="rectangle",Points=new(){new[]{x,y},new[]{x+w,y},new[]{x+w,y+h},new[]{x,y+h}}};
            }
            else
            {
                if(!sketch.Closed)throw new InvalidDataException("먼저 선 스케치를 닫으세요.");
                s=sketch.Sketch.Copy();int i=(int)vertex.Value-1;
                if(i>=s.Points.Count)throw new InvalidDataException("해당 꼭짓점이 없습니다.");
                s.Points[i]=[(double)u.Value,(double)v.Value];
            }
            sketch.SetSketch(s);tabs.SelectedIndex=0;status.Text="스케치 치수 적용됨. 3D에 반영하려면 돌출/잘라내기 또는 피처 수정을 누르세요.";
        });
    }
    void SelectFeature(int index)
    {
        if(index<0||index>=document.Features.Count)return;
        EndSketchSession();selected=index;var f=document.Features[index];featureKind.SelectedIndex=f.FeatureKind=="revolve"?1:f.FeatureKind=="fillet"?2:0;angle.Value=(decimal)f.AngleDegrees;filletRadius.Value=(decimal)f.FilletRadius;
        featureName.Text=f.Name;plane.SelectedIndex=f.Plane=="XY"?0:f.Plane=="XZ"?1:2;operation.SelectedIndex=f.Operation=="add"?0:1;
        throughAll.Checked=f.ThroughAll;midplane.Checked=f.Midplane;reverse.Checked=f.Reverse;pendingPlaneSourceId=f.PlaneSourceId;planeLink.Checked=pendingPlaneSourceId.Length>0;RefreshPlaneLink();offset.Value=(decimal)document.PlaneOffset(f);depth.Value=(decimal)f.Depth;dimensionMode=f.Sketch.Kind=="circle"?"circle":f.Sketch.DimensionMode;
        sketch.SetSketch(f.Sketch);RefreshFeatureKind();status.Text=$"선택: {f.Name}. 수정 후 '피처 수정'으로 전체 재계산.";
    }
    void BuildTree()
    {
        propertyActive=false;editingExisting=false;managerTabs.SelectedIndex=0;
        tree.Nodes.Clear();var root=tree.Nodes.Add(document.Name+" · rev "+document.Revision);
        root.Nodes.Add("주석 / 단위: mm");root.Nodes.Add("재질 <지정되지 않음>");
        var planes=root.Nodes.Add("기준면");
        planes.Nodes.Add("XY 기준면").Tag="plane:XY";planes.Nodes.Add("XZ 기준면").Tag="plane:XZ";planes.Nodes.Add("YZ 기준면").Tag="plane:YZ";
        root.Nodes.Add("원점");
        string filter=treeFilter.Text.Trim();
        for(int i=0;i<document.Features.Count;i++)
        {
            var f=document.Features[i];
            if(filter.Length>0&&!f.Name.Contains(filter,StringComparison.OrdinalIgnoreCase))continue;
            string detail=f.FeatureKind=="fillet"?$"필렛 R{f.FilletRadius:G6} mm":f.FeatureKind=="revolve"?$"회전 {f.AngleDegrees:G6}°":$"{f.Plane} / {(f.ThroughAll?"관통":$"{f.Depth:G6} mm")}{(f.Midplane?" / 중간 평면":f.Reverse?" / 반대":"")}";
            var node=root.Nodes.Add($"{i+1}. {f.Name} [{detail}]");node.Tag=i;
            node.Nodes.Add(f.FeatureKind=="fillet"?"전체 모서리 필렛":f.FeatureKind=="revolve"?"회전 · "+f.AngleDegrees+"°":f.Sketch.Kind=="circle"?"스케치 · 원":"스케치 · 닫힌 다각형").Tag=i;
        }
        root.Expand();selected=-1;Text=$"디엠아이(DMI) CAD 1.0-preview3 — {document.Name}"+(dirty?" *":"");
    }
    async Task<bool> Build(PartDocument next)
    {
        if(rebuilding)return false;
        try
        {
            next=next.Copy();rebuilding=true;Enabled=false;status.Text="정밀 솔리드 재계산 중…";UseWaitCursor=true;
            if(next.Features.Count==0){modelVolume=0;geometry=null;sketchPlanes.Clear();viewport.Geometry=null;viewport.Invalidate();report.Text="비어 있는 프로젝트";return true;}
            string source=Path.Combine(temp,Guid.NewGuid()+".dmipart");
            string output=Path.ChangeExtension(source,".stl");
            await File.WriteAllTextAsync(source,JsonSerializer.Serialize(next,PartDocument.Json),Encoding.UTF8);
            await CadFiles.Convert(source,output);
            var mesh=await Task.Run(()=>CadFiles.Stl(output));mesh.Name=next.Name;mesh.Source=source;
            using var metrics=JsonDocument.Parse(await File.ReadAllTextAsync(output+".metrics.json"));
            double volume=metrics.RootElement.GetProperty("volumeMm3").GetDouble();
            var nextPlanes=JsonSerializer.Deserialize<List<SketchPlaneChoice>>(metrics.RootElement.GetProperty("sketchPlanes").GetRawText(),PartDocument.Json)??new();
            mesh=FaceMesh.Read(metrics.RootElement.GetProperty("renderTriangles"));mesh.Name=next.Name;mesh.Source=source;
            modelVolume=volume;CancelFacePick();sketchPlanes=nextPlanes;geometry=mesh;viewport.Geometry=mesh;viewport.Fit();
            report.Text=$"Open CASCADE 정밀 형상 검증 통과\r\n유효한 단일 솔리드\r\n단위: mm\r\n정확한 부피: {volume:G12} mm³\r\n피처: {next.Features.Count}\r\n표시 삼각형: {mesh.Triangles.Count:N0}\r\n표시 메시 외형: {mesh.Size.X:G7} × {mesh.Size.Y:G7} × {mesh.Size.Z:G7} mm\r\n\r\n외형은 표시 메시의 범위이며 정밀 면/모서리 측정 도구가 아닙니다.\r\n프로젝트는 스케치와 피처 이력을 저장하고 재계산합니다.\r\n일반 스케치 구속 솔버, 임의 기울기 면 스케치, 도면·조립·금형·CAM/NC는 아직 구현되지 않았습니다.";
            return true;
        }
        catch(Exception ex){if(Program.Verifying)throw;MessageBox.Show(this,ex.Message+"\n기존 모델은 변경하지 않았습니다.","모델 재계산 실패");return false;}
        finally{rebuilding=false;Enabled=true;UseWaitCursor=false;status.Text="준비";}
    }
    async Task<bool> Commit(PartDocument next)
    {
        next.Revision=document.Revision+1;
        if(!await Build(next))return false;
        EndSketchSession();undo.Push(document.Copy());redo.Clear();document=next;dirty=true;BuildTree();tabs.SelectedIndex=1;status.Text="모델 재계산 완료 · 프로젝트를 저장하세요.";return true;
    }
    async Task<bool> ApplyFeature(bool edit)
    {
        if(featureKind.SelectedIndex!=2&&!sketch.Closed){MessageBox.Show(this,"닫힌 스케치를 먼저 그리거나 치수를 적용하세요.");return false;}
        if(edit&&(selected<0||selected>=document.Features.Count)){MessageBox.Show(this,"트리에서 수정할 피처를 선택하세요.");return false;}
        var next=document.Copy();
        var feature=new PartFeature{Name=string.IsNullOrWhiteSpace(featureName.Text)?"피처 "+(next.Features.Count+1):featureName.Text,
            FeatureKind=featureKind.SelectedIndex==1?"revolve":featureKind.SelectedIndex==2?"fillet":"extrude",AngleDegrees=(double)angle.Value,FilletRadius=(double)filletRadius.Value,
            Operation=operation.SelectedIndex==0?"add":"cut",Plane=plane.SelectedIndex==0?"XY":plane.SelectedIndex==1?"XZ":"YZ",
            PlaneSourceId=planeLink.Checked?pendingPlaneSourceId:"",Offset=(double)offset.Value,Depth=(double)depth.Value,Reverse=reverse.Checked,Midplane=midplane.Checked,ThroughAll=throughAll.Checked,Sketch=sketch.Sketch.Copy()};
        if(edit){feature.Id=next.Features[selected].Id;next.Features[selected]=feature;}else next.Features.Add(feature);
        return await Commit(next);
    }
    async Task RemoveFeature()
    {
        if(selected<0){MessageBox.Show(this,"삭제할 피처를 트리에서 선택하세요.");return;}
        var next=document.Copy();next.Features.RemoveAt(selected);await Commit(next);
    }
    async Task Undo(bool forward)
    {
        var from=forward?redo:undo;var to=forward?undo:redo;
        if(from.Count==0||rebuilding)return;
        var next=from.Peek().Copy();
        if(!await Build(next))return;
        EndSketchSession();from.Pop();to.Push(document.Copy());document=next;dirty=true;BuildTree();status.Text=forward?"다시 실행됨":"실행 취소됨";
    }
    void NewProject()
    {
        if(!CanReplace())return;
        EndSketchSession();CancelFacePick();document=new PartDocument();sketchPlanes.Clear();geometry=null;viewport.Geometry=null;viewport.Invalidate();undo.Clear();redo.Clear();
        dirty=false;projectPath=null;report.Clear();BuildTree();sketch.Begin("rectangle");tabs.SelectedIndex=1;RefreshWorkflowGuide();
    }
    async void OpenProject()
    {
        if(!CanReplace())return;
        using var dialog=new OpenFileDialog{Filter="DMI 피처 프로젝트|*.dmipart"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            if(new FileInfo(dialog.FileName).Length>1048576)throw new InvalidDataException("프로젝트 제한은 1 MB입니다.");
            var next=PartDocument.Parse(await File.ReadAllTextAsync(dialog.FileName));
            if(!await Build(next))return;
            EndSketchSession();document=next;projectPath=dialog.FileName;undo.Clear();redo.Clear();dirty=false;BuildTree();tabs.SelectedIndex=1;
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"프로젝트 열기 실패");}
    }
    void SaveProject()
    {
        if(rebuilding)return;
        using var dialog=new SaveFileDialog{Filter="DMI 피처 프로젝트|*.dmipart",FileName=projectPath==null?document.Name+".dmipart":Path.GetFileName(projectPath)};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string scratch=dialog.FileName+"."+Guid.NewGuid()+".tmp";
        try
        {
            File.WriteAllText(scratch,JsonSerializer.Serialize(document.Copy(),PartDocument.Json),Encoding.UTF8);
            File.Move(scratch,dialog.FileName,true);projectPath=dialog.FileName;dirty=false;BuildTree();status.Text="피처 프로젝트 저장 완료. 아직 피처에 적용하지 않은 스케치는 저장되지 않습니다.";
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"저장 실패");}
        finally{if(File.Exists(scratch))File.Delete(scratch);}
    }
    async void Export()
    {
        if(document.Features.Count==0){MessageBox.Show(this,"먼저 솔리드를 만드세요.");return;}
        using var dialog=new SaveFileDialog{Filter="STEP 정밀 형상|*.step|IGES 정밀 형상|*.iges|STL 표시 메시|*.stl",FileName=document.Name};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string source=Path.Combine(temp,Guid.NewGuid()+".dmipart"),output=Path.Combine(Path.GetDirectoryName(dialog.FileName)!,Guid.NewGuid()+Path.GetExtension(dialog.FileName));
        try
        {
            Enabled=false;rebuilding=true;status.Text="정밀 형상 내보내기 중…";
            await File.WriteAllTextAsync(source,JsonSerializer.Serialize(document,PartDocument.Json),Encoding.UTF8);
            await CadFiles.Convert(source,output);File.Move(output,dialog.FileName,true);status.Text="내보내기 완료";
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"내보내기 실패");}
        finally{Enabled=true;rebuilding=false;if(File.Exists(output))File.Delete(output);if(File.Exists(output+".metrics.json"))File.Delete(output+".metrics.json");}
    }
    void Sheet()
    {
        var form=new MachiningSheetWindow();
        if(geometry!=null)form.SetModelReference(document.Name,document.Revision,geometry.Size);
        form.Show(this);
    }
    public async Task<bool> LoadDemo()
    {
        if(!CanReplace())return false;
        var demo=new PartDocument{Name="예제 플레이트"};
        PartSketch Rect(double x,double y,double w,double h)=>new(){DimensionMode="rectangle",Points=new(){new[]{x,y},new[]{x+w,y},new[]{x+w,y+h},new[]{x,y+h}}};
        demo.Features.Add(new PartFeature{Name="기본 플레이트",Depth=10,Sketch=Rect(0,0,100,70)});
        demo.Features.Add(new PartFeature{Name="관통 원 구멍",Operation="cut",ThroughAll=true,Depth=10,Sketch=new PartSketch{Kind="circle",Center=[50,35],Radius=10}});
        demo.Features.Add(new PartFeature{Name="상단 보스",Offset=10,Depth=15,Sketch=Rect(5,5,20,20)});
        await Commit(demo);
        return document.Features.Count==3&&document.Name=="예제 플레이트";
    }
    void Help()=>MessageBox.Show(this,
        "디엠아이 CAD 1.0 개발판 — 시작하기\n\n1. 상단 스케치 탭 → 사각형/원/선 선택.\n2. 왼쪽 속성에서 좌표·치수 입력 → 스케치 치수 적용.\n3. 스케치 종료 또는 ✓ 확인으로 스케치를 유지합니다.\n4. 피처 탭 → 돌출 보스/베이스 또는 돌출 컷.\n5. 왼쪽 깊이·평면·오프셋·방향 설정(컷은 관통 선택 가능) → ✓ 확인.\n6. 피처 트리에서 우클릭 → 피처 정의 편집 / 스케치 편집.\n7. 변경은 확인으로 계산, 취소 또는 Esc로 미적용 입력 취소.\n\nCtrl+Z/Y: 피처 변경 취소/다시 실행. Ctrl+S: 프로젝트 저장.\n3D: 가운데 회전, Ctrl+가운데 이동, 휠 확대.\n면을 클릭한 뒤 사각형/원 도구를 선택하세요. 도구를 바꿔도 선택한 면은 유지됩니다.\n보스는 바깥쪽, 컷은 안쪽 방향으로 시작합니다. 지원하는 돌출 끝 평면은 연결됩니다.\n검사/출력 → 1.0 금속 플레이트 / 회전 부품 예제를 실행하세요.\n피처 → 회전 보스/컷, 전체 모서리 필렛을 사용할 수 있습니다.\n일반 스케치 구속 솔버, 정밀 모서리 측정, 도면·조립·금형·NC는 아직 없습니다.\nSOLIDWORKS 2020 기본 배치를 참고한 독립적인 디엠아이 UI이며 완전한 조작 호환은 아닙니다.",
        "도움말 · F1",MessageBoxButtons.OK,MessageBoxIcon.Information);
}
