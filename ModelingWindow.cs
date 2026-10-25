using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace DmiCad;

sealed class ModelingWindow : Form
{
    readonly SketchEditor sketch=new();
    readonly Viewport viewport=new(){Model=new Model{Visible=false}};
    readonly TreeView tree=new(){Dock=DockStyle.Fill,HideSelection=false};
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    readonly TextBox report=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
    readonly ToolStripStatusLabel status=new("스케치를 그리거나 치수로 정의하세요.");
    readonly NumericUpDown u=N(0,-1000000,1000000),v=N(0,-1000000,1000000),width=N(100,.001m,10000),
        height=N(70,.001m,10000),radius=N(5,.001m,10000),depth=N(10,.001m,10000),
        offset=N(0,-1000000,1000000),vertex=N(1,1,500),grid=N(1,.001m,1000);
    readonly TextBox featureName=new(){Text="돌출 1",MaxLength=200,Dock=DockStyle.Fill};
    readonly ComboBox plane=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly ComboBox operation=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly Stack<PartDocument> undo=new(),redo=new();
    readonly string temp=Path.Combine(Path.GetTempPath(),"DMI-CAD-"+Guid.NewGuid().ToString("N"));
    PartDocument document=new();
    Geometry? geometry;
    string? projectPath;
    string dimensionMode="rectangle";
    bool rebuilding,dirty,sync;
    int selected=-1;
    public ModelingWindow()
    {
        Directory.CreateDirectory(temp);
        Text="디엠아이(DMI) CAD 0.3 — 정밀 스케치 / 솔리드";
        Width=1500;Height=960;MinimumSize=new Size(1120,760);Font=new Font("Segoe UI",9);
        KeyPreview=true;
        var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=106,Padding=new Padding(4),WrapContents=true,BackColor=Color.FromArgb(232,238,243)};
        var logo=Brand.CreateLogo();
        var picture=new PictureBox{Image=logo,SizeMode=PictureBoxSizeMode.Zoom,Width=106,Height=44,BackColor=Color.White};
        picture.Disposed+=(_,_)=>logo.Dispose();bar.Controls.Add(picture);
        void B(string text,Action action,string tip)
        {
            var button=new Button {Text=text,Height=42,AutoSize=true,MinimumSize=new Size(82,42),Margin=new Padding(3),FlatStyle=FlatStyle.Flat};
            button.Click+=(_,_)=>action();bar.Controls.Add(button);
            tips.SetToolTip(button,tip);
        }
        B("새 프로젝트",NewProject,"새로운 mm 단위 피처 프로젝트. 기존 변경은 먼저 저장하세요.");
        B("열기",OpenProject,".dmipart 프로젝트를 다시 계산해 엽니다. STEP 등은 파일 뷰어에서 엽니다.");
        B("저장",SaveProject,"스케치 좌표와 피처 이력을 저장합니다. 임시 스케치는 피처에 추가한 뒤 저장하세요.");
        B("내보내기",Export,"현재 정확한 솔리드를 STEP / IGES / STL로 출력합니다.");
        B("파일 뷰어",()=>new CadWindow().Show(this),"기존 STEP / IGES / STL / DXF 확인·변환 창을 엽니다.");
        B("가공 지시서",Sheet,"현재 모델 이름·리비전·외형 크기를 지시서 초안에 전달합니다.");
        B("도움말",Help,"작업 순서, 좌표계와 기능 제한을 설명합니다.");
        B("예제 만들기",async()=>await LoadDemo(),"스케치 돌출 → 원 구멍 → 추가 돌출을 실제 엔진으로 계산합니다.");
        B("사각형",()=>Begin("rectangle"),"2D 화면에서 대각 두 점을 찍거나 U/V/너비/높이를 입력해 치수 적용.");
        B("원",()=>Begin("circle"),"중심과 둘레를 찍거나 U/V/반지름을 입력해 치수 적용.");
        B("선 / 다각형",()=>Begin("polygon"),"점을 차례대로 찍고 첫 점 또는 스케치 닫기를 누릅니다.");
        B("스케치 닫기",()=>Try(()=>sketch.Finish()),"자기교차하지 않는 다각형을 만드세요.");
        B("치수 적용",ApplyDimensions,"현재 스케치 치수 또는 선택한 꼭짓점 좌표를 적용합니다.");
        B("돌출 추가",async()=>{operation.SelectedIndex=0;await ApplyFeature(false);},"스케치를 평면 법선 방향으로 돌출해 재료를 추가합니다.");
        B("잘라내기",async()=>{operation.SelectedIndex=1;await ApplyFeature(false);},"스케치 돌출 영역을 기존 솔리드에서 뺍니다. 첫 피처로 사용할 수 없습니다.");
        B("피처 수정",async()=>await ApplyFeature(true),"트리에서 선택한 피처의 스케치·평면·깊이를 수정하고 이후 피처를 재계산합니다.");
        B("피처 삭제",async()=>await RemoveFeature(),"선택 피처를 삭제하고 재계산합니다. 실패하면 기존 모델을 유지합니다.");
        B("실행 취소",async()=>await Undo(false),"Ctrl+Z · 직전 피처 변경으로 되돌립니다.");
        B("다시 실행",async()=>await Undo(true),"Ctrl+Y · 취소한 피처 변경을 다시 적용합니다.");
        var split=new SplitContainer{Size=new Size(1400,750),Dock=DockStyle.Fill,SplitterDistance=330,Panel1MinSize=310,Panel2MinSize=500,FixedPanel=FixedPanel.Panel1};
        var props=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=468,ColumnCount=2,Padding=new Padding(8),AutoScroll=true};
        props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        plane.Items.AddRange(new object[]{"XY → +Z","XZ → −Y","YZ → +X"});plane.SelectedIndex=0;
        operation.Items.AddRange(new object[]{"재료 추가","재료 제거"});operation.SelectedIndex=0;
        void Row(string label,Control control)
        {
            int row=props.RowCount++;props.RowStyles.Add(new RowStyle(SizeType.Absolute,31));
            props.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            control.Dock=DockStyle.Fill;props.Controls.Add(control,1,row);
        }
        Row("피처 이름",featureName);Row("작업",operation);Row("스케치 평면 / 방향",plane);
        Row("평면 오프셋 (mm)",offset);Row("돌출 깊이 (mm)",depth);
        Row("U / 점 X (mm)",u);Row("V / 점 Y (mm)",v);Row("사각형 너비 (mm)",width);Row("사각형 높이 (mm)",height);
        Row("원 반지름 (mm)",radius);Row("다각형 꼭짓점 번호",vertex);Row("스냅 간격 (mm)",grid);
        var instructions=new Label{Text="치수 입력 → 치수 적용 → 돌출/잘라내기\n기존 피처: 트리 선택 → 수정 → 피처 수정",Dock=DockStyle.Fill,AutoSize=false,ForeColor=Color.DarkSlateGray};
        props.Controls.Add(instructions,0,props.RowCount);props.SetColumnSpan(instructions,2);
        props.RowStyles.Add(new RowStyle(SizeType.Absolute,48));props.RowCount++;
        split.Panel1.Controls.Add(tree);split.Panel1.Controls.Add(props);
        var sketchPage=new TabPage("2D 스케치 (U/V)");sketchPage.Controls.Add(sketch);
        var solidPage=new TabPage("3D 솔리드");
        var views=new FlowLayoutPanel{Dock=DockStyle.Top,Height=38};
        foreach(string name in new[]{"등각","정면","평면","화면 맞춤","와이어프레임"})
        {
            string current=name;var button=new Button{Text=name,AutoSize=true,Height=30};
            button.Click+=(_,_)=>{if(current=="화면 맞춤")viewport.Fit();else if(current=="와이어프레임"){viewport.Wire=!viewport.Wire;viewport.Invalidate();}else viewport.SetView(current=="등각"?"iso":current=="정면"?"front":"top");};
            views.Controls.Add(button);
        }
        solidPage.Controls.Add(viewport);solidPage.Controls.Add(views);
        var detailsPage=new TabPage("형상 정보 / 검증");detailsPage.Controls.Add(report);
        tabs.TabPages.AddRange(new[]{sketchPage,solidPage,detailsPage});split.Panel2.Controls.Add(tabs);
        var bottom=new StatusStrip();status.Spring=true;status.TextAlign=ContentAlignment.MiddleLeft;bottom.Items.Add(status);bottom.Items.Add("mm · 단일 솔리드 · 피처 프로젝트 v1");
        Controls.Add(split);Controls.Add(bar);Controls.Add(bottom);
        sketch.Changed+=(_,_)=>ShowSketchDimensions();
        grid.ValueChanged+=(_,_)=>sketch.Grid=(double)grid.Value;
        vertex.ValueChanged+=(_,_)=>{if(sync||sketch.Sketch.Kind=="circle"||!sketch.Closed)return;int i=(int)vertex.Value-1;if(i<sketch.Sketch.Points.Count){u.Value=(decimal)sketch.Sketch.Points[i][0];v.Value=(decimal)sketch.Sketch.Points[i][1];}};
        tree.AfterSelect+=(_,e)=>{if(e.Node?.Tag is int i)SelectFeature(i);};
        KeyDown+=async(_,e)=>{if(e.Control&&e.KeyCode==Keys.Z){e.SuppressKeyPress=true;await Undo(false);}else if(e.Control&&e.KeyCode==Keys.Y){e.SuppressKeyPress=true;await Undo(true);}else if(e.Control&&e.KeyCode==Keys.S){e.SuppressKeyPress=true;SaveProject();}else if(e.KeyCode==Keys.F1){e.SuppressKeyPress=true;Help();}};
        FormClosing+=(_,e)=>{if(rebuilding){e.Cancel=true;return;}if(dirty&&MessageBox.Show(this,"저장하지 않은 모델 변경을 닫을까요?","변경 확인",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
        FormClosed+=(_,_)=>{tips.Dispose();try{Directory.Delete(temp,true);}catch{}};
        BuildTree();Begin("rectangle");
    }
    readonly ToolTip tips=new(){AutoPopDelay=15000};
    static NumericUpDown N(decimal value,decimal min,decimal max)=>new(){Value=value,Minimum=min,Maximum=max,DecimalPlaces=3,Increment=1,ThousandsSeparator=false};
    void Try(Action action){try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"확인 필요");}}
    bool CanReplace()=>!dirty||MessageBox.Show(this,"저장하지 않은 모델 변경을 버리고 진행할까요?","변경 확인",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
    void Begin(string mode)
    {
        dimensionMode=mode;sketch.Begin(mode);tabs.SelectedIndex=0;
        featureName.Text=(operation.SelectedIndex==1?"잘라내기 ":"돌출 ")+(document.Features.Count+1);
        status.Text="스케치를 그리거나 좌표·치수를 입력하고 '치수 적용'을 누르세요.";
    }
    void ShowSketchDimensions()
    {
        if(!sketch.Closed)return;sync=true;
        var s=sketch.Sketch;
        if(s.Kind=="circle"){u.Value=(decimal)s.Center[0];v.Value=(decimal)s.Center[1];radius.Value=(decimal)Math.Clamp(s.Radius,.001,10000);dimensionMode="circle";}
        else if(s.Points.Count>0)
        {
            double minU=s.Points.Min(p=>p[0]),minV=s.Points.Min(p=>p[1]),w=s.Points.Max(p=>p[0])-minU,h=s.Points.Max(p=>p[1])-minV;
            u.Value=(decimal)minU;v.Value=(decimal)minV;width.Value=(decimal)Math.Clamp(w,.001,10000);height.Value=(decimal)Math.Clamp(h,.001,10000);
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
        selected=index;var f=document.Features[index];
        featureName.Text=f.Name;plane.SelectedIndex=f.Plane=="XY"?0:f.Plane=="XZ"?1:2;operation.SelectedIndex=f.Operation=="add"?0:1;
        offset.Value=(decimal)f.Offset;depth.Value=(decimal)f.Depth;dimensionMode=f.Sketch.Kind=="circle"?"circle":f.Sketch.DimensionMode;
        sketch.SetSketch(f.Sketch);status.Text=$"선택: {f.Name}. 수정 후 '피처 수정'으로 전체 재계산.";
    }
    void BuildTree()
    {
        tree.Nodes.Clear();var root=tree.Nodes.Add(document.Name+" · rev "+document.Revision);
        root.Nodes.Add("단위 mm / XY·XZ·YZ 기준면");
        for(int i=0;i<document.Features.Count;i++){var f=document.Features[i];var node=root.Nodes.Add($"{i+1}. {f.Name} [{f.Plane} / {f.Depth:G6} mm]");node.Tag=i;}
        root.Expand();selected=-1;Text=$"디엠아이(DMI) CAD 0.3 — {document.Name}"+(dirty?" *":"");
    }
    async Task<bool> Build(PartDocument next)
    {
        if(rebuilding)return false;
        try
        {
            next=next.Copy();rebuilding=true;Enabled=false;status.Text="정밀 솔리드 재계산 중…";UseWaitCursor=true;
            if(next.Features.Count==0){geometry=null;viewport.Geometry=null;viewport.Invalidate();report.Text="비어 있는 프로젝트";return true;}
            string source=Path.Combine(temp,Guid.NewGuid()+".dmipart");
            string output=Path.ChangeExtension(source,".stl");
            await File.WriteAllTextAsync(source,JsonSerializer.Serialize(next,PartDocument.Json),Encoding.UTF8);
            await CadFiles.Convert(source,output);
            var mesh=await Task.Run(()=>CadFiles.Stl(output));mesh.Name=next.Name;mesh.Source=source;
            using var metrics=JsonDocument.Parse(await File.ReadAllTextAsync(output+".metrics.json"));
            double volume=metrics.RootElement.GetProperty("volumeMm3").GetDouble();
            geometry=mesh;viewport.Geometry=mesh;viewport.Fit();
            report.Text=$"Open CASCADE 정밀 형상 검증 통과\r\n유효한 단일 솔리드\r\n단위: mm\r\n정확한 부피: {volume:G12} mm³\r\n피처: {next.Features.Count}\r\n표시 삼각형: {mesh.Triangles.Count:N0}\r\n표시 메시 외형: {mesh.Size.X:G7} × {mesh.Size.Y:G7} × {mesh.Size.Z:G7} mm\r\n\r\n외형은 표시 메시의 범위이며 정밀 면/모서리 측정 도구가 아닙니다.\r\n프로젝트는 스케치와 피처 이력을 저장하고 재계산합니다.\r\n스케치 구속 솔버, 필렛, 임의 면 선택, 도면·조립·금형·CAM/NC는 아직 구현되지 않았습니다.";
            return true;
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message+"\n기존 모델은 변경하지 않았습니다.","모델 재계산 실패");return false;}
        finally{rebuilding=false;Enabled=true;UseWaitCursor=false;status.Text="준비";}
    }
    async Task Commit(PartDocument next)
    {
        next.Revision=document.Revision+1;
        if(!await Build(next))return;
        undo.Push(document.Copy());redo.Clear();document=next;dirty=true;BuildTree();tabs.SelectedIndex=1;status.Text="모델 재계산 완료 · 프로젝트를 저장하세요.";
    }
    async Task ApplyFeature(bool edit)
    {
        if(!sketch.Closed){MessageBox.Show(this,"닫힌 스케치를 먼저 그리거나 치수를 적용하세요.");return;}
        if(edit&&(selected<0||selected>=document.Features.Count)){MessageBox.Show(this,"트리에서 수정할 피처를 선택하세요.");return;}
        var next=document.Copy();
        var feature=new PartFeature{Name=string.IsNullOrWhiteSpace(featureName.Text)?"피처 "+(next.Features.Count+1):featureName.Text,
            Operation=operation.SelectedIndex==0?"add":"cut",Plane=plane.SelectedIndex==0?"XY":plane.SelectedIndex==1?"XZ":"YZ",
            Offset=(double)offset.Value,Depth=(double)depth.Value,Sketch=sketch.Sketch.Copy()};
        if(edit){feature.Id=next.Features[selected].Id;next.Features[selected]=feature;}else next.Features.Add(feature);
        await Commit(next);
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
        from.Pop();to.Push(document.Copy());document=next;dirty=true;BuildTree();status.Text=forward?"다시 실행됨":"실행 취소됨";
    }
    void NewProject()
    {
        if(!CanReplace())return;
        document=new PartDocument();geometry=null;viewport.Geometry=null;viewport.Invalidate();undo.Clear();redo.Clear();
        dirty=false;projectPath=null;report.Clear();BuildTree();Begin("rectangle");
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
            document=next;projectPath=dialog.FileName;undo.Clear();redo.Clear();dirty=false;BuildTree();tabs.SelectedIndex=1;
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
        demo.Features.Add(new PartFeature{Name="관통 원 구멍",Operation="cut",Depth=10,Sketch=new PartSketch{Kind="circle",Center=[50,35],Radius=10}});
        demo.Features.Add(new PartFeature{Name="상단 보스",Offset=10,Depth=15,Sketch=Rect(5,5,20,20)});
        await Commit(demo);
        return document.Features.Count==3&&document.Name=="예제 플레이트";
    }
    void Help()=>MessageBox.Show(this,
        "디엠아이 CAD 0.3 — 시작하기\n\n1. 사각형/원/선 선택 → 2D에서 점을 찍습니다.\n2. 숫자로 만들려면 U/V와 너비/높이 또는 반지름 입력 → 치수 적용.\n3. 평면·오프셋·깊이 설정 → 돌출 추가 또는 잘라내기.\n4. 트리에서 피처 선택 → 스케치/치수 변경 → 치수 적용 → 피처 수정.\n5. Ctrl+Z/Y: 실행 취소/다시 실행. 프로젝트 저장은 .dmipart, 형상 출력은 STEP.\n\n평면: XY는 +Z, XZ는 −Y, YZ는 +X 방향으로 돌출합니다.\n2D: Ctrl+가운데 이동, 휠 확대. 3D: 가운데 회전, Ctrl+가운데 이동.\n사각형/원의 숫자는 직접 치수를 정의합니다. 일반 스케치 구속 솔버는 아직 없습니다.\n피처에 적용하지 않은 임시 스케치는 저장되지 않습니다.\n절삭/금형/해석/정밀 선택·측정 기능은 현재 제공되지 않습니다.",
        "도움말 · F1",MessageBoxButtons.OK,MessageBoxIcon.Information);
}
