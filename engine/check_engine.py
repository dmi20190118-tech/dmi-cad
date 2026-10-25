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

# Parametric model: exact volume, export roundtrip, and invalid geometry rejection.
import json
import math
from OCP.BRepGProp import BRepGProp
from OCP.GProp import GProp_GProps
from OCP.STEPControl import STEPControl_Reader

def polygon(points):
    return {"kind":"polygon","points":points}
def feature(sketch, depth=10, operation="add", offset=0, plane="XY"):
    return {"name":"test","plane":plane,"offset":offset,"depth":depth,"operation":operation,"sketch":sketch}

with TemporaryDirectory(prefix="dmi-model-") as folder:
    folder=Path(folder)
    project=folder/"part.dmipart"
    features=[
        feature(polygon([[0,0],[20,0],[20,10],[0,10]])),
        feature({"kind":"circle","center":[5,5],"radius":2},operation="cut"),
        feature(polygon([[0,0],[5,0],[5,5],[0,5]]),depth=5,offset=10)
    ]
    doc={"version":1,"units":"mm","name":"test","features":features}
    project.write_text(json.dumps(doc),encoding="utf-8")
    result=folder/"part.step"
    subprocess.run([engine,str(project),str(result)],check=True,timeout=180)
    metrics=json.loads(Path(str(result)+".metrics.json").read_text())
    expected=2000-math.pi*4*10+125
    assert abs(metrics["volumeMm3"]-expected)<1e-5,(metrics,expected)
    assert metrics["solidCount"]==1 and metrics["featureCount"]==3
    reader=STEPControl_Reader()
    assert reader.ReadFile(str(result))==IFSelect_RetDone
    assert reader.TransferRoots()>0
    props=GProp_GProps()
    BRepGProp.VolumeProperties_s(reader.OneShape(),props)
    assert abs(abs(props.Mass())-expected)<1e-4
    subprocess.run([engine,str(project),str(folder/"part.stl")],check=True,timeout=180)
    # Editing depth must regenerate the exact body.
    doc["features"][2]["depth"]=7
    project.write_text(json.dumps(doc),encoding="utf-8")
    subprocess.run([engine,str(project),str(result)],check=True,timeout=180)
    edited=json.loads(Path(str(result)+".metrics.json").read_text())
    assert abs(edited["volumeMm3"]-(expected+50))<1e-5
    def reject(candidate):
        project.write_text(json.dumps(candidate),encoding="utf-8")
        failed=subprocess.run([engine,str(project),str(folder/"invalid.step")],capture_output=True,timeout=180)
        assert failed.returncode!=0,"Invalid model was silently accepted."
    reject({"version":1,"units":"mm","features":[feature(polygon([[0,0],[10,10],[0,10],[10,0]]))]})
    reject({"version":1,"units":"mm","features":[feature({"kind":"circle","center":[0,0],"radius":0})]})
    reject({"version":1,"units":"mm","features":[features[0],feature({"kind":"circle","center":[100,100],"radius":2},operation="cut")]})
    reject({"version":1,"units":"mm","features":[features[0],feature(polygon([[100,0],[110,0],[110,10],[100,10]]))]})
    # Other principal planes must produce the same single-feature volume.
    for plane in ("XZ","YZ"):
        doc={"version":1,"units":"mm","features":[feature(polygon([[0,0],[20,0],[20,10],[0,10]]),plane=plane)]}
        project.write_text(json.dumps(doc),encoding="utf-8")
        subprocess.run([engine,str(project),str(result)],check=True,timeout=180)
        assert abs(json.loads(Path(str(result)+".metrics.json").read_text())["volumeMm3"]-2000)<1e-5
print("Exact sketch extrusion, boolean cut, feature edit, STEP roundtrip, plane and rejection checks passed.")
