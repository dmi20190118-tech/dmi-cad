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

# Reverse direction: exact STEP bounds, all principal planes, cuts and roundtrip.
from OCP.Bnd import Bnd_Box
from OCP.BRepBndLib import BRepBndLib
with TemporaryDirectory(prefix="dmi-direction-") as folder:
    folder=Path(folder)
    project=folder/"direction.dmipart"
    target=folder/"direction.step"
    def check_direction(features, expected_volume, expected_bounds):
        project.write_text(json.dumps({"version":1,"units":"mm","features":features}),encoding="utf-8")
        subprocess.run([engine,str(project),str(target)],check=True,timeout=180)
        reader=STEPControl_Reader()
        assert reader.ReadFile(str(target))==IFSelect_RetDone and reader.TransferRoots()>0
        shape=reader.OneShape()
        props=GProp_GProps()
        BRepGProp.VolumeProperties_s(shape,props)
        assert abs(abs(props.Mass())-expected_volume)<1e-4
        box=Bnd_Box()
        BRepBndLib.Add_s(shape,box)
        assert all(abs(a-b)<1e-4 for a,b in zip(box.Get(),expected_bounds)),(box.Get(),expected_bounds)
    for plane,bounds in [
        ("XY",(0,0,-7,20,10,0)),
        ("XZ",(0,0,0,20,7,10)),
        ("YZ",(-7,0,0,0,20,10))]:
        f=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),depth=7,plane=plane)
        f["reverse"]=True
        check_direction([f],1400,bounds)
    base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]))
    cut=feature({"kind":"circle","center":[5,5],"radius":2},operation="cut",offset=10)
    cut["reverse"]=True
    check_direction([base,cut],2000-math.pi*40,(0,0,0,20,10,10))
    cut["reverse"]="false"
    project.write_text(json.dumps({"version":1,"units":"mm","features":[base,cut]}),encoding="utf-8")
    assert subprocess.run([engine,str(project),str(folder/"invalid.step")],capture_output=True,timeout=180).returncode!=0
print("Reverse extrusion bounds, three planes, reverse cut and invalid direction checks passed.")

# Midplane depth is total thickness; offset is the symmetry plane.
with TemporaryDirectory(prefix="dmi-midplane-") as folder:
    folder=Path(folder)
    project=folder/"midplane.dmipart"
    target=folder/"midplane.step"
    for plane,bounds in [
        ("XY",(0,0,-2,20,10,8)),
        ("XZ",(0,-2,0,20,8,10)),
        ("YZ",(-2,0,0,8,20,10))]:
        for reverse in (False,True):
            f=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),depth=10,offset=3,plane=plane)
            f.update(midplane=True,reverse=reverse)
            check_direction([f],2000,bounds)
    base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]))
    cut=feature({"kind":"circle","center":[5,5],"radius":2},depth=20,operation="cut",offset=5)
    cut["midplane"]=True
    check_direction([base,cut],2000-math.pi*40,(0,0,0,20,10,10))
    # A centered circular base verifies non-polygon profile translation.
    circle=feature({"kind":"circle","center":[0,0],"radius":2},depth=10,offset=3)
    circle["midplane"]=True
    check_direction([circle],math.pi*40,(-2,-2,-2,2,2,8))
    # New version roundtrip through the packaged engine.
    doc={"version":2,"units":"mm","features":[circle]}
    project.write_text(json.dumps(doc),encoding="utf-8")
    subprocess.run([engine,str(project),str(target)],check=True,timeout=180)
    for invalid in ("true",1,None):
        circle["midplane"]=invalid
        project.write_text(json.dumps(doc),encoding="utf-8")
        assert subprocess.run([engine,str(project),str(folder/"invalid.step")],capture_output=True,timeout=180).returncode!=0
    doc["version"]=5
    circle["midplane"]=True
    project.write_text(json.dumps(doc),encoding="utf-8")
    assert subprocess.run([engine,str(project),str(folder/"future.step")],capture_output=True,timeout=180).returncode!=0
print("Midplane exact bounds/volume, reversed invariance, circle, cut, v2 and invalid input checks passed.")

