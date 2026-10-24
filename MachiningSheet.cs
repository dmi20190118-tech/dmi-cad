using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DmiCad;

public sealed class SheetData
{
    public Dictionary<string,string> Values { get; set; } = new();
    public string Get(string key) => Values.GetValueOrDefault(key,"").Trim();
}

static class SheetRules
{
    public static readonly (string Key,string Label)[] Fields =
    [
        ("program","NC 프로그램명"), ("drawing","제품 / 도면번호"), ("revision","도면 / NC 리비전"),
        ("operation","공정명"), ("machine","장비 / 컨트롤러"), ("material","소재"),
        ("origin","가공 원점 / 좌표계"), ("setup","고정 / 기준면 / 셋업"),
        ("tool","공구번호 T"), ("diameter","공구 지름 D (mm)"), ("teeth","날 수 Z"),
        ("rpm","회전수 S (rpm)"), ("feed","이송 F (mm/min)"),
        ("depth","절입 깊이 ap (mm)"), ("stepover","절삭 폭 ae (mm)"),
        ("dimensions","작업 치수 / 공차"), ("stock","소재 크기 / 잔삭 여유"),
        ("maxrpm","참조 최대 회전수 (rpm)"), ("maxfeed","참조 최대 이송 (mm/min)"),
        ("vc","참조 절삭속도 Vc (m/min)"), ("fz","참조 날당 이송 fz (mm/tooth)"),
        ("reference","조건표 출처 / 버전"), ("notes","작업자 주의사항")
    ];
    static readonly string[] Positive = ["diameter","teeth","rpm","feed","depth","stepover"];
    public static bool Number(string text,out double result)
    {
        bool valid = double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out result)
            || double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out result);
        return valid && double.IsFinite(result);
    }
    public static (List<string> Errors,List<string> Review,string Calculated) Check(SheetData d)
    {
        var errors = new List<string>(); var review = new List<string>();
        foreach (string key in new[] { "program","drawing","operation","machine","material","origin","tool","dimensions" })
            if (d.Get(key).Length == 0) errors.Add(Fields.First(f=>f.Key==key).Label + " 누락");
        foreach (string key in Positive)
            if (!Number(d.Get(key),out double v) || v <= 0)
                errors.Add(Fields.First(f=>f.Key==key).Label + ": 0보다 큰 수가 필요합니다.");
        foreach (string key in new[] { "tool","teeth" })
            if (Number(d.Get(key),out double v) && (v < 1 || v != Math.Truncate(v)))
                errors.Add(Fields.First(f=>f.Key==key).Label + ": 양의 정수가 필요합니다.");
        if (!Number(d.Get("tool"),out _)) errors.Add("공구번호는 숫자로 입력하세요.");
        foreach (string key in new[] { "maxrpm","maxfeed","vc","fz" })
            if (d.Get(key).Length > 0 && (!Number(d.Get(key),out double value) || value <= 0))
                errors.Add(Fields.First(f=>f.Key==key).Label + ": 양수가 필요합니다.");
        string calculated = "";
        if (errors.Count == 0)
        {
            Number(d.Get("diameter"),out double diameter); Number(d.Get("rpm"),out double rpm);
            Number(d.Get("teeth"),out double teeth); Number(d.Get("feed"),out double feed);
            Number(d.Get("stepover"),out double width);
            double vc = Math.PI * diameter * rpm / 1000;
            double fz = feed / (rpm * teeth);
            if (!double.IsFinite(vc) || !double.IsFinite(fz) || fz <= 0)
                errors.Add("계산 범위를 벗어난 조건입니다.");
            calculated = $"계산 절삭속도 Vc = {vc:F3} m/min / 계산 날당 이송 fz = {fz:F5} mm/tooth";
            if (width > diameter) errors.Add("절삭 폭 ae가 공구 지름보다 큽니다. 단위와 입력값을 확인하세요.");
            if (Number(d.Get("maxrpm"),out double maxrpm) && rpm > maxrpm) errors.Add("회전수가 참조 최대값을 초과합니다.");
            if (Number(d.Get("maxfeed"),out double maxfeed) && feed > maxfeed) errors.Add("이송이 참조 최대값을 초과합니다.");
            if (Number(d.Get("vc"),out double targetVc) && Math.Abs(vc-targetVc)/targetVc > .05)
                review.Add("입력 회전수와 참조 Vc가 5% 넘게 다릅니다. 조건 변경 여부를 확인하세요.");
            if (Number(d.Get("fz"),out double targetFz) && Math.Abs(fz-targetFz)/targetFz > .05)
                review.Add("입력 이송과 참조 fz가 5% 넘게 다릅니다. 조건 변경 여부를 확인하세요.");
        }
        foreach (string key in new[] { "maxrpm","maxfeed","vc","fz","reference","revision","setup" })
            if (d.Get(key).Length == 0) review.Add(Fields.First(f=>f.Key==key).Label + " 확인 필요");
        review.Add("NC 원본의 공구·S/F·좌표·단위·가공 치수와 자동 대조하지 않은 초안입니다.");
        review.Add("지름 기준 Vc 및 mm/min 기준 fz 계산입니다. 선삭·볼엔드밀 유효지름·G93/G95 등은 별도 검토가 필요합니다.");
        return (errors,review,calculated);
    }
    public static string Html(SheetData d)
    {
        var result = Check(d);
        if (result.Errors.Count > 0) throw new InvalidDataException(string.Join("\n",result.Errors));
        string E(string value) => WebUtility.HtmlEncode(value);
        var body = new StringBuilder();
        foreach(var field in Fields)
            body.Append("<tr><th>").Append(E(field.Label)).Append("</th><td>")
                .Append(E(d.Get(field.Key)).Replace("\n","<br>")).Append("</td></tr>");
        return "<!doctype html><html lang='ko'><meta charset='utf-8'><title>가공 지시서 초안</title>" +
            "<style>body{font:14px sans-serif;color:#172438;max-width:900px;margin:28px auto;padding:12px}h1{font-size:25px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #8a96a5;padding:7px;text-align:left;overflow-wrap:anywhere}th{width:32%;background:#eef2f7}p,li{line-height:1.5}.draft{border:2px solid #b77a12;padding:10px}@page{size:A4;margin:12mm}@media print{button{display:none}body{margin:0;padding:0;font-size:11px}tr{break-inside:avoid}th{-webkit-print-color-adjust:exact}}</style>" +
            " <button onclick='window.print()'>인쇄 / PDF 저장</button>" + Brand.HtmlLogo + "<h1>가공 지시서 — 검토용 초안</h1>" +
            "<p>작성: "+E(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))+"</p><table>"+body+"</table><p>"+E(result.Calculated)+"</p>"+
            "<div class='draft'><strong>입력 형식·참조 최대값 검사 통과 / 현장 사용 전 확인 필요</strong><ul>"+
            string.Join("",result.Review.Select(t=>"<li>"+E(t)+"</li>"))+
            "</ul></div><p>작성자: __________ &nbsp; 검토자: __________ &nbsp; 확인일: __________</p></html>";
    }
}

