import importlib.metadata
import shutil
import sys
from pathlib import Path

target = Path(sys.argv[1])
target.mkdir(parents=True, exist_ok=True)
for distribution in importlib.metadata.distributions():
    name = distribution.metadata.get("Name", "unknown")
    for file in distribution.files or []:
        if any(word in file.name.lower() for word in ("license", "copying", "copyright", "notice")):
            source = Path(distribution.locate_file(file))
            if source.is_file():
                destination = target / name / str(file).replace("..", "_")
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
python_license = Path(sys.base_prefix) / "LICENSE.txt"
if python_license.exists():
    shutil.copy2(python_license, target / "PYTHON-LICENSE.txt")