# Through-all follows upstream thickness instead of a hard-coded cut depth.
with TemporaryDirectory(prefix="dmi-through-all-") as folder:
    folder=Path(folder)
    project=folder/"through.dmipart"
    target=folder/"through.step"
    def check_through(features, expected):
        project.write_text(json.dumps({"version":3,"units":"mm","features":features}),encoding="utf-8")
        subprocess.run([engine,str(project),str(target)],check=True,timeout=180)
        reader=STEPControl_Reader()
        assert reader.ReadFile(str(target))==IFSelect_RetDone and reader.TransferRoots()>0
        props=GProp_GProps()
        BRepGProp.VolumeProperties_s(reader.OneShape(),props)
        assert abs(abs(props.Mass())-expected)<1e-4,(props.Mass(),expected)
    for plane in ("XY","XZ","YZ"):
        for thick in (10,25):
            base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),depth=thick,plane=plane)
            cut=feature({"kind":"circle","center":[5,5],"radius":2},depth=1,operation="cut",plane=plane)
            cut["throughAll"]=True
            check_through([base,cut],(200-math.pi*4)*thick)
            cut["reverse"]=True
            cut["offset"]=-thick if plane=="XZ" else thick
            check_through([base,cut],(200-math.pi*4)*thick)
    # Invalid combinations, missing stock, malformed flag and direction missing stock.
    def reject_through(features):
        project.write_text(json.dumps({"version":3,"units":"mm","features":features}),encoding="utf-8")
        assert subprocess.run([engine,str(project),str(folder/"invalid.step")],capture_output=True,timeout=180).returncode!=0
    base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]))
    cut=feature({"kind":"circle","center":[5,5],"radius":2},depth=1,operation="cut")
    cut["throughAll"]=True
    reject_through([cut])
    reject_through([base,dict(cut,operation="add")])
    reject_through([base,dict(cut,midplane=True)])
    reject_through([base,dict(cut,throughAll="true")])
    reject_through([base,dict(cut,offset=10)]) # positive direction points away
    # Keep original input and persist/reopen the complete feature history.
    original={"version":3,"units":"mm","features":[base,cut]}
    project.write_text(json.dumps(original),encoding="utf-8")
    original_bytes=project.read_bytes()
    reopened=json.loads(project.read_text(encoding="utf-8"))
    check_through(reopened["features"],(200-math.pi*4)*10)
    assert project.read_bytes()==original_bytes
print("Through-all three planes, both directions, upstream edits, disk reopen and rejection checks passed.")

# Exact planar face discovery and face-derived inward through-cut.
with TemporaryDirectory(prefix="dmi-face-plane-") as folder:
    folder=Path(folder)
    source=folder/"face.dmipart"
    target=folder/"face.step"
    def face_model(features):
        source.write_text(json.dumps({"version":3,"units":"mm","features":features}),encoding="utf-8")
        subprocess.run([engine,str(source),str(target)],check=True,timeout=180)
        return json.loads(Path(str(target)+".metrics.json").read_text())
    for plane in ("XY","XZ","YZ"):
        base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),plane=plane)
        m=face_model([base])
        faces=m["sketchPlanes"]
        assert len(faces)==6 and abs(sum(f["areaMm2"] for f in faces)-1000)<1e-5
        end=-10 if plane=="XZ" else 10
        top=next(f for f in faces if f["plane"]==plane and abs(f["offset"]-end)<1e-8)
        assert not top["outwardReverse"] and abs(top["areaMm2"]-200)<1e-6
        cut=feature({"kind":"circle","center":[5,5],"radius":2},plane=top["plane"],offset=top["offset"],operation="cut",depth=1)
        cut.update(reverse=not top["outwardReverse"],throughAll=True)
        assert abs(face_model([base,cut])["volumeMm3"]-(2000-math.pi*40))<1e-5
    cylinder=feature({"kind":"circle","center":[0,0],"radius":5},depth=10)
    faces=face_model([cylinder])["sketchPlanes"]
    assert len(faces)==2, "Curved cylinder wall was offered as a sketch plane."
    assert all(f["plane"]=="XY" for f in faces)
    tilted=feature(polygon([[0,0],[20,0],[0,10]]))
    assert len(face_model([tilted])["sketchPlanes"])==4, "Tilted planar side was incorrectly treated as a principal plane."
