"""Local STEP/IGES reader and translator. No uploads or network access."""
import argparse
from pathlib import Path
from OCP.STEPControl import STEPControl_Reader, STEPControl_Writer, STEPControl_AsIs
from OCP.IGESControl import IGESControl_Reader, IGESControl_Writer
from OCP.IFSelect import IFSelect_RetDone
from OCP.BRepMesh import BRepMesh_IncrementalMesh
from OCP.StlAPI import StlAPI_Writer

def convert(source, target):
    ext = Path(source).suffix.lower()
    metrics = None
    if ext == ".dmipart":
        from parametric import build_part
        shape, metrics = build_part(source)
        write_shape(shape, target)
        import json
        Path(str(target) + ".metrics.json").write_text(json.dumps(metrics), encoding="utf-8")
        return
    if ext in (".step", ".stp"):
        reader = STEPControl_Reader()
    elif ext in (".iges", ".igs"):
        reader = IGESControl_Reader()
    else:
        raise ValueError("Only STEP and IGES source files are supported by this engine.")
    if reader.ReadFile(str(source)) != IFSelect_RetDone:
        raise ValueError("CAD file could not be read.")
    if reader.TransferRoots() == 0:
        raise ValueError("CAD file contains no transferable geometry.")
    shape = reader.OneShape()
    if shape.IsNull():
        raise ValueError("CAD file contains no shape.")
    write_shape(shape, target)

def write_shape(shape, target):
    output = Path(target).suffix.lower()
    if output == ".stl":
        mesh = BRepMesh_IncrementalMesh(shape, 0.1, False, 0.35, True)
        mesh.Perform()
        if not mesh.IsDone():
            raise ValueError("Tessellation failed.")
        writer = StlAPI_Writer()
        writer.ASCIIMode = False
        if not writer.Write(shape, str(target)):
            raise ValueError("STL output failed.")
    elif output in (".step", ".stp"):
        writer = STEPControl_Writer()
        if writer.Transfer(shape, STEPControl_AsIs) != IFSelect_RetDone:
            raise ValueError("STEP transfer failed.")
        if writer.Write(str(target)) != IFSelect_RetDone:
            raise ValueError("STEP output failed.")
    elif output in (".iges", ".igs"):
        writer = IGESControl_Writer("MM", 0)
        if not writer.AddShape(shape) or not writer.Write(str(target)):
            raise ValueError("IGES output failed.")
    else:
        raise ValueError("Unsupported output extension.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("target")
    args = parser.parse_args()
    convert(args.source, args.target)
