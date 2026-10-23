import subprocess
import sys
from pathlib import Path
from tempfile import TemporaryDirectory
from OCP.BRepPrimAPI import BRepPrimAPI_MakeBox
from OCP.STEPControl import STEPControl_Writer, STEPControl_AsIs
from OCP.IFSelect import IFSelect_RetDone
from OCP.StlAPI import StlAPI_Reader
from OCP.TopoDS import TopoDS_Shape

engine = str(Path(sys.argv[1]).resolve())
with TemporaryDirectory(prefix="dmi cad 한글 ") as folder:
    folder = Path(folder)
    step = folder / "sample.step"
    writer = STEPControl_Writer()
    assert writer.Transfer(BRepPrimAPI_MakeBox(10, 20, 30).Shape(), STEPControl_AsIs) == IFSelect_RetDone
    assert writer.Write(str(step)) == IFSelect_RetDone
    def run(source, target):
        subprocess.run([engine, str(source), str(target)], check=True, timeout=180)
        assert target.exists() and target.stat().st_size > 0
    iges = folder / "sample.iges"
    stl = folder / "sample.stl"
    run(step, stl)
    shape = TopoDS_Shape()
    assert StlAPI_Reader().Read(shape, str(stl)) and not shape.IsNull()
    run(step, iges)
    run(iges, folder / "roundtrip.step")
    run(iges, folder / "iges.stl")
print("Packaged engine STEP/IGES/STL checks passed.")
