# Bundled CAD engine

The application starts engine/cad-engine.exe as a separate local process.
Original engine Python sources are shipped in engine/source, dependency versions in engine/dependencies.txt, and upstream license notices in engine/licenses.

- cadquery-ocp bindings: https://github.com/CadQuery/OCP — LGPL-2.1
- Open CASCADE Technology: https://dev.opencascade.org/release — LGPL-2.1 with the Open CASCADE exception. License and source: https://github.com/Open-Cascade-SAS/OCCT
- VTK: https://vtk.org — BSD-3-Clause. Source: https://gitlab.kitware.com/vtk/vtk
- Python: https://www.python.org — PSF License
- PyInstaller bootloader: https://pyinstaller.org — GPL with bootloader exception

These components remain under their respective upstream licenses. They are distributed without modification. Users may replace the separate engine and its libraries with compatible builds, and may debug or modify LGPL-covered components as permitted by their licenses. The engine interface is two command-line arguments: source path and target path. Engine source is also in this repository under engine/.

This program does not bundle proprietary translators for SOLIDWORKS, CATIA, Cimatron or DWG and is not affiliated with those products.
