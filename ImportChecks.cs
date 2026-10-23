using System.Numerics;
namespace DmiCad;

static class ImportChecks
{
    public static void Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dmi-check-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string stl = Path.Combine(dir,"triangle.stl");
            File.WriteAllText(stl,"solid test\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 10 0 0\nvertex 0 20 0\nendloop\nendfacet\nendsolid test");
            var mesh = CadFiles.Read(stl).GetAwaiter().GetResult();
            if (mesh.Triangles.Count != 1 || mesh.Size != new Vector3(10,20,0)) throw new Exception("ASCII STL import failed.");
            string binary = Path.Combine(dir,"binary.stl");
            CadFiles.WriteStl(mesh,binary);
            if (CadFiles.Read(binary).GetAwaiter().GetResult().Size != mesh.Size) throw new Exception("STL roundtrip failed.");
            string dxf = Path.Combine(dir,"line.dxf");
            File.WriteAllText(dxf,"0\nSECTION\n2\nENTITIES\n0\nLINE\n10\n0\n20\n0\n11\n12\n21\n8\n0\nENDSEC\n0\nEOF\n");
            var wire = CadFiles.Read(dxf).GetAwaiter().GetResult();
            if (wire.Lines.Count != 1 || wire.Size != new Vector3(12,8,0)) throw new Exception("DXF LINE import failed.");
            File.WriteAllText(dxf,"0\nSECTION\n2\nENTITIES\n0\nINSERT\n2\nblock\n0\nENDSEC\n0\nEOF\n");
            try { CadFiles.Read(dxf).GetAwaiter().GetResult(); throw new Exception("Unsupported DXF silently accepted."); }
            catch (NotSupportedException) { }
        }
        finally { Directory.Delete(dir,true); }
    }
}