print("Exact face areas, three plane orientations, face-derived cuts and curved face exclusion passed.")

# Display triangles preserve exact OCCT face IDs, including curved occluders.
with TemporaryDirectory(prefix="dmi-pickmesh-") as folder:
    folder=Path(folder)
    source=folder/"pick.dmipart"
    target=folder/"pick.stl"
    base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]))
    source.write_text(json.dumps({"version":3,"units":"mm","features":[base]}),encoding="utf-8")
    subprocess.run([engine,str(source),str(target)],check=True,timeout=180)
    data=json.loads(Path(str(target)+".metrics.json").read_text())
    rows=data["renderTriangles"]
    assert len(rows)==12 and all(len(row)==10 for row in rows)
    planes={p["faceIndex"]:p for p in data["sketchPlanes"]}
    assert set(row[0] for row in rows)==set(planes)
    for row in rows:
        plane=planes[row[0]]
        axis={"YZ":0,"XZ":1,"XY":2}[plane["plane"]]
        assert all(abs(row[1+3*i+axis]-plane["offset"])<1e-8 for i in range(3))
    base=feature({"kind":"circle","center":[0,0],"radius":5})
    source.write_text(json.dumps({"version":3,"units":"mm","features":[base]}),encoding="utf-8")
    subprocess.run([engine,str(source),str(target)],check=True,timeout=180)
    data=json.loads(Path(str(target)+".metrics.json").read_text())
    assert len(set(row[0] for row in data["renderTriangles"]))==3
    assert len(data["sketchPlanes"])==2, "Curved wall must occlude without being a sketch plane."
print("Face-tagged display mesh and curved occlusion metadata checks passed.")

# Stable feature-end plane references follow upstream depth and chained references.
with TemporaryDirectory(prefix="dmi-plane-link-") as folder:
    folder=Path(folder)
    project=folder/"linked.dmipart"
    target=folder/"linked.step"
    def run_link(features):
        project.write_text(json.dumps({"version":4,"units":"mm","features":features}),encoding="utf-8")
        subprocess.run([engine,str(project),str(target)],check=True,timeout=180)
        return json.loads(Path(str(target)+".metrics.json").read_text())
    for plane in ("XY","XZ","YZ"):
        for thick in (10,25):
            base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),depth=thick,plane=plane)
            base["id"]="plate"
            cut=feature({"kind":"circle","center":[5,5],"radius":2},depth=1,operation="cut",plane=plane,offset=123)
            cut.update(id="hole",planeSourceId="plate",reverse=True,throughAll=True)
            m=run_link([base,cut])
            assert abs(m["volumeMm3"]-(200-math.pi*4)*thick)<1e-5
            assert abs(m["history"][1]["planeOffset"]-(-thick if plane=="XZ" else thick))<1e-8
    base=feature(polygon([[0,0],[20,0],[20,10],[0,10]]),depth=10)
    base["id"]="plate"
    boss=feature(polygon([[0,0],[5,0],[5,5],[0,5]]),depth=5,offset=999)
    boss.update(id="boss",planeSourceId="plate")
    tip=feature(polygon([[0,0],[2,0],[2,2],[0,2]]),depth=2,offset=999)
    tip.update(id="tip",planeSourceId="boss")
    m=run_link([base,boss,tip])
    assert abs(m["volumeMm3"]-2133)<1e-5 and m["history"][2]["planeOffset"]==15
    base["depth"]=20
    m=run_link([base,boss,tip])
    assert abs(m["volumeMm3"]-4133)<1e-5 and m["history"][2]["planeOffset"]==25
    def reject_link(features):
        project.write_text(json.dumps({"version":4,"units":"mm","features":features}),encoding="utf-8")
        assert subprocess.run([engine,str(project),str(folder/"invalid.step")],capture_output=True,timeout=180).returncode!=0
    reject_link([boss]) # source removed
    reject_link([dict(base,planeSourceId="plate")]) # self cycle
    reject_link([base,dict(boss,planeSourceId="tip"),tip]) # forward/cyclic
    reject_link([dict(base,midplane=True),boss]) # unsupported source mode
    reject_link([base,dict(boss,plane="XZ")]) # mismatched plane
print("End-plane links, depth edits, chained references and broken reference rejection passed.")
