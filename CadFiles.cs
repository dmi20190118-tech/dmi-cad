using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace DmiCad;

sealed class Geometry
{
    public List<Vector3[]> Triangles { get; } = new();
    public List<int> FaceIds { get; } = new();
    public List<Vector3[]> Lines { get; } = new();
    public Vector3 Center { get; private set; }
    public float Extent { get; private set; } = 1;
    public Vector3 Size { get; private set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public void Finish()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        int count = 0;
        foreach (var p in Triangles.Concat(Lines).SelectMany(p => p))
        {
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) ||
                Math.Abs(p.X) > 1e12 || Math.Abs(p.Y) > 1e12 || Math.Abs(p.Z) > 1e12)
                throw new InvalidDataException("좌표 범위가 올바르지 않습니다.");
            min = Vector3.Min(min, p); max = Vector3.Max(max, p); count++;
        }
        if (count == 0) throw new InvalidDataException("표시할 형상이 없습니다.");
        Center = (min + max) / 2; Size = max - min;
        Extent = Math.Max(Math.Max(Size.X, Size.Y), Math.Max(Size.Z, .001f));
    }
}

static class CadFiles
{
    const int Limit = 250000;
    static float F(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    public static async Task<Geometry> Read(string path)
    {
        if (new FileInfo(path).Length > 128L * 1024 * 1024)
            throw new InvalidDataException("현재 버전의 파일 크기 제한은 128 MB입니다.");
        string ext = Path.GetExtension(path).ToLowerInvariant();
        Geometry geometry;
        if (ext is ".step" or ".stp" or ".iges" or ".igs")
        {
            string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".stl");
            try { await Convert(path, tmp); geometry = await Task.Run(() => Stl(tmp)); }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        else if (ext == ".stl") geometry = await Task.Run(() => Stl(path));
        else if (ext == ".dxf") geometry = await Task.Run(() => Dxf(path));
        else throw new NotSupportedException("이 원본 형식은 아직 지원하지 않습니다. 원래 CAD에서 STEP/IGES 또는 기본 2D DXF로 내보내 주세요. DWG와 상용 CAD 원본에는 별도 변환 SDK가 필요합니다.");
        geometry.Name = Path.GetFileName(path); geometry.Source = path; geometry.Finish();
        return geometry;
    }
    public static async Task Convert(string source, string target)
    {
        string engine = Path.Combine(AppContext.BaseDirectory, "engine", "cad-engine.exe");
        if (!File.Exists(engine)) throw new FileNotFoundException("CAD 읽기 엔진이 없습니다. 최신 설치파일로 다시 설치해 주세요.");
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(source); start.ArgumentList.Add(target);
        using var process = Process.Start(start) ?? throw new IOException("변환 엔진을 시작하지 못했습니다.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); throw new IOException("변환 제한 시간 3분을 초과했습니다."); }
        await stdout; string error = await stderr;
        if (process.ExitCode != 0 || !File.Exists(target))
            throw new IOException("CAD 변환 실패: " + error[..Math.Min(error.Length, 1500)]);
    }
    public static Geometry Stl(string path)
    {
        var g = new Geometry();
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        uint n = 0;
        if (stream.Length >= 84) { stream.Position = 80; n = reader.ReadUInt32(); }
        if (stream.Length >= 84 && 84L + n * 50L == stream.Length)
        {
            if (n > Limit) throw new InvalidDataException("삼각형 25만 개를 초과합니다. 거친 메시로 내보내 주세요.");
            for (int i = 0; i < n; i++)
            {
                stream.Position += 12;
                var triangle = new Vector3[3];
                for (int j = 0; j < 3; j++) triangle[j] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                reader.ReadUInt16(); g.Triangles.Add(triangle);
            }
        }
        else
        {
            stream.Position = 0; using var text = new StreamReader(stream);
            var vertices = new List<Vector3>();
            while (text.ReadLine() is string line)
            {
                var p = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 4 && p[0].Equals("vertex", StringComparison.OrdinalIgnoreCase))
                {
                    vertices.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                    if (vertices.Count == 3) { g.Triangles.Add(vertices.ToArray()); vertices.Clear(); }
                    if (g.Triangles.Count > Limit) throw new InvalidDataException("삼각형 25만 개를 초과합니다.");
                }
            }
            if (vertices.Count != 0) throw new InvalidDataException("손상된 STL 정점 목록입니다.");
        }
        g.Finish(); return g;
    }
    public static Geometry Dxf(string path)
    {
        var g = new Geometry();
        using var text = new StreamReader(path);
        var pairs = new List<(int code, string value)>();
        while (text.ReadLine() is string code)
        {
            string value = text.ReadLine() ?? throw new InvalidDataException("DXF 코드 쌍이 손상되었습니다.");
            if (!int.TryParse(code.Trim(), out int c)) throw new InvalidDataException("ASCII DXF 파일만 지원합니다.");
            pairs.Add((c, value.Trim()));
            if (pairs.Count > 2000000) throw new InvalidDataException("DXF가 너무 큽니다.");
        }
        bool entities = false;
        var unsupported = new HashSet<string>();
        for (int i = 0; i < pairs.Count;)
        {
            if (pairs[i].code == 2 && pairs[i].value == "ENTITIES") { entities = true; i++; continue; }
            if (pairs[i].code == 0 && pairs[i].value == "ENDSEC") entities = false;
            if (!entities || pairs[i].code != 0) { i++; continue; }
            string type = pairs[i++].value;
            var fields = new List<(int code, string value)>();
            while (i < pairs.Count && pairs[i].code != 0) fields.Add(pairs[i++]);
            float Get(int c, float fallback = 0) { var p = fields.Find(p => p.code == c); return p.value == null ? fallback : F(p.value); }
            Vector3 P(int x, int y, int z) => new(Get(x), Get(y), Get(z));
            if (type == "LINE") g.Lines.Add([P(10,20,30), P(11,21,31)]);
            else if (type is "CIRCLE" or "ARC")
            {
                var center = P(10,20,30); float radius = Get(40);
                if (radius <= 0) throw new InvalidDataException("DXF 반지름이 올바르지 않습니다.");
                if (Get(210) != 0 || Get(220) != 0 || Get(230, 1) != 1) throw new NotSupportedException("기울어진 DXF 곡선은 아직 지원하지 않습니다.");
                double start = type == "ARC" ? Get(50) : 0, end = type == "ARC" ? Get(51) : 360;
                if (end <= start) end += 360;
                int segments = Math.Clamp((int)Math.Ceiling((end - start) / 3), 1, 120);
                var points = Enumerable.Range(0, segments + 1).Select(j => {
                    double a = (start + (end-start)*j/segments) * Math.PI / 180;
                    return center + new Vector3((float)Math.Cos(a)*radius, (float)Math.Sin(a)*radius,0);
                }).ToArray();
                g.Lines.Add(points);
            }
            else if (type == "LWPOLYLINE")
            {
                if (fields.Any(p => p.code == 42 && F(p.value) != 0)) throw new NotSupportedException("불지 곡선 폴리라인은 직선/호로 분해한 뒤 다시 열어 주세요.");
                if (Get(210) != 0 || Get(220) != 0 || Get(230, 1) != 1) throw new NotSupportedException("기울어진 폴리라인은 아직 지원하지 않습니다.");
                var points = new List<Vector3>(); float? x = null;
                foreach (var p in fields) { if (p.code == 10) x = F(p.value); else if (p.code == 20 && x.HasValue) { points.Add(new Vector3(x.Value,F(p.value),Get(38))); x = null; } }
                if (points.Count >= 2) { if (((int)Get(70) & 1) != 0) points.Add(points[0]); g.Lines.Add(points.ToArray()); }
            }
            else unsupported.Add(type);
        }
        if (unsupported.Count > 0) throw new NotSupportedException("누락된 도면을 표시하지 않도록 불러오기를 중단했습니다. 미지원 DXF 요소: " + string.Join(", ",unsupported) + ". LINE/ARC/CIRCLE/LWPOLYLINE으로 분해해서 내보내 주세요.");
        g.Finish(); return g;
    }
    public static void WriteStl(Geometry g, string path)
    {
        if (g.Triangles.Count == 0) throw new NotSupportedException("선 도면은 STL 입체로 내보낼 수 없습니다.");
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(new byte[80]); writer.Write((uint)g.Triangles.Count);
        foreach (var t in g.Triangles)
        {
            var normal = Vector3.Cross(t[1]-t[0],t[2]-t[0]);
            if (normal.LengthSquared() > 0) normal = Vector3.Normalize(normal);
            foreach (var p in new[] { normal,t[0],t[1],t[2] }) { writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Z); }
            writer.Write((ushort)0);
        }
    }
}
