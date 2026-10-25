"""Versioned sketch-feature model, in millimeters. Exact OCCT solids."""
import json
import math
from pathlib import Path
from OCP.Bnd import Bnd_Box
from OCP.BRepBndLib import BRepBndLib
from OCP.gp import gp_Pnt, gp_Dir, gp_Ax2, gp_Circ, gp_Vec, gp_Trsf
from OCP.BRepBuilderAPI import BRepBuilderAPI_MakePolygon, BRepBuilderAPI_MakeEdge, BRepBuilderAPI_MakeWire, BRepBuilderAPI_MakeFace, BRepBuilderAPI_Transform
from OCP.BRepPrimAPI import BRepPrimAPI_MakePrism
from OCP.BRepAlgoAPI import BRepAlgoAPI_Fuse, BRepAlgoAPI_Cut
from OCP.BRepCheck import BRepCheck_Analyzer
from OCP.BRepGProp import BRepGProp
from OCP.GProp import GProp_GProps
from OCP.TopExp import TopExp_Explorer
from OCP.TopAbs import TopAbs_SOLID, TopAbs_FACE, TopAbs_REVERSED
from OCP.TopoDS import TopoDS
from OCP.BRepAdaptor import BRepAdaptor_Surface
from OCP.GeomAbs import GeomAbs_Plane

def number(value, name, positive=False):
    if isinstance(value, bool) or not isinstance(value, (float, int)):
        raise ValueError(name + " must be numeric.")
    value = float(value)
    if not math.isfinite(value) or abs(value) > 1000000 or (positive and value <= 0):
        raise ValueError(name + " is outside the supported range.")
    return value

def point(value):
    if not isinstance(value, list) or len(value) != 2:
        raise ValueError("Sketch point requires two coordinates.")
    return (number(value[0], "U"), number(value[1], "V"))

def cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def polygon_points(sketch):
    points = [point(p) for p in sketch.get("points", [])]
    if not 3 <= len(points) <= 500:
        raise ValueError("A closed polygon requires 3 to 500 vertices.")
    for i in range(len(points)):
        a, b = points[i], points[(i+1) % len(points)]
        if math.dist(a, b) < 1e-6:
            raise ValueError("Duplicate or zero-length polygon edge.")
        for j in range(i+1, len(points)):
            if j == i+1 or (i == 0 and j == len(points)-1):
                continue
            c, d = points[j], points[(j+1) % len(points)]
            def on(p, q, r):
                return abs(cross(p, q, r)) < 1e-8 and min(p[0],q[0])-1e-8 <= r[0] <= max(p[0],q[0])+1e-8 and min(p[1],q[1])-1e-8 <= r[1] <= max(p[1],q[1])+1e-8
            if (cross(a,b,c)*cross(a,b,d)<0 and cross(c,d,a)*cross(c,d,b)<0) or on(a,b,c) or on(a,b,d) or on(c,d,a) or on(c,d,b):
                raise ValueError("Self-intersecting or touching polygon edges.")
    area = sum(points[i][0]*points[(i+1)%len(points)][1]-points[(i+1)%len(points)][0]*points[i][1] for i in range(len(points)))/2
    if abs(area) < 1e-6:
        raise ValueError("Sketch has no enclosed area.")
    return points

def volume(shape):
    props = GProp_GProps()
    BRepGProp.VolumeProperties_s(shape, props)
    return abs(props.Mass())

def count_solids(shape):
    count = 0
    it = TopExp_Explorer(shape, TopAbs_SOLID)
    while it.More():
        count += 1
        it.Next()
    return count

def sketch_planes(shape):
    """Exact planar faces parallel to principal planes; no inferred mesh planes."""
    result = []
    explorer = TopExp_Explorer(shape, TopAbs_FACE)
    index = 0
    while explorer.More():
        face = TopoDS.Face_s(explorer.Current())
        surface = BRepAdaptor_Surface(face)
        if surface.GetType() == GeomAbs_Plane:
            p = surface.Plane()
            normal = p.Axis().Direction()
            if face.Orientation() == TopAbs_REVERSED:
                normal = normal.Reversed()
            xyz = (normal.X(), normal.Y(), normal.Z())
            axis = max(range(3), key=lambda i: abs(xyz[i]))
            if abs(abs(xyz[axis])-1) < 1e-12 and all(abs(xyz[i]) < 1e-12 for i in range(3) if i != axis):
                origin = p.Location()
                offset = (origin.X(), origin.Y(), origin.Z())[axis]
                if abs(offset) > 1000000:
                    index += 1
                    explorer.Next()
                    continue
                props = GProp_GProps()
                BRepGProp.SurfaceProperties_s(face, props)
                center = props.CentreOfMass()
                result.append({"faceIndex":index,"plane":("YZ","XZ","XY")[axis],
                    "offset":offset,"areaMm2":props.Mass(),
                    "outwardReverse":xyz[axis] * (1,-1,1)[axis] < 0,
                    "center":[center.X(),center.Y(),center.Z()]})
        index += 1
        explorer.Next()
    return result

