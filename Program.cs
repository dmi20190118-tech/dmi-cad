using System.Drawing.Drawing2D;
using System.Text.Json;

namespace DmiCad;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            try { ImportChecks.Run(); SheetChecks.Run(); Environment.ExitCode = 0; }
            catch (Exception ex) { if (args.Length > 1) File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new CadWindow());
    }
}

public sealed class Model
{
    public decimal Width { get; set; } = 100;
    public decimal Depth { get; set; } = 70;
    public decimal Height { get; set; } = 35;
    public bool Visible { get; set; } = true;
}

sealed class CadWindow : Form
{
    readonly Viewport view = new();
    readonly TreeView tree = new();
    readonly ToolStripStatusLabel status = new("준비");
    readonly NumericUpDown width = Number(100), depth = Number(70), height = Number(35);
    readonly TabControl ribbon = new();
    readonly Label info = new() { AutoSize = false, Height = 62, Dock = DockStyle.Bottom,
        Text = "인터페이스 시제품 0.1\n정밀 CAD / CAM 엔진 미포함", Padding = new Padding(12), ForeColor = Color.DimGray };
    Model model = new();
    Geometry? imported;
    bool loading;
    public CadWindow()
    {
        Text = "DMI CAD — 부품1 | 인터페이스 미리보기";
        Width = 1440; Height = 920; MinimumSize = new Size(980, 640);
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(242, 244, 247);
        var menu = new MenuStrip { BackColor = Color.FromArgb(36, 45, 61), ForeColor = Color.White };
        var file = new ToolStripMenuItem("파일");
        file.DropDownItems.Add("새 부품", null, (_, _) => NewPart());
        file.DropDownItems.Add("열기...", null, (_, _) => Open());
        file.DropDownItems.Add("저장...", null, (_, _) => Save());
        file.DropDownItems.Add("변환 / 내보내기...", null, (_, _) => Export());
        file.DropDownItems.Add("종료", null, (_, _) => Close());
        menu.Items.Add(file);
        var v = new ToolStripMenuItem("보기");
        v.DropDownItems.Add("등각 보기", null, (_, _) => view.SetView("iso"));
        v.DropDownItems.Add("정면 보기", null, (_, _) => view.SetView("front"));
        v.DropDownItems.Add("평면 보기", null, (_, _) => view.SetView("top"));
        menu.Items.Add(v);
        var sheetMenu = new ToolStripMenuItem("가공 지시서");
        sheetMenu.Click += (_, _) => new MachiningSheetWindow().Show(this);
        menu.Items.Add(sheetMenu);
        var help = new ToolStripMenuItem("도움말");
        help.Click += (_, _) => MessageBox.Show(this,
            "DMI CAD 인터페이스 시제품\n\n가운데 버튼 드래그: 회전\nCtrl + 가운데 버튼: 화면 이동\nShift + 가운데 버튼: 확대/축소\n휠: 확대/축소 (아래로: 확대)\n가운데 버튼 더블클릭: 화면 맞춤\n치수 입력: 예제 직육면체 변경\n\n독립적인 시제품이며 SOLIDWORKS 제품이 아닙니다.\n선/곡면 편집 및 정밀 모델링은 아직 지원하지 않습니다.",
            "DMI CAD");
        menu.Items.Add(help);
        MainMenuStrip = menu;
        ribbon.Dock = DockStyle.Top; ribbon.Height = 118;
        AddRibbon("피처", ("▱", "예제 돌출", Extrude), ("↻", "회전", () => Notice("회전 피처")),
            ("⌒", "필렛", () => Notice("필렛")), ("◩", "모따기", () => Notice("모따기")),
            ("⊞", "패턴", () => Notice("패턴")));
        AddRibbon("스케치", ("✎", "스케치 모드", Sketch), ("╱", "선", () => Notice("선 편집")),
            ("□", "사각형", () => Notice("사각형 스케치")), ("○", "원", () => Notice("원 스케치")),
            ("↔", "치수", () => width.Focus()));
        AddRibbon("곡면", ("▧", "곡면 생성", () => Notice("곡면 모델링")),
            ("⌁", "스윕", () => Notice("스윕")), ("▱", "로프트", () => Notice("로프트")));
        AddRibbon("표시", ("◆", "등각", () => view.SetView("iso")), ("▣", "정면", () => view.SetView("front")),
            ("⊞", "평면", () => view.SetView("top")), ("◇", "와이어프레임", () => { view.Wire = !view.Wire; view.Invalidate(); }),
            ("⊙", "화면 맞춤", () => view.Fit()));
        var bottom = new StatusStrip();
        bottom.Items.Add(status);
        bottom.Items.Add(new ToolStripStatusLabel { Spring = true });
        bottom.Items.Add(new ToolStripStatusLabel("MMGS · mm   |   인터페이스 시제품"));
        var split = new SplitContainer { Size = new Size(1200, 600), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 260, Panel1MinSize = 230, Panel2MinSize = 400, BackColor = Color.White };
        tree.Dock = DockStyle.Fill; tree.BorderStyle = BorderStyle.None;
        tree.HideSelection = false; tree.ItemHeight = 27;
        tree.AfterSelect += (_, e) => { status.Text = e.Node?.Text ?? "준비"; };
        var properties = new Panel { Height = 190, Dock = DockStyle.Bottom, Padding = new Padding(12) };
        var title = new Label { Text = "예제 입체 치수 (mm)", Dock = DockStyle.Top, Height = 28, Font = new Font(Font, FontStyle.Bold) };
        properties.Controls.Add(title);
        var grid = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 130, ColumnCount = 2, RowCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        string[] labels = ["너비 X", "깊이 Y", "높이 Z"];
        NumericUpDown[] numbers = [width, depth, height];
        for (int i = 0; i < 3; i++)
        {
            grid.Controls.Add(new Label { Text = labels[i], AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, 0, i);
            numbers[i].Dock = DockStyle.Fill; grid.Controls.Add(numbers[i], 1, i);
            numbers[i].ValueChanged += (_, _) => UpdateDimensions();
        }
        properties.Controls.Add(grid);
        var heading = new Label { Text = "  모델 트리", Height = 34, Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.FromArgb(231, 235, 241) };
        split.Panel1.Controls.Add(tree); split.Panel1.Controls.Add(properties);
        split.Panel1.Controls.Add(info); split.Panel1.Controls.Add(heading);
        split.Panel2.Controls.Add(view);
        var viewbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
        foreach (var name in new[] { "등각", "정면", "평면", "화면 맞춤" })
        {
            string n = name;
            var b = new Button { Text = n, AutoSize = true, FlatStyle = FlatStyle.Flat, Height = 29 };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => { if (n == "화면 맞춤") view.Fit(); else view.SetView(n == "등각" ? "iso" : n == "정면" ? "front" : "top"); };
            viewbar.Controls.Add(b);
        }
        split.Panel2.Controls.Add(viewbar);
        Controls.Add(split); Controls.Add(ribbon); Controls.Add(menu); Controls.Add(bottom);
        view.Model = model; RefreshTree();
    }
    static NumericUpDown Number(decimal value) => new() { Minimum = 1, Maximum = 10000, DecimalPlaces = 1, Value = value };
    void AddRibbon(string name, params (string icon, string label, Action action)[] items)
    {
        var page = new TabPage(name) { BackColor = Color.FromArgb(247, 248, 250) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        foreach (var item in items)
        {
            var b = new Button { Text = item.icon + "\n" + item.label, Width = 105, Height = 67,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(4) };
            b.FlatAppearance.BorderColor = Color.FromArgb(217, 222, 230);
            b.Click += (_, _) => item.action();
            flow.Controls.Add(b);
        }
        page.Controls.Add(flow); ribbon.TabPages.Add(page);
    }
    void Notice(string feature) => MessageBox.Show(this, feature + " 기능은 이 인터페이스 시제품에 아직 구현되지 않았습니다.", "개발 예정");
    void Sketch() { view.Sketch = !view.Sketch; view.SetView(view.Sketch ? "top" : "iso"); status.Text = view.Sketch ? "평면 미리보기 · 스케치 편집은 개발 예정" : "3D 보기"; }
    void Extrude() { imported = null; view.Geometry = null; width.Enabled = depth.Enabled = height.Enabled = true; model.Visible = true; view.Sketch = false; view.SetView("iso"); RefreshTree(); status.Text = "예제 직육면체 표시 · 왼쪽 치수로 크기 변경"; }
    void UpdateDimensions()
    {
        if (loading) return;
        model.Width = width.Value; model.Depth = depth.Value; model.Height = height.Value;
        view.Invalidate(); status.Text = "예제 입체 치수 변경";
    }
    void RefreshTree()
    {
        tree.Nodes.Clear(); var root = tree.Nodes.Add("▣ " + (imported?.Name ?? "부품1"));
        if (imported != null) { root.Nodes.Add($"삼각형: {imported.Triangles.Count:N0}"); root.Nodes.Add($"선 요소: {imported.Lines.Count:N0}"); root.Nodes.Add($"크기: {imported.Size.X:G5} × {imported.Size.Y:G5} × {imported.Size.Z:G5} (파일 좌표)"); root.Expand(); view.Invalidate(); return; }
        root.Nodes.Add("재질 <지정되지 않음>"); root.Nodes.Add("정면 기준면");
        root.Nodes.Add("평면 기준면"); root.Nodes.Add("우측 기준면"); root.Nodes.Add("원점");
        if (model.Visible) root.Nodes.Add("예제 돌출 1");
        root.Expand(); view.Invalidate();
    }
    void NewPart() { imported = null; view.Geometry = null; width.Enabled = depth.Enabled = height.Enabled = true; model = new Model { Visible = false }; Sync(); status.Text = "새 부품 · 예제 돌출을 눌러 입체 표시"; }
    void Sync()
    {
        loading = true; width.Value = model.Width; depth.Value = model.Depth; height.Value = model.Height;
        loading = false; view.Model = model; view.Sketch = false; RefreshTree(); view.Fit();
    }
    void Save()
    {
        if (imported != null) { Export(); return; }
        using var dialog = new SaveFileDialog { Filter = "DMI CAD 프로젝트 (*.dmicad)|*.dmicad", FileName = "부품1.dmicad" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(model)); status.Text = "저장됨"; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "저장 실패"); }
    }
    async void Open()
    {
        using var dialog = new OpenFileDialog { Filter = "지원 CAD 파일|*.step;*.stp;*.iges;*.igs;*.stl;*.dxf;*.dmicad|STEP|*.step;*.stp|IGES|*.iges;*.igs|STL|*.stl|기본 ASCII DXF|*.dxf|DMI 프로젝트|*.dmicad|모든 파일|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (!Path.GetExtension(dialog.FileName).Equals(".dmicad", StringComparison.OrdinalIgnoreCase))
            {
                Enabled = false; status.Text = "파일을 읽고 있습니다…"; Update();
                var geometry = await CadFiles.Read(dialog.FileName);
                imported = geometry; view.Geometry = geometry; view.Sketch = false;
                width.Enabled = depth.Enabled = height.Enabled = false;
                Text = "DMI CAD — " + geometry.Name;
                info.Text = "가져온 형상 보기 · 편집 불가\n크기는 파일 좌표 기준 (STL/DXF 단위 확인 필요)";
                RefreshTree(); view.SetView(geometry.Triangles.Count == 0 ? "top" : "iso"); view.Fit();
                status.Text = "불러오기 완료 · " + geometry.Name;
                return;
            }
            if (new FileInfo(dialog.FileName).Length > 65536) throw new Exception("프로젝트 파일이 너무 큽니다.");
            var m = JsonSerializer.Deserialize<Model>(File.ReadAllText(dialog.FileName)) ?? throw new Exception("잘못된 파일입니다.");
            if (m.Width < 1 || m.Width > 10000 || m.Depth < 1 || m.Depth > 10000 || m.Height < 1 || m.Height > 10000)
                throw new Exception("치수 범위는 1~10000 mm입니다.");
            imported = null; view.Geometry = null; width.Enabled = depth.Enabled = height.Enabled = true; model = m; Sync(); status.Text = "불러오기 완료";
        }
        catch (Exception ex) { status.Text = "불러오기 실패"; MessageBox.Show(this, ex.Message, "열기 실패"); }
        finally { Enabled = true; }
    }
    async void Export()
    {
        var geometry = imported;
        if (geometry == null) { MessageBox.Show(this, "먼저 STEP, IGES 또는 STL 파일을 열어 주세요."); return; }
        bool cad = Path.GetExtension(geometry.Source).ToLowerInvariant() is ".step" or ".stp" or ".iges" or ".igs";
        using var dialog = new SaveFileDialog { Filter = cad ? "STL 메시|*.stl|STEP 형상|*.step|IGES 형상|*.iges" : "STL 메시|*.stl", FileName = Path.GetFileNameWithoutExtension(geometry.Name) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string temp = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, Guid.NewGuid() + Path.GetExtension(dialog.FileName));
        try
        {
            Enabled = false; status.Text = "변환 중…";
            if (cad) await CadFiles.Convert(geometry.Source, temp);
            else await Task.Run(() => CadFiles.WriteStl(geometry, temp));
            File.Move(temp, dialog.FileName, true); status.Text = "내보내기 완료";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "변환 실패"); }
        finally { Enabled = true; if (File.Exists(temp)) File.Delete(temp); }
    }
}

