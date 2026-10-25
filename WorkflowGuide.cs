namespace DmiCad;
sealed partial class ModelingWindow
{
    readonly System.Windows.Forms.Timer guideTimer=new(){Interval=250};
    readonly Label guideStage=new(){Dock=DockStyle.Top,Height=24,Padding=new Padding(8,3,0,0),Font=new Font("Segoe UI",9,FontStyle.Bold),ForeColor=Color.FromArgb(30,90,140)};
    readonly Label guideTarget=new(){Dock=DockStyle.Top,Height=25,Padding=new Padding(8,3,0,0),AutoEllipsis=true};
    readonly FlowLayoutPanel guideActions=new(){Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,Padding=new Padding(6,1,0,0)};
    readonly Button guideStart=new(){AutoSize=true,Height=29,Text="이 면에서 스케치 시작"};
    readonly Button guideRectangle=new(){AutoSize=true,Height=29,Text="사각형"};
    readonly Button guideCircle=new(){AutoSize=true,Height=29,Text="원"};
    readonly Button guideBoss=new(){AutoSize=true,Height=29,Text="위로 쌓기 · 돌출 보스"};
    readonly Button guideCut=new(){AutoSize=true,Height=29,Text="안으로 파기 · 돌출 컷"};
    readonly Button guideConfirm=new(){AutoSize=true,Height=29,Text="✓ 형상 계산 / 확인"};
    readonly Button guideCancel=new(){AutoSize=true,Height=29,Text="취소"};
    readonly Button guideEdit=new(){AutoSize=true,Height=29,Text="스케치로 돌아가기"};
    readonly List<Button> guidePlanes=new();
    readonly Dictionary<Control,(Label caption,int row)> propertyRows=new();
    TableLayoutPanel? propertyTable;
    Button? propertySketchApply;
    Panel BuildWorkflowGuide()
    {
        var panel=new Panel{Dock=DockStyle.Top,Height=94,BackColor=Color.FromArgb(235,244,251),Padding=new Padding(0,0,0,3)};
        foreach(string name in new[]{"XY","XZ","YZ"})
        {
            var button=new Button{Text=name+" 기준면 선택",AutoSize=true,Height=29};
            button.Click+=(_,_)=>{SelectBasePlaneForCheck(name);RefreshWorkflowGuide();};
            guidePlanes.Add(button);guideActions.Controls.Add(button);
        }
        guideStart.Click+=(_,_)=>GuideStart();
        guideRectangle.Click+=(_,_)=>GuideShape("rectangle");guideCircle.Click+=(_,_)=>GuideShape("circle");
        guideBoss.Click+=(_,_)=>GuideFeature(0);guideCut.Click+=(_,_)=>GuideFeature(1);
        guideConfirm.Click+=async(_,_)=>{await ConfirmProperty();RefreshWorkflowGuide();};
        guideCancel.Click+=(_,_)=>{CancelProperty();CancelFacePick();RefreshWorkflowGuide();};
        guideEdit.Click+=(_,_)=>{OpenSketchProperties();tabs.SelectedIndex=0;RefreshWorkflowGuide();};
        guideActions.Controls.AddRange(new Control[]{guideStart,guideRectangle,guideCircle,guideBoss,guideCut,guideConfirm,guideCancel,guideEdit});
        panel.Controls.Add(guideActions);panel.Controls.Add(guideTarget);panel.Controls.Add(guideStage);
        return panel;
    }
    void StartWorkflowGuide()
    {
        guideTimer.Tick+=(_,_)=>RefreshWorkflowGuide();guideTimer.Start();
        operation.SelectedIndexChanged+=(_,_)=>{
            if(selected<0&&!editingExisting&&(featureName.Text=="돌출 "+(document.Features.Count+1)||featureName.Text=="잘라내기 "+(document.Features.Count+1)))
                featureName.Text=(operation.SelectedIndex==1?"잘라내기 ":"돌출 ")+(document.Features.Count+1);
        };
        RefreshWorkflowGuide();
    }
    bool HasBasePlaneSelection()=>tree.SelectedNode?.Tag is string tag&&tag.StartsWith("plane:");
    void GuideStart()
    {
        if(pickedPlane!=null)Begin("rectangle");
        else if(sketchSession){OpenSketchProperties();tabs.SelectedIndex=0;}
        else if(HasBasePlaneSelection())Begin("rectangle");
        else status.Text="먼저 XY/XZ/YZ 기준면 또는 모델의 평평한 면을 선택하세요.";
        RefreshWorkflowGuide();
    }
    void GuideShape(string kind)
    {
        if(sketchSession||pickedPlane!=null||HasBasePlaneSelection())Begin(kind);
        else status.Text="도형을 그릴 면이 필요합니다. 위 안내에서 기준면을 고르거나 모델 면을 클릭하세요.";
        RefreshWorkflowGuide();
    }
    void GuideFeature(int op)
    {
        if(!sketch.Closed){status.Text="먼저 도형을 닫으세요.";return;}
        bool edit=editingExisting;FinishSketchSession();OpenFeatureProperties(edit,edit?-1:op);RefreshWorkflowGuide();
    }
    void PropertyRow(Control control,bool show)
    {
        if(propertyTable==null||!propertyRows.TryGetValue(control,out var value))return;
        var style=propertyTable.RowStyles[value.row];float height=show?34:0;
        if(style.Height!=height){style.Height=height;control.Visible=value.caption.Visible=show;}
    }
    void RefreshPropertyRows()
    {
        bool drawing=propertyActive&&propertyKind=="sketch";
        bool fillet=featureKind.SelectedIndex==2,revolve=featureKind.SelectedIndex==1;
        PropertyRow(featureKind,!drawing);PropertyRow(featureName,!drawing);PropertyRow(operation,!drawing&&!fillet);
        PropertyRow(plane,!fillet);PropertyRow(offset,!fillet);
        PropertyRow(planeLink,!fillet&&pendingPlaneSourceId.Length>0);PropertyRow(planeLinkInfo,!fillet&&planeLink.Checked);
        PropertyRow(throughAll,!drawing&&!fillet&&!revolve&&operation.SelectedIndex==1);
        PropertyRow(depth,!drawing&&!fillet&&!revolve&&!throughAll.Checked);
        PropertyRow(angle,!drawing&&revolve);PropertyRow(filletRadius,!drawing&&fillet);
        PropertyRow(midplane,!drawing&&!fillet&&!revolve&&!throughAll.Checked);
        PropertyRow(reverse,!drawing&&!fillet&&!midplane.Checked);
        PropertyRow(u,drawing);PropertyRow(v,drawing);
        PropertyRow(width,drawing&&dimensionMode=="rectangle");PropertyRow(height,drawing&&dimensionMode=="rectangle");
        PropertyRow(radius,drawing&&dimensionMode=="circle");PropertyRow(vertex,drawing&&dimensionMode=="polygon");
        PropertyRow(grid,drawing);
        if(propertySketchApply!=null)
        {
            int row=propertyTable!.GetRow(propertySketchApply);
            float desired=drawing?40:0;
            if(propertyTable.RowStyles[row].Height!=desired){propertyTable.RowStyles[row].Height=desired;propertySketchApply.Visible=drawing;}
        }
    }
    void RefreshWorkflowGuide()
    {
        if(!shellReady)return;
        bool feature=propertyActive&&propertyKind=="feature";
        bool drawing=sketchSession&&!feature;
        bool face=pickedPlane!=null;
        bool basis=HasBasePlaneSelection();
        guideStage.Text=feature?"③ 보스 / 컷 · 깊이와 방향을 확인하세요":drawing?"② 2D 스케치 · 이 평면에 도형을 그립니다":face||basis?"① 선택 완료 · 이 면에서 새 스케치를 시작하세요":"① 평면 / 면 선택 → ② 스케치 → ③ 보스 / 컷";
        string target=face?$"선택 면 {pickedPlane!.FaceIndex+1} · {pickedPlane.Plane} · 위치 {pickedPlane.Offset:G9} mm":
            drawing||feature||basis?CurrentPlaneLabel():document.Features.Count==0?"첫 형상: 아래에서 기준면을 고르세요.":"다음 형상: 더 쌓거나 팔 제품 면을 왼쪽 클릭하세요.";
        guideTarget.Text=target+(drawing?(sketch.Closed?" · 닫힌 스케치 완료 → 쌓기 또는 파기":" · 사각형/원 선택 후 화면에서 그리세요"):feature?" · 확인하면 실제 솔리드에 반영됩니다.":"");
        tips.SetToolTip(guideTarget,guideTarget.Text);
        foreach(var b in guidePlanes)b.Visible=!drawing&&!feature&&!face;
        guideStart.Visible=!drawing&&!feature;guideStart.Enabled=face||basis;
        guideStart.Text=face?"이 면에서 스케치 시작":basis?"이 기준면에서 스케치 시작":"먼저 평면 / 면을 선택";
        guideRectangle.Visible=guideCircle.Visible=drawing;
        guideBoss.Visible=drawing&&sketch.Closed;guideCut.Visible=drawing&&sketch.Closed&&!editingExisting;
        guideBoss.Text=editingExisting?"기존 피처 수정":"위로 쌓기 · 돌출 보스";
        guideCut.Enabled=document.Features.Count>0;
        guideConfirm.Visible=feature;guideConfirm.Enabled=featureKind.SelectedIndex==2||sketch.Closed;
        guideCancel.Visible=drawing||feature||face;
        guideEdit.Visible=feature&&featureKind.SelectedIndex!=2;
        guideActions.Enabled=!rebuilding;
        RefreshPropertyRows();
    }
}