def build_part(path):
    if Path(path).stat().st_size > 1048576:
        raise ValueError("Part project exceeds 1 MB.")
    doc = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    if doc.get("version") not in (1, 2, 3) or doc.get("units") != "mm":
        raise ValueError("Unsupported part project version or units.")
    features = doc.get("features", [])
    if not 1 <= len(features) <= 100:
        raise ValueError("Part requires 1 to 100 features.")
    shape = None
    history = []
    for index, feature in enumerate(features):
        try:
            plane = feature.get("plane", "XY")
            offset = number(feature.get("offset", 0), "Plane offset")
            depth = number(feature.get("depth"), "Depth", True)
            if depth > 10000:
                raise ValueError("Depth exceeds 10000 mm.")
            if plane == "XY":
                def map_point(u, v): return gp_Pnt(u, v, offset)
                direction = gp_Dir(0,0,1)
            elif plane == "XZ":
                def map_point(u, v): return gp_Pnt(u, offset, v)
                direction = gp_Dir(0,-1,0)
            elif plane == "YZ":
                def map_point(u, v): return gp_Pnt(offset, u, v)
                direction = gp_Dir(1,0,0)
            else:
                raise ValueError("Unsupported sketch plane.")
            reverse = feature.get("reverse", False)
            if not isinstance(reverse, bool):
                raise ValueError("Reverse direction must be boolean.")
            if reverse:
                direction = direction.Reversed()
            midplane = feature.get("midplane", False)
            if not isinstance(midplane, bool):
                raise ValueError("Midplane must be boolean.")
            through_all = feature.get("throughAll", False)
            if not isinstance(through_all, bool):
                raise ValueError("Through-all must be boolean.")
            if through_all:
                if shape is None or feature.get("operation") != "cut" or midplane:
                    raise ValueError("Through-all requires an existing solid and a one-direction cut.")
                bounds = Bnd_Box()
                BRepBndLib.Add_s(shape, bounds)
                low_x, low_y, low_z, high_x, high_y, high_z = bounds.Get()
                origin = map_point(0, 0)
                far = max(
                    (x-origin.X())*direction.X()+(y-origin.Y())*direction.Y()+(z-origin.Z())*direction.Z()
                    for x in (low_x,high_x) for y in (low_y,high_y) for z in (low_z,high_z))
                depth = max(0,far)+1.0
            sketch = feature.get("sketch", {})
            kind = sketch.get("kind")
            if kind == "circle":
                center = point(sketch.get("center"))
                radius = number(sketch.get("radius"), "Radius", True)
                circle = gp_Circ(gp_Ax2(map_point(*center), direction), radius)
                wire = BRepBuilderAPI_MakeWire(BRepBuilderAPI_MakeEdge(circle).Edge()).Wire()
            elif kind == "polygon":
                builder = BRepBuilderAPI_MakePolygon()
                for p in polygon_points(sketch):
                    builder.Add(map_point(*p))
                builder.Close()
                wire = builder.Wire()
            else:
                raise ValueError("Only circle and closed polygon sketches are supported.")
            face_builder = BRepBuilderAPI_MakeFace(wire, True)
            if not face_builder.IsDone():
                raise ValueError("Sketch could not form a planar face.")
            profile = face_builder.Face()
            if midplane:
                shift = gp_Trsf()
                shift.SetTranslation(gp_Vec(direction).Multiplied(-depth/2))
                profile = BRepBuilderAPI_Transform(profile, shift, True).Shape()
            prism = BRepPrimAPI_MakePrism(profile, gp_Vec(direction).Multiplied(depth), True).Shape()
            if not BRepCheck_Analyzer(prism).IsValid():
                raise ValueError("Extrusion produced an invalid solid.")
            operation = feature.get("operation")
            previous = volume(shape) if shape is not None else 0
            if shape is None:
                if operation != "add":
                    raise ValueError("The first feature must add material.")
                candidate = prism
            else:
                if operation == "add":
                    boolean = BRepAlgoAPI_Fuse(shape, prism)
                elif operation == "cut":
                    boolean = BRepAlgoAPI_Cut(shape, prism)
                else:
                    raise ValueError("Unsupported feature operation.")
                boolean.Build()
                if not boolean.IsDone():
                    raise ValueError("Boolean operation failed.")
                candidate = boolean.Shape()
            current = volume(candidate)
            if not BRepCheck_Analyzer(candidate).IsValid() or count_solids(candidate) != 1 or current < 1e-6:
                raise ValueError("Feature must leave one valid solid with positive volume.")
            tolerance = max(1e-6, previous*1e-9)
            if shape is not None and abs(current-previous) <= tolerance:
                raise ValueError("Feature does not change the solid. Check plane offset, direction and intersection.")
            if operation == "cut" and current >= previous-tolerance:
                raise ValueError("Cut did not remove material.")
            shape = candidate
            history.append({"index":index,"name":str(feature.get("name","")),"volumeMm3":current})
        except Exception as error:
            raise ValueError("Feature %d (%s): %s" % (index+1, feature.get("name",""), error)) from error
    return shape, {"volumeMm3":volume(shape),"featureCount":len(features),"solidCount":count_solids(shape),"history":history,"sketchPlanes":sketch_planes(shape)}
