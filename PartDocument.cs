using System.Text.Json;

namespace DmiCad;

public sealed class PartSketch
{
    public string Kind { get; set; } = "polygon";
    public string DimensionMode { get; set; } = "polygon";
    public List<double[]> Points { get; set; } = new();
    public double[] Center { get; set; } = [0,0];
    public double Radius { get; set; } = 5;
    public PartSketch Copy() => JsonSerializer.Deserialize<PartSketch>(JsonSerializer.Serialize(this,PartDocument.Json),PartDocument.Json)!;
}
public sealed class PartFeature
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Operation { get; set; } = "add";
    public string Plane { get; set; } = "XY";
    public double Offset { get; set; }
    public double Depth { get; set; } = 10;
    public bool Reverse { get; set; }
    public bool Midplane { get; set; }
    public bool ThroughAll { get; set; }
    public PartSketch Sketch { get; set; } = new();
}
public sealed class PartDocument
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public int Version { get; set; } = 3;
    public string Units { get; set; } = "mm";
    public string Name { get; set; } = "부품1";
    public int Revision { get; set; }
    public List<PartFeature> Features { get; set; } = new();
    public PartDocument Copy() => Parse(JsonSerializer.Serialize(this,Json));
    public static PartDocument Parse(string text)
    {
        if(text.Length>1048576)throw new InvalidDataException("프로젝트 제한은 1 MB입니다.");
        var doc=JsonSerializer.Deserialize<PartDocument>(text,Json)??throw new InvalidDataException("프로젝트가 비어 있습니다.");
        if((doc.Version<1||doc.Version>3)||doc.Units!="mm"||doc.Features==null||doc.Features.Count>100||string.IsNullOrWhiteSpace(doc.Name)||doc.Name.Length>200)
            throw new InvalidDataException("지원하지 않는 프로젝트 버전·단위·구조입니다.");
        var ids=new HashSet<string>();
        bool Finite(double v)=>double.IsFinite(v)&&Math.Abs(v)<=1000000;
        foreach(var f in doc.Features)
        {
            if(f==null||!ids.Add(f.Id)||string.IsNullOrWhiteSpace(f.Id)||f.Sketch==null||f.Name==null||f.Name.Length>200||
               !(f.Operation is "add" or "cut")||!(f.Plane is "XY" or "XZ" or "YZ")||
               !Finite(f.Offset)||!Finite(f.Depth)||f.Depth<.001||f.Depth>10000)
                throw new InvalidDataException("피처 이름·평면·치수·식별자를 확인하세요.");
            if(f.ThroughAll&&(f.Operation!="cut"||f.Midplane))throw new InvalidDataException("관통은 한 방향 잘라내기에만 사용할 수 있습니다.");
            var s=f.Sketch;
            if(s.Kind=="circle")
            {
                if(s.Center==null||s.Center.Length!=2||s.Center.Any(v=>!Finite(v))||!Finite(s.Radius)||s.Radius<.001)
                    throw new InvalidDataException("원 중심과 반지름을 확인하세요.");
            }
            else if(s.Kind=="polygon")
            {
                if(s.Points==null||s.Points.Count<3||s.Points.Count>500||s.Points.Any(p=>p==null||p.Length!=2||p.Any(v=>!Finite(v))))
                    throw new InvalidDataException("닫힌 스케치에는 3~500개의 올바른 점이 필요합니다.");
            }
            else throw new InvalidDataException("지원하지 않는 스케치입니다.");
        }
        doc.Version=3; // Older applications reject new geometry semantics.
        return doc;
    }
}

static class PartChecks
{
    public static void Run()
    {
        var doc=new PartDocument();
        doc.Features.Add(new PartFeature {Name="돌출1",Sketch=new PartSketch {Points=new(){new[]{0.0,0.0},new[]{20.0,0.0},new[]{20.0,10.0},new[]{0.0,10.0}}}});
        var copy=doc.Copy();
        copy.Features[0].Sketch.Points[0][0]=9;
        if(doc.Features[0].Sketch.Points[0][0]!=0)throw new Exception("Part snapshots share mutable vertices.");
        if(PartDocument.Parse(JsonSerializer.Serialize(doc,PartDocument.Json)).Features[0].Id!=doc.Features[0].Id)throw new Exception("Feature identity changed.");
        copy.Features[0].Reverse=true;
        if(!copy.Copy().Features[0].Reverse||doc.Features[0].Reverse)throw new Exception("Extrusion direction persistence failed.");
        copy.Features[0].Midplane=true;
        if(!copy.Copy().Features[0].Midplane||doc.Features[0].Midplane)throw new Exception("Midplane persistence failed.");
        string legacy=JsonSerializer.Serialize(doc,PartDocument.Json).Replace("\"version\": 3","\"version\": 1");
        if(PartDocument.Parse(legacy).Version!=3)throw new Exception("Legacy project migration failed.");
        var future=doc.Copy();future.Version=4;
        try {future.Copy();throw new Exception("Future project version accepted.");}catch(InvalidDataException){}
        var through=doc.Copy();through.Features[0].Operation="cut";through.Features[0].ThroughAll=true;
        if(!through.Copy().Features[0].ThroughAll)throw new Exception("Through-all persistence failed.");
        through.Features[0].Midplane=true;
        try {through.Copy();throw new Exception("Incompatible through-all mode accepted.");}catch(InvalidDataException){}
        doc.Features[0].Depth=-1;
        try {doc.Copy();throw new Exception("Negative depth accepted.");}catch(InvalidDataException){}
    }
}
