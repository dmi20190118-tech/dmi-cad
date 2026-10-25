using System.Text.Json;
using System.Drawing.Drawing2D;
namespace DmiCad;

sealed class CadCommandButton : Button
{
    public CadCommandButton(string label,string icon,Action action)
    {
        Text=label;Width=82;Height=72;Image=CadGlyph.Create(icon,28);TextImageRelation=TextImageRelation.ImageAboveText;
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;FlatAppearance.MouseOverBackColor=Color.FromArgb(219,235,248);
        BackColor=Color.FromArgb(245,246,248);Margin=new Padding(2,0,2,0);Padding=new Padding(2);
        Click+=(_,_)=>action();AccessibleName=label;UseMnemonic=false;
        Disposed+=(_,_)=>Image?.Dispose();
    }
}
static class CadGlyph
{
    public static Bitmap Create(string kind,int size)
    {
        var bitmap=new Bitmap(size,size);
        using var g=Graphics.FromImage(bitmap);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/32f,size/32f);
        using var blue=new Pen(Color.FromArgb(44,104,161),2);
        using var green=new Pen(Color.FromArgb(43,143,91),2);
        using var orange=new Pen(Color.FromArgb(219,143,37),2);
        using var blueFill=new SolidBrush(Color.FromArgb(125,174,213));
        using var lightFill=new SolidBrush(Color.FromArgb(215,231,243));
        if(kind is "extrude" or "cut" or "part" or "view")
        {
            PointF[] top=[new(5,12),new(18,5),new(28,11),new(15,18)];
            PointF[] left=[new(5,12),new(15,18),new(15,29),new(5,23)];
            PointF[] right=[new(15,18),new(28,11),new(28,22),new(15,29)];
            g.FillPolygon(lightFill,top);g.FillPolygon(blueFill,left);g.FillPolygon(blueFill,right);
            g.DrawPolygon(blue,top);g.DrawPolygon(blue,left);g.DrawPolygon(blue,right);
            if(kind=="cut"){g.DrawEllipse(orange,11,10,9,6);g.DrawLine(orange,15,10,15,21);}
            if(kind=="extrude"){g.DrawLine(orange,23,15,23,1);g.DrawLine(orange,23,1,19,5);g.DrawLine(orange,23,1,27,5);}
        }
        else if(kind=="circle"){g.DrawEllipse(blue,5,5,22,22);g.DrawLine(green,13,16,19,16);g.DrawLine(green,16,13,16,19);}
        else if(kind=="rectangle"){g.DrawRectangle(blue,4,7,24,18);g.FillRectangle(Brushes.DarkGreen,2,5,4,4);g.FillRectangle(Brushes.DarkGreen,26,23,4,4);}
        else if(kind=="line"){g.DrawLines(blue,new PointF[]{new(3,26),new(11,9),new(21,17),new(29,4)});foreach(var p in new PointF[]{new(3,26),new(11,9),new(21,17),new(29,4)})g.FillRectangle(Brushes.DarkGreen,p.X-2,p.Y-2,4,4);}
        else if(kind=="dimension"){g.DrawLine(blue,4,8,4,26);g.DrawLine(blue,28,8,28,26);g.DrawLine(orange,4,17,28,17);g.DrawLine(orange,4,17,9,13);g.DrawLine(orange,4,17,9,21);g.DrawLine(orange,28,17,23,13);g.DrawLine(orange,28,17,23,21);}
        else if(kind=="save"){g.FillRectangle(lightFill,5,3,22,26);g.DrawRectangle(blue,5,3,22,26);g.DrawRectangle(blue,10,4,12,8);g.DrawRectangle(blue,10,19,12,9);}
        else if(kind=="open"){g.DrawPolygon(orange,new Point[]{new(3,9),new(13,9),new(16,5),new(28,5),new(28,26),new(3,26)});g.DrawLine(blue,4,14,29,14);}
        else if(kind=="undo"||kind=="redo"){g.DrawArc(blue,6,8,21,20,190,220);g.DrawLine(orange,5,9,5,18);g.DrawLine(orange,5,18,13,15);}
        else if(kind=="delete"){g.DrawLine(blue,9,9,11,28);g.DrawLine(blue,23,9,21,28);g.DrawLine(blue,11,28,21,28);g.DrawLine(orange,6,7,26,7);g.DrawLine(blue,12,4,20,4);}
        else if(kind=="check"){g.DrawLines(green,new Point[]{new(5,17),new(13,25),new(28,6)});}
        else if(kind=="cancel"){g.DrawLine(orange,7,7,25,25);g.DrawLine(orange,25,7,7,25);}
        else if(kind=="document"){g.FillRectangle(lightFill,7,3,19,26);g.DrawRectangle(blue,7,3,19,26);for(int y=9;y<=23;y+=5)g.DrawLine(blue,11,y,22,y);}
        else{g.DrawRectangle(blue,5,5,22,22);g.DrawLine(orange,9,23,24,8);}
        return bitmap;
    }
}
sealed partial class ModelingWindow
{
    readonly TabControl managerTabs=new(){Dock=DockStyle.Fill};
    readonly TabControl commandTabs=new(){Dock=DockStyle.Top,Height=117,Alignment=TabAlignment.Bottom};
    readonly TextBox treeFilter=new(){Dock=DockStyle.Top,PlaceholderText="피처 트리 필터",BorderStyle=BorderStyle.FixedSingle};
    readonly Label propertyHeading=new(){Dock=DockStyle.Top,Height=34,Padding=new Padding(9,7,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
    readonly Label propertyHint=new(){Dock=DockStyle.Top,Height=68,Padding=new Padding(9),ForeColor=Color.DimGray};
    readonly Panel propertyContainer=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.FromArgb(248,249,250)};
    bool propertyActive,editingExisting,shellReady;
    string propertyKind="sketch";
    void BuildShell()
    {
        BackColor=Color.FromArgb(237,240,244);
        var menu=new MenuStrip{Dock=DockStyle.Top,Height=47,AutoSize=false,BackColor=Color.FromArgb(241,243,245),Padding=new Padding(5,3,4,3)};
        var logoImage=Brand.CreateLogo();
        var logoBox=new PictureBox{Image=logoImage,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,Width=88,Height=38};
        logoBox.Disposed+=(_,_)=>logoImage.Dispose();
        menu.Items.Add(new ToolStripControlHost(logoBox){AutoSize=false,Width=92,Height=40});
        ToolStripMenuItem Menu(string label){var m=new ToolStripMenuItem(label);menu.Items.Add(m);return m;}
        void Item(ToolStripMenuItem parent,string text,Action action,Keys shortcut=Keys.None){var item=new ToolStripMenuItem(text);item.Click+=(_,_)=>action();item.ShortcutKeys=shortcut;parent.DropDownItems.Add(item);}
        var file=Menu("파일");Item(file,"새 부품",NewProject,Keys.Control|Keys.N);Item(file,"프로젝트 열기",OpenProject,Keys.Control|Keys.O);Item(file,"저장",SaveProject);
        Item(file,"내보내기",Export);file.DropDownItems.Add(new ToolStripSeparator());Item(file,"파일 뷰어",()=>new CadWindow().Show(this));Item(file,"종료",Close);
        var edit=Menu("편집");Item(edit,"실행 취소",async()=>await Undo(false));Item(edit,"다시 실행",async()=>await Undo(true));
        Item(edit,"피처 정의 편집",()=>OpenFeatureProperties(true));Item(edit,"피처 삭제",async()=>await RemoveFeature());
        var view=Menu("보기");Item(view,"3D 모델",()=>tabs.SelectedIndex=1);Item(view,"2D 스케치",()=>tabs.SelectedIndex=0);Item(view,"형상 정보",()=>tabs.SelectedIndex=2);
        Item(view,"화면 맞춤",()=>{if(tabs.SelectedIndex==0)sketch.Fit();else viewport.Fit();});
        var insert=Menu("삽입");Item(insert,"돌출 보스 / 베이스",()=>OpenFeatureProperties(false,0));Item(insert,"돌출 컷",()=>OpenFeatureProperties(false,1));
        var tools=Menu("도구");Item(tools,"가공 지시서",Sheet);Item(tools,"예제 부품 만들기",async()=>await LoadDemo());
        var workspace=Menu("작업 영역");Item(workspace,"부품 모델링",()=>tabs.SelectedIndex=1);
        foreach(string planned in new[]{"도면 — 개발 예정","어셈블리 — 개발 예정","금형 — 개발 예정","CAM / NC — 개발 예정"})workspace.DropDownItems.Add(new ToolStripMenuItem(planned){Enabled=false});
        Item(Menu("도움말"),"시작하기 · F1",Help);
        MainMenuStrip=menu;
        var quick=new ToolStrip{Dock=DockStyle.Top,GripStyle=ToolStripGripStyle.Hidden,BackColor=Color.FromArgb(248,249,250),Height=31,ImageScalingSize=new Size(19,19),Padding=new Padding(8,0,3,0)};
        void Q(string label,string icon,Action action)
        {
            var image=CadGlyph.Create(icon,20);
            var b=new ToolStripButton(label,image){DisplayStyle=ToolStripItemDisplayStyle.Image,ToolTipText=label};b.Click+=(_,_)=>action();quick.Items.Add(b);
            b.Disposed+=(_,_)=>image.Dispose();
        }
        Q("새 부품","document",NewProject);Q("열기","open",OpenProject);Q("저장 · Ctrl+S","save",SaveProject);
        quick.Items.Add(new ToolStripSeparator());Q("실행 취소 · Ctrl+Z","undo",async()=>await Undo(false));Q("다시 실행 · Ctrl+Y","redo",async()=>await Undo(true));
        quick.Items.Add(new ToolStripSeparator());Q("형상 검증 정보","check",()=>tabs.SelectedIndex=2);
        quick.Items.Add(new ToolStripLabel("  디엠아이 CAD · 부품 설계"){ForeColor=Color.FromArgb(85,105,120)});
        FlowLayoutPanel Page(string title)
        {
            var page=new TabPage(title){BackColor=Color.FromArgb(245,246,248),Padding=new Padding(5)};
            var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,BackColor=page.BackColor};
            page.Controls.Add(flow);commandTabs.TabPages.Add(page);return flow;
        }
        void C(FlowLayoutPanel flow,string label,string icon,Action action,string tip,bool enabled=true)
        {
            var b=new CadCommandButton(label,icon,action){Enabled=enabled};tips.SetToolTip(b,tip);flow.Controls.Add(b);
        }
        void Separator(FlowLayoutPanel flow)=>flow.Controls.Add(new Panel{Width=1,Height=66,Margin=new Padding(9,3,9,3),BackColor=Color.FromArgb(202,209,217)});
        var features=Page("피처");
        C(features,"스케치","line",()=>Begin("rectangle"),"기준 평면에서 스케치를 시작합니다.");
        Separator(features);
        C(features,"돌출 보스\n/ 베이스","extrude",()=>OpenFeatureProperties(false,0),"닫힌 스케치로 재료를 추가합니다. 왼쪽 속성에서 깊이 설정 후 확인.");
        C(features,"돌출 컷","cut",()=>OpenFeatureProperties(false,1),"닫힌 스케치를 돌출해 기존 솔리드에서 재료를 제거합니다.");
        Separator(features);
        C(features,"피처 편집","dimension",()=>OpenFeatureProperties(true),"트리에서 피처 선택 후 속성을 편집합니다.");
        C(features,"삭제","delete",async()=>await RemoveFeature(),"선택 피처를 삭제하고 전체 재계산합니다.");
        Separator(features);
        C(features,"필렛\n(개발 예정)","view",()=>{},"현재 미지원 기능입니다.",false);
        C(features,"모따기\n(개발 예정)","view",()=>{},"현재 미지원 기능입니다.",false);
        var sketches=Page("스케치");
        C(sketches,"면에서\n스케치","part",ChooseSketchPlane,"현재 솔리드의 정밀 평면 면 목록에서 선택해 원/사각형 스케치를 시작합니다. 위치는 고정값으로 복사됩니다.");
        C(sketches,"스케치 종료","check",FinishSketchSession,"닫힌 스케치를 유지하고 모델 화면으로 돌아갑니다.");
        Separator(sketches);
        C(sketches,"선 / 다각형","line",()=>Begin("polygon"),"점을 찍고 첫 점 또는 닫기를 눌러 폐곡선을 만듭니다.");
        C(sketches,"사각형","rectangle",()=>Begin("rectangle"),"대각 두 점을 찍거나 왼쪽에서 좌표·너비·높이를 입력합니다.");
        C(sketches,"원","circle",()=>Begin("circle"),"중심과 둘레를 찍거나 중심·반지름으로 정의합니다.");
        Separator(sketches);
        C(sketches,"치수 입력","dimension",()=>{OpenSketchProperties();u.Focus();},"왼쪽 속성에서 좌표와 치수를 입력합니다. 일반 스마트 치수/구속 솔버는 미지원입니다.");
        C(sketches,"치수 적용","check",ApplyDimensions,"입력값을 현재 2D 스케치에 적용합니다.");
        C(sketches,"다각형 닫기","line",()=>Try(()=>sketch.Finish()),"선으로 점 세 개 이상을 찍은 뒤 폐곡선을 완성합니다.");
        var inspection=Page("검사 / 출력");
        C(inspection,"형상 정보","check",()=>tabs.SelectedIndex=2,"유효 솔리드, 실제 부피와 표시 외형 범위를 확인합니다.");
        C(inspection,"내보내기","document",Export,"정밀 STEP/IGES 또는 메시 STL 출력.");
        C(inspection,"가공 지시서","document",Sheet,"현재 모델 참조를 지시서 초안에 전달합니다.");
        Separator(inspection);
        C(inspection,"예제 부품","part",async()=>await LoadDemo(),"3개 피처의 실제 솔리드를 계산합니다.");
        C(inspection,"파일 뷰어","open",()=>new CadWindow().Show(this),"기존 교환 형식 파일을 확인합니다.");
        C(inspection,"도움말","document",Help,"현재 기능과 사용 순서, 제한을 확인합니다.");

        var split=new SplitContainer{Size=new Size(1440,720),Dock=DockStyle.Fill,SplitterDistance=277,Panel1MinSize=250,Panel2MinSize=500,FixedPanel=FixedPanel.Panel1,SplitterWidth=4,BackColor=Color.FromArgb(192,202,212)};
        var treePage=new TabPage("피처 트리"){BackColor=Color.White,Padding=new Padding(4)};
        tree.BorderStyle=BorderStyle.None;tree.ItemHeight=24;tree.FullRowSelect=true;
        treePage.Controls.Add(tree);treePage.Controls.Add(treeFilter);
        treeFilter.TextChanged+=(_,_)=>BuildTree();
        var properties=new TabPage("속성"){BackColor=propertyContainer.BackColor};
        var confirmation=new FlowLayoutPanel{Dock=DockStyle.Top,Height=38,Padding=new Padding(5),BackColor=Color.FromArgb(237,241,245)};
        var accept=new Button{Text="✓ 확인",ForeColor=Color.DarkGreen,Width=86,Height=29,FlatStyle=FlatStyle.Flat};
        accept.Click+=async(_,_)=>await ConfirmProperty();
        var cancel=new Button{Text="✕ 취소",ForeColor=Color.Firebrick,Width=86,Height=29,FlatStyle=FlatStyle.Flat};
        cancel.Click+=(_,_)=>CancelProperty();
        confirmation.Controls.Add(accept);confirmation.Controls.Add(cancel);
        tips.SetToolTip(accept,"현재 스케치 또는 피처 변경을 확인합니다.");tips.SetToolTip(cancel,"아직 적용하지 않은 입력을 버리고 모델로 돌아갑니다.");
        var props=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8)};
        props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        void Row(string label,Control control)
        {
            int row=props.RowCount++;props.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            props.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            control.Dock=DockStyle.Fill;props.Controls.Add(control,1,row);
        }
        plane.Items.AddRange(new object[]{"XY → +Z","XZ → −Y","YZ → +X"});plane.SelectedIndex=0;
        operation.Items.AddRange(new object[]{"재료 추가","재료 제거"});operation.SelectedIndex=0;
        Row("피처 이름",featureName);Row("작업",operation);Row("스케치 평면",plane);Row("평면 오프셋 mm",offset);
        Row("컷 종료 조건",throughAll);Row("전체 깊이 mm",depth);Row("돌출 방식",midplane);Row("돌출 방향",reverse);
        void RefreshExtrusionMode()
        {
            if(operation.SelectedIndex!=1)throughAll.Checked=false;
            if(throughAll.Checked)midplane.Checked=false;
            throughAll.Enabled=operation.SelectedIndex==1;
            midplane.Enabled=!throughAll.Checked;depth.Enabled=!throughAll.Checked;
            reverse.Enabled=!midplane.Checked;
        }
        midplane.CheckedChanged+=(_,_)=>RefreshExtrusionMode();
        throughAll.CheckedChanged+=(_,_)=>RefreshExtrusionMode();
        operation.SelectedIndexChanged+=(_,_)=>RefreshExtrusionMode();
        RefreshExtrusionMode();
        tips.SetToolTip(throughAll,"기존 솔리드의 범위에서 선택 방향의 관통 길이를 자동 계산합니다. 깊이 입력은 무시됩니다.");
        tips.SetToolTip(midplane,"전체 깊이의 절반씩 기준면 양쪽으로 생성합니다. 중간 평면에서는 방향 반전이 적용되지 않습니다.");
        tips.SetToolTip(reverse,"체크하면 기준면의 법선 반대 방향으로 돌출/잘라내기합니다. 깊이는 양수 mm입니다.");Row("U / 점 X mm",u);Row("V / 점 Y mm",v);Row("사각형 너비 mm",width);
        Row("사각형 높이 mm",height);Row("원 반지름 mm",radius);Row("꼭짓점 번호",vertex);Row("스냅 간격 mm",grid);
        var apply=new Button{Text="스케치 치수 적용",Height=34,Dock=DockStyle.Fill};apply.Click+=(_,_)=>ApplyDimensions();
        int buttonRow=props.RowCount++;props.RowStyles.Add(new RowStyle(SizeType.Absolute,40));props.Controls.Add(apply,0,buttonRow);props.SetColumnSpan(apply,2);
        propertyContainer.Controls.Add(props);
        properties.Controls.Add(propertyContainer);properties.Controls.Add(propertyHint);properties.Controls.Add(confirmation);properties.Controls.Add(propertyHeading);
        managerTabs.TabPages.AddRange(new[]{treePage,properties});split.Panel1.Controls.Add(managerTabs);
        tabs.Alignment=TabAlignment.Bottom;
        var sketchPage=new TabPage("스케치"){BackColor=Color.White};sketchPage.Controls.Add(sketch);
        var solidPage=new TabPage("모델"){BackColor=Color.White};solidPage.Controls.Add(viewport);
        var views=new FlowLayoutPanel{Height=33,AutoSize=true,WrapContents=false,BackColor=Color.FromArgb(245,247,249),BorderStyle=BorderStyle.FixedSingle};
        foreach(string name in new[]{"등각","정면","평면","화면 맞춤","음영 / 선"})
        {
            string current=name;var button=new Button{Text=name,AutoSize=true,Height=26,FlatStyle=FlatStyle.Flat,Margin=new Padding(1),Padding=new Padding(2),Font=new Font("Segoe UI",8.5f)};
            button.FlatAppearance.BorderSize=0;button.Click+=(_,_)=>{if(current=="화면 맞춤")viewport.Fit();else if(current=="음영 / 선"){viewport.Wire=!viewport.Wire;viewport.Invalidate();}else viewport.SetView(current=="등각"?"iso":current=="정면"?"front":"top");};
            views.Controls.Add(button);
        }
        viewport.Controls.Add(views);
        void CenterViews(){views.Location=new Point(Math.Max(6,(viewport.Width-views.Width)/2),7);views.BringToFront();}
        viewport.Resize+=(_,_)=>CenterViews();views.SizeChanged+=(_,_)=>CenterViews();
        var infoPage=new TabPage("형상 정보");infoPage.Controls.Add(report);report.BackColor=Color.White;report.BorderStyle=BorderStyle.None;
        tabs.TabPages.AddRange(new[]{sketchPage,solidPage,infoPage});
        var taskButtons=new FlowLayoutPanel{Dock=DockStyle.Right,Width=39,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.FromArgb(233,238,243),Padding=new Padding(2,8,2,0)};
        foreach(var item in new[]{("도움","시작하기",new Action(Help)),("파일","파일 뷰어",new Action(()=>new CadWindow().Show(this))),("시트","가공 지시서",new Action(Sheet))})
        {
            var b=new Button{Text=item.Item1,Width=32,Height=39,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,0,4),Font=new Font("Segoe UI",8)};
            b.FlatAppearance.BorderSize=0;b.Click+=(_,_)=>item.Item3();tips.SetToolTip(b,item.Item2);taskButtons.Controls.Add(b);
        }
        split.Panel2.Controls.Add(tabs);split.Panel2.Controls.Add(taskButtons);
        var bottom=new StatusStrip{BackColor=Color.FromArgb(240,243,246)};
        status.Spring=true;status.TextAlign=ContentAlignment.MiddleLeft;bottom.Items.Add(status);bottom.Items.Add("MMGS · mm | 단일 솔리드 | 피처 프로젝트 v3");
        var context=new ContextMenuStrip();
        var editFeature=new ToolStripMenuItem("피처 정의 편집");editFeature.Click+=(_,_)=>OpenFeatureProperties(true);
        var editSketch=new ToolStripMenuItem("스케치 편집");editSketch.Click+=(_,_)=>{if(selected>=0){editingExisting=true;OpenSketchProperties();tabs.SelectedIndex=0;}};
        var delete=new ToolStripMenuItem("피처 삭제");delete.Click+=async(_,_)=>await RemoveFeature();
        context.Items.AddRange(new ToolStripItem[]{editFeature,editSketch,delete});
        context.Opening+=(_,_)=>editFeature.Enabled=editSketch.Enabled=delete.Enabled=selected>=0;
        tree.ContextMenuStrip=context;tree.NodeMouseClick+=(_,e)=>{if(e.Button==MouseButtons.Right)tree.SelectedNode=e.Node;};
        tree.NodeMouseDoubleClick+=(_,e)=>{if(e.Node.Tag is int)OpenFeatureProperties(true);};
        Controls.Add(split);Controls.Add(commandTabs);Controls.Add(quick);Controls.Add(menu);Controls.Add(bottom);
        shellReady=true;
    }
    void OpenSketchProperties()
    {
        propertyKind="sketch";propertyActive=true;propertyHeading.Text=editingExisting?"스케치 편집":"스케치";
        propertyHint.Text="좌표·치수를 입력한 뒤 '스케치 치수 적용'.\n원/사각형을 그리거나 다각형을 닫으세요.\n확인은 스케치를 유지하고 돌아갑니다.";
        managerTabs.SelectedIndex=1;commandTabs.SelectedIndex=1;
    }
    void OpenFeatureProperties(bool edit,int op=-1)
    {
        if(edit&&(selected<0||selected>=document.Features.Count)){MessageBox.Show(this,"피처 트리에서 편집할 피처를 선택하세요.");return;}
        editingExisting=edit;if(op>=0)operation.SelectedIndex=op;
        propertyKind="feature";propertyActive=true;
        propertyHeading.Text=edit?"피처 정의 편집":operation.SelectedIndex==1?"돌출 컷":"돌출 보스 / 베이스";
        propertyHint.Text="평면·종료 조건·방향을 확인하세요.\n✓ 확인: 계산 / ✕ 취소: 입력 취소.\n치수 수정 후 치수 적용을 누르세요.";
        managerTabs.SelectedIndex=1;commandTabs.SelectedIndex=0;
        if(!sketch.Closed){tabs.SelectedIndex=0;status.Text="먼저 스케치 탭에서 닫힌 스케치를 만드세요.";}
    }
    async Task ConfirmProperty()
    {
        if(!propertyActive)return;
        if(propertyKind=="sketch"&&!editingExisting){FinishSketchSession();return;}
        if(await ApplyFeature(editingExisting)){propertyActive=false;editingExisting=false;managerTabs.SelectedIndex=0;tabs.SelectedIndex=1;}
    }
    void FinishSketchSession()
    {
        if(!sketch.Closed){MessageBox.Show(this,"아직 닫힌 스케치가 없습니다. 치수를 적용하거나 다각형을 닫으세요.");return;}
        if(editingExisting){OpenFeatureProperties(true);return;}
        propertyActive=false;managerTabs.SelectedIndex=0;commandTabs.SelectedIndex=0;tabs.SelectedIndex=1;
        status.Text="닫힌 스케치 준비됨. 피처 탭에서 돌출 보스 또는 돌출 컷을 선택하세요.";
    }
    void CancelProperty()
    {
        if(!propertyActive)return;
        if(selected>=0&&selected<document.Features.Count)SelectFeature(selected);else sketch.Begin(dimensionMode);
        propertyActive=false;editingExisting=false;managerTabs.SelectedIndex=0;tabs.SelectedIndex=document.Features.Count>0?1:0;
        status.Text="미적용 입력을 취소했습니다. 저장된 모델은 변경하지 않았습니다.";
    }
    public async Task<bool> RunUiWorkflowChecks()
    {
        int before=document.Features.Count;
        Begin("rectangle");u.Value=70;v.Value=10;width.Value=10;height.Value=10;ApplyDimensions();FinishSketchSession();
        if(!sketch.Closed||managerTabs.SelectedIndex!=0||commandTabs.TabPages.Count!=3)throw new Exception("Sketch completion or command tabs failed.");
        offset.Value=0;depth.Value=10;reverse.Checked=true;midplane.Checked=true;OpenFeatureProperties(false,0);
        if(reverse.Enabled)throw new Exception("Midplane did not disable irrelevant direction control.");
        if(managerTabs.SelectedIndex!=1)throw new Exception("Property manager did not activate.");
        await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));Directory.CreateDirectory("preview");preview.Save(Path.Combine("preview","property-manager-0.3.5.png"),System.Drawing.Imaging.ImageFormat.Png);}
        await ConfirmProperty();
        if(document.Features.Count!=before+1||!document.Features[^1].Reverse||!document.Copy().Features[^1].Reverse||!document.Copy().Features[^1].Midplane||managerTabs.SelectedIndex!=0)throw new Exception("Property confirmation failed.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==0);string snapshot=JsonSerializer.Serialize(document,PartDocument.Json);
        OpenFeatureProperties(true);depth.Value=20;reverse.Checked=true;midplane.Checked=true;CancelProperty();
        if(midplane.Checked||!reverse.Enabled)throw new Exception("Cancel did not restore midplane control.");
        if(reverse.Checked)throw new Exception("Cancel did not restore extrusion direction.");
        if(JsonSerializer.Serialize(document,PartDocument.Json)!=snapshot)throw new Exception("Cancel changed the committed model.");
        await Undo(false);if(document.Features.Count!=before)throw new Exception("UI undo failed.");
        await Undo(true);if(!document.Features[^1].Midplane)throw new Exception("Redo lost midplane.");
        if(document.Features.Count!=before+1)throw new Exception("UI redo failed.");
        await Undo(false);
        // Exercise an actual through-all edit, source thickness change and disk reopen.
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==1);
        OpenFeatureProperties(true);throughAll.Checked=true;offset.Value=0;reverse.Checked=false;
        if(depth.Enabled||midplane.Enabled)throw new Exception("Through-all left incompatible inputs enabled.");
        await Task.Delay(100);Update();
        using(var preview=new Bitmap(Width,Height)){DrawToBitmap(preview,new Rectangle(0,0,Width,Height));preview.Save(Path.Combine("preview","property-manager-0.3.5.png"),System.Drawing.Imaging.ImageFormat.Png);}
        await ConfirmProperty();
        if(!document.Features[1].ThroughAll)throw new Exception("Through-all edit was not committed.");
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==0);
        OpenFeatureProperties(true);depth.Value=15;await ConfirmProperty();
        string saved=Path.Combine(temp,"workflow-reopen.dmipart");
        await File.WriteAllTextAsync(saved,JsonSerializer.Serialize(document.Copy(),PartDocument.Json));
        var reopened=PartDocument.Parse(await File.ReadAllTextAsync(saved));
        if(!reopened.Features[1].ThroughAll||reopened.Features[1].Reverse||reopened.Features[0].Depth!=15||reopened.Version!=3)
            throw new Exception("Disk reopen lost geometry settings.");
        if(!await Build(reopened))throw new Exception("Reopened model regeneration failed.");
        await Undo(false);await Undo(false);
        treeFilter.Text="관통";
        if(tree.Nodes[0].Nodes.Cast<TreeNode>().Count(n=>n.Tag is int)!=1)throw new Exception("Feature filter failed.");
        treeFilter.Text="";
        tree.SelectedNode=tree.Nodes[0].Nodes.Cast<TreeNode>().First(n=>n.Tag is int i&&i==0);
        if(selected!=0)throw new Exception("Feature selection did not activate target.");
        tree.SelectedNode=tree.Nodes[0];
        if(selected!=-1)throw new Exception("Root selection retained a stale feature target.");
        await CheckFaceSketchWorkflow();
        managerTabs.SelectedIndex=0;tabs.SelectedIndex=1;return true;
    }
}
