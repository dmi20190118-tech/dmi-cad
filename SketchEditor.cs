using System.Drawing.Drawing2D;
namespace DmiCad;

sealed class SketchEditor : Control
{
    public PartSketch Sketch { get; private set; } = new();
    public bool Closed { get; private set; }
    public string Mode { get; private set; } = "rectangle";
    public double Grid { get; set; } = 1;
    public event EventHandler? Changed;
    readonly List<double[]> clicks = new();
    float scale=4;
    PointF pan;
    bool moving;
    Point previous;
    public SketchEditor()
    {
        DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;BackColor=Color.White;
        MouseDown+=Down;MouseMove+=Move;
        MouseUp+=(_,_)=>{moving=false;Capture=false;};
        MouseCaptureChanged+=(_,_)=>{if(!Capture)moving=false;};
        MouseWheel+=(_,e)=>{scale=Math.Clamp(scale*(e.Delta<0?1.12f:.89f),.05f,100);Invalidate();};
    }
    public void Begin(string mode)
    {
        Mode=mode;Sketch=new PartSketch();Closed=false;clicks.Clear();Invalidate();Changed?.Invoke(this,EventArgs.Empty);
    }
    public void SetSketch(PartSketch s)
    {
        Sketch=s.Copy();Closed=true;clicks.Clear();Mode=s.Kind=="circle"?"circle":"polygon";Fit();Changed?.Invoke(this,EventArgs.Empty);
    }
    public void Finish()
    {
        if(Mode!="polygon"||clicks.Count<3)throw new InvalidDataException("선으로 점을 세 개 이상 찍고 닫기를 누르세요.");
        Sketch=new PartSketch {Points=clicks.Select(p=>p.ToArray()).ToList()};Closed=true;clicks.Clear();Invalidate();Changed?.Invoke(this,EventArgs.Empty);
    }
    public void Fit()
    {
        var pts=Sketch.Kind=="circle"?new[]{new[]{Sketch.Center[0]-Sketch.Radius,Sketch.Center[1]-Sketch.Radius},new[]{Sketch.Center[0]+Sketch.Radius,Sketch.Center[1]+Sketch.Radius}}:Sketch.Points.ToArray();
        if(pts.Length==0){scale=4;pan=PointF.Empty;Invalidate();return;}
        double minX=pts.Min(p=>p[0]),maxX=pts.Max(p=>p[0]),minY=pts.Min(p=>p[1]),maxY=pts.Max(p=>p[1]);
        scale=(float)Math.Clamp(Math.Min(Math.Max(Width,300)/(Math.Max(maxX-minX,1)*1.4),Math.Max(Height,300)/(Math.Max(maxY-minY,1)*1.4)),.05,100);
        pan=new PointF((float)(-(minX+maxX)/2*scale),(float)((minY+maxY)/2*scale));Invalidate();
    }
    double[] World(Point p)
    {
        double u=(p.X-Width/2f-pan.X)/scale,v=-(p.Y-Height/2f-pan.Y)/scale;
        return [Math.Round(u/Grid)*Grid,Math.Round(v/Grid)*Grid];
    }
    PointF Screen(double[] p)=>new(Width/2f+pan.X+(float)p[0]*scale,Height/2f+pan.Y-(float)p[1]*scale);
    void Down(object? sender,MouseEventArgs e)
    {
        Focus();
        if(e.Button==MouseButtons.Middle&&(ModifierKeys&Keys.Control)!=0){moving=true;previous=e.Location;Capture=true;return;}
        if(e.Button!=MouseButtons.Left||Closed)return;
        double[] p=World(e.Location);
        if(p.Any(v=>Math.Abs(v)>1000000))return;
        if(Mode=="polygon"&&clicks.Count>=3)
        {
            var start=Screen(clicks[0]);
            if(Math.Abs(start.X-e.X)<10&&Math.Abs(start.Y-e.Y)<10){Finish();return;}
        }
        if(clicks.Count>0&&clicks[^1].SequenceEqual(p))return;
        clicks.Add(p);
        if(Mode=="rectangle"&&clicks.Count==2)
        {
            var a=clicks[0];var b=clicks[1];
            if(a[0]==b[0]||a[1]==b[1]){clicks.RemoveAt(1);return;}
            Sketch=new PartSketch {DimensionMode="rectangle",Points=new(){new[]{Math.Min(a[0],b[0]),Math.Min(a[1],b[1])},new[]{Math.Max(a[0],b[0]),Math.Min(a[1],b[1])},new[]{Math.Max(a[0],b[0]),Math.Max(a[1],b[1])},new[]{Math.Min(a[0],b[0]),Math.Max(a[1],b[1])}}};
            Closed=true;clicks.Clear();
        }
        else if(Mode=="circle"&&clicks.Count==2)
        {
            double r=Math.Sqrt(Math.Pow(clicks[1][0]-clicks[0][0],2)+Math.Pow(clicks[1][1]-clicks[0][1],2));
            if(r<1e-6){clicks.RemoveAt(1);return;}
            Sketch=new PartSketch {Kind="circle",Center=clicks[0].ToArray(),Radius=r};Closed=true;clicks.Clear();
        }
        else if(clicks.Count>500){clicks.RemoveAt(clicks.Count-1);}
        Invalidate();Changed?.Invoke(this,EventArgs.Empty);
    }
    void Move(object? sender,MouseEventArgs e)
    {
        if(moving){pan=new PointF(pan.X+e.X-previous.X,pan.Y+e.Y-previous.Y);previous=e.Location;Invalidate();}
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        float grid=(float)Grid*scale;while(grid<16)grid*=5;while(grid>100)grid/=2;
        using var gridPen=new Pen(Color.FromArgb(230,235,240));
        float cx=Width/2f+pan.X,cy=Height/2f+pan.Y;
        for(float x=(cx%grid+grid)%grid;x<Width;x+=grid)g.DrawLine(gridPen,x,0,x,Height);
        for(float y=(cy%grid+grid)%grid;y<Height;y+=grid)g.DrawLine(gridPen,0,y,Width,y);
        using var axis=new Pen(Color.FromArgb(135,155,170));g.DrawLine(axis,0,cy,Width,cy);g.DrawLine(axis,cx,0,cx,Height);
        using var line=new Pen(Color.FromArgb(24,105,150),2);
        if(Closed&&Sketch.Kind=="circle")
        {
            var p=Screen(Sketch.Center);float r=(float)Sketch.Radius*scale;
            g.DrawEllipse(line,p.X-r,p.Y-r,r*2,r*2);
            g.DrawString($"R {Sketch.Radius:G5} mm",Font,Brushes.DarkSlateGray,p.X+r+5,p.Y);
        }
        else
        {
            var points=Closed?Sketch.Points:clicks;
            if(points.Count>1){var a=points.Select(Screen).ToArray();if(Closed)g.DrawPolygon(line,a);else g.DrawLines(line,a);}
            for(int i=0;i<points.Count;i++)
            {
                var p=Screen(points[i]);g.FillEllipse(Brushes.DarkBlue,p.X-3,p.Y-3,6,6);
                g.DrawString($"{i+1}: {points[i][0]:G5}, {points[i][1]:G5}",Font,Brushes.DarkSlateGray,p.X+6,p.Y+5);
            }
        }
        g.DrawString(Closed?"닫힌 스케치 · 치수 입력 / 피처 생성 가능":"사각형: 대각 두 점 / 원: 중심과 둘레 / 선: 점을 찍은 뒤 닫기",Font,Brushes.DimGray,12,12);
        g.DrawString("U → / V ↑ · mm · Ctrl+가운데: 이동 · 휠: 확대",Font,Brushes.DimGray,12,Height-30);
    }
}