sealed class Viewport : Control
{
    public Model Model { get; set; } = new();
    public Geometry? Geometry { get; set; }
    public bool Wire { get; set; }
    public bool Sketch { get; set; }
    double yaw = -.65, pitch = .5;
    float zoom = 1;
    PointF pan;
    Point previous;
    bool dragging;
    public Viewport()
    {
        Dock = DockStyle.Fill; DoubleBuffered = true; ResizeRedraw = true;
        MouseDown += (_, e) => { if (e.Button != MouseButtons.Middle) return; previous = e.Location; dragging = true; Capture = true; Focus(); };
        MouseUp += (_, e) => { if (e.Button != MouseButtons.Middle) return; dragging = false; Capture = false; };
        MouseCaptureChanged += (_, _) => { if (!Capture) dragging = false; };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            int dx = e.X - previous.X, dy = e.Y - previous.Y;
            if ((ModifierKeys & Keys.Control) != 0)
                pan = new PointF(pan.X + dx, pan.Y + dy);
            else if ((ModifierKeys & Keys.Shift) != 0)
                zoom = Math.Clamp(zoom * (float)Math.Exp(-dy * .01), .15f, 8f);
            else
            {
                yaw += dx * .01;
                pitch = Math.Clamp(pitch + dy * .01, -Math.PI / 2, Math.PI / 2);
            }
            previous = e.Location;
            Invalidate();
        };
        // SOLIDWORKS default wheel direction: toward user zooms in.
        MouseWheel += (_, e) => ZoomAt(e.Location, (float)Math.Pow(1.12, -e.Delta / 120.0));
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Middle) Fit(); };
    }
    void ZoomAt(Point anchor, float factor)
    {
        float next = Math.Clamp(zoom * factor, .15f, 8f);
        float ratio = next / zoom;
        // Keep the model point under the pointer fixed, including at zoom limits.
        float ax = anchor.X - Width / 2f, ay = anchor.Y - Height / 2f;
        pan = new PointF(ax - (ax - pan.X) * ratio, ay - (ay - pan.Y) * ratio);
        zoom = next;
        Invalidate();
    }
    public void Fit() { zoom = 1; pan = PointF.Empty; Invalidate(); }
    public void SetView(string name)
    {
        (yaw, pitch) = name switch { "front" => (0.0, 0.0), "top" => (0.0, 1.5707), _ => (-.65, .5) };
        Invalidate();
    }
    (double x, double y, double z) Rotate(double x, double y, double z)
    {
        double a = x * Math.Cos(yaw) + y * Math.Sin(yaw);
        double b = -x * Math.Sin(yaw) + y * Math.Cos(yaw);
        return (a, b * Math.Sin(pitch) + z * Math.Cos(pitch), b * Math.Cos(pitch) - z * Math.Sin(pitch));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 2 || Height < 2) return;
        using var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(222, 231, 242), Color.FromArgb(250, 251, 253), 90);
        g.FillRectangle(bg, ClientRectangle);
        using var grid = new Pen(Color.FromArgb(40, 110, 130, 155));
        for (int x = Width / 2 % 30; x < Width; x += 30) g.DrawLine(grid, x, 0, x, Height);
        for (int y = Height / 2 % 30; y < Height; y += 30) g.DrawLine(grid, 0, y, Width, y);
        double w = (double)Model.Width, d = (double)Model.Depth, h = Sketch ? 0 : (double)Model.Height;
        float scale = (float)(Math.Min(Width, Height) * .46 / Math.Max(w, Math.Max(d, Math.Max(h, 1)))) * zoom;
        var points = new[] { (-w/2,-d/2,-h/2), (w/2,-d/2,-h/2), (w/2,d/2,-h/2), (-w/2,d/2,-h/2),
            (-w/2,-d/2,h/2), (w/2,-d/2,h/2), (w/2,d/2,h/2), (-w/2,d/2,h/2) };
        var rotated = points.Select(p => Rotate(p.Item1,p.Item2,p.Item3)).ToArray();
        var projected = rotated.Select(p => new PointF(Width/2f + pan.X + (float)p.x*scale, Height/2f + pan.Y - (float)p.y*scale)).ToArray();
        int[][] faces = [[0,1,2,3],[4,7,6,5],[0,4,5,1],[1,5,6,2],[2,6,7,3],[3,7,4,0]];
        Color[] colors = [Color.FromArgb(103,139,172),Color.FromArgb(184,208,228),Color.FromArgb(130,168,199),
            Color.FromArgb(158,188,212),Color.FromArgb(113,152,184),Color.FromArgb(147,179,205)];
        if (Geometry == null && Model.Visible)
        {
            foreach (int i in Enumerable.Range(0,6).OrderByDescending(i => faces[i].Average(n => rotated[n].z)))
            {
                var poly = faces[i].Select(n => projected[n]).ToArray();
                if (!Wire && !Sketch) { using var fill = new SolidBrush(colors[i]); g.FillPolygon(fill,poly); }
                using var edge = new Pen(Color.FromArgb(49,77,103),1.4f); g.DrawPolygon(edge,poly);
            }
        }
        if (Geometry is Geometry geometry)
        {
            float importedScale = Math.Min(Width, Height) * .65f / geometry.Extent * zoom;
            PointF Project(System.Numerics.Vector3 point)
            {
                var q = point - geometry.Center; var p = Rotate(q.X, q.Y, q.Z);
                return new PointF(Width/2f + pan.X + (float)p.x*importedScale, Height/2f + pan.Y - (float)p.y*importedScale);
            }
            using var edge = new Pen(Color.FromArgb(49,77,103), .7f);
            var ordered = geometry.Triangles.Select(t => new { Vertices = t, Depth = t.Average(p => { var q = p - geometry.Center; return Rotate(q.X,q.Y,q.Z).z; }) }).OrderByDescending(t => t.Depth);
            foreach (var triangle in ordered)
            {
                var polygon = triangle.Vertices.Select(Project).ToArray();
                if (!Wire)
                {
                    var normal = System.Numerics.Vector3.Cross(triangle.Vertices[1]-triangle.Vertices[0],triangle.Vertices[2]-triangle.Vertices[0]);
                    if (normal.LengthSquared() > 0) normal = System.Numerics.Vector3.Normalize(normal);
                    var n = Rotate(normal.X,normal.Y,normal.Z);
                    int shade = (int)(130 + 90 * Math.Abs(n.z));
                    using var fill = new SolidBrush(Color.FromArgb(shade-30,shade-10,Math.Min(255,shade+15)));
                    g.FillPolygon(fill,polygon);
                }
                else g.DrawPolygon(edge,polygon);
            }
            foreach (var line in geometry.Lines) if (line.Length >= 2) g.DrawLines(edge,line.Select(Project).ToArray());
        }
        using var text = new SolidBrush(Color.FromArgb(76,91,110));
        g.DrawString(Geometry?.Name ?? (Sketch ? "평면 미리보기" : "부품1 · 3D 작업 화면"), Font, text, 18,18);
        g.DrawString("가운데: 회전  |  Ctrl+가운데: 이동  |  휠: 확대/축소", Font, text, 18, Height - 32);
        float ox = Width - 76, oy = Height - 76;
        var axes = new[] { (1.0,0.0,0.0,"X",Color.Firebrick),(0.0,1.0,0.0,"Y",Color.ForestGreen),(0.0,0.0,1.0,"Z",Color.RoyalBlue) };
        foreach (var a in axes)
        {
            var p = Rotate(a.Item1,a.Item2,a.Item3);
            float x = ox+(float)p.x*42, y = oy-(float)p.y*42;
            using var pen = new Pen(a.Item5,2); g.DrawLine(pen,ox,oy,x,y);
            using var brush = new SolidBrush(a.Item5); g.DrawString(a.Item4,Font,brush,x,y);
        }
    }
}