sealed class MachiningSheetWindow : Form
{
    readonly Dictionary<string,TextBox> inputs = new();
    readonly TextBox findings = new() { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Bottom,Height=150 };
    public MachiningSheetWindow()
    {
        Text = "DMI CAD — 가공 지시서 초안"; Width=850;Height=900;MinimumSize=new Size(650,600);
        Font=new Font("Segoe UI",9);
        var table=new TableLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,Padding=new Padding(12)};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var field in SheetRules.Fields)
        {
            int row=table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,39));
            table.Controls.Add(new Label {Text=field.Label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
            var box=new TextBox {Dock=DockStyle.Fill,Margin=new Padding(3,7,3,3),MaxLength=2000};
            inputs.Add(field.Key,box); table.Controls.Add(box,1,row);
        }
        var actions=new FlowLayoutPanel {Dock=DockStyle.Top,Height=44};
        void Button(string label,Action action) {var b=new Button {Text=label,AutoSize=true,Height=32};b.Click+=(_,_)=>action();actions.Controls.Add(b);}
        Button("입력 점검",()=>ValidateSheet(true));
        Button("인쇄용 시트",Print);
        Button("초안 저장",Save);
        Button("초안 불러오기",LoadDraft);
        var notice=new Label {Text="임시 양식 · 한 공정용 · 실제 Excel 양식/조건표 및 NC 자동 연동은 추후 적용",Dock=DockStyle.Top,Height=36,Padding=new Padding(8),ForeColor=Color.DarkGoldenrod};
        Controls.Add(table);Controls.Add(findings);Controls.Add(actions);Controls.Add(notice);
    }
    SheetData Data()=>new() {Values=inputs.ToDictionary(p=>p.Key,p=>p.Value.Text)};
    bool ValidateSheet(bool popup)
    {
        var r=SheetRules.Check(Data());
        findings.ForeColor=r.Errors.Count>0?Color.DarkRed:Color.DarkGoldenrod;
        findings.Text=(r.Errors.Count>0?"입력 오류 — 수정 필요\r\n":"기본 입력 검사 통과 — 검토 필요\r\n")+
            string.Join("\r\n",r.Errors.Concat(r.Review))+"\r\n"+r.Calculated;
        if(popup) MessageBox.Show(this,findings.Text,r.Errors.Count>0?"입력 오류":"검토 필요",MessageBoxButtons.OK,r.Errors.Count>0?MessageBoxIcon.Error:MessageBoxIcon.Warning);
        return r.Errors.Count==0;
    }
    void Print()
    {
        if(!ValidateSheet(false)) {MessageBox.Show(this,"입력 오류를 먼저 수정하세요.");return;}
        try
        {
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DMI CAD","Sheets");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"sheet-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".html");
            File.WriteAllText(path,SheetRules.Html(Data()),Encoding.UTF8);
            Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
        }
        catch(Exception ex) {MessageBox.Show(this,ex.Message,"시트 생성 실패");}
    }
    void Save()
    {
        using var dialog=new SaveFileDialog {Filter="가공 지시서 초안|*.dmisheet",FileName="가공지시서.dmisheet"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try {File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(Data(),new JsonSerializerOptions{WriteIndented=true}),Encoding.UTF8);}
        catch(Exception ex) {MessageBox.Show(this,ex.Message,"저장 실패");}
    }
    void LoadDraft()
    {
        using var dialog=new OpenFileDialog {Filter="가공 지시서 초안|*.dmisheet"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            if(new FileInfo(dialog.FileName).Length>128*1024)throw new InvalidDataException("초안 파일이 너무 큽니다.");
            var data=JsonSerializer.Deserialize<SheetData>(File.ReadAllText(dialog.FileName))??throw new InvalidDataException("잘못된 초안입니다.");
            if(data.Values==null || data.Values.Any(p=>p.Value==null || p.Value.Length>2000))throw new InvalidDataException("잘못된 초안 항목입니다.");
            foreach(var input in inputs)input.Value.Text=data.Get(input.Key);
            ValidateSheet(false);
        }
        catch(Exception ex) {MessageBox.Show(this,ex.Message,"불러오기 실패");}
    }
}

static class SheetChecks
{
    public static void Run()
    {
        var data=new SheetData();
        foreach(string key in new[]{"program","drawing","revision","operation","machine","material","origin","setup","dimensions","reference"})data.Values[key]="test";
        foreach(var p in new[]{("tool","1"),("diameter","10"),("teeth","4"),("rpm","3000"),("feed","600"),("depth","1"),("stepover","5"),("maxrpm","6000"),("maxfeed","1200"),("vc","94.2478"),("fz","0.05")})data.Values[p.Item1]=p.Item2;
        if(SheetRules.Check(data).Errors.Count!=0)throw new Exception("Valid milling sheet rejected.");
        data.Values["notes"]="<script>alert(1)</script>";
        if(SheetRules.Html(data).Contains("<script>"))throw new Exception("Sheet HTML escaping failed.");
        data.Values["rpm"]="7000";
        if(!SheetRules.Check(data).Errors.Any(e=>e.Contains("회전수가")))throw new Exception("RPM limit not enforced.");
        data.Values["rpm"]="NaN";
        if(SheetRules.Check(data).Errors.Count==0)throw new Exception("Nonfinite input accepted.");
        if(SheetRules.Check(new SheetData()).Errors.Count==0)throw new Exception("Empty sheet accepted.");
    }
}
