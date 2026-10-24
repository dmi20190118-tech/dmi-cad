# DMI CAD development agreement

The user approved this workflow on 2026-10-24. Apply it to every future request in this repository. These are project instructions; do not assume chat memory outside this repository.

## Mandatory order for each feature

1. Define purpose and workflow.
   - Translate the request into the user's concrete task and expected result.
   - Place it in the workflow: open file → inspect model → navigate → measure → edit/model → save or convert.
   - State acceptance criteria, data requirements, supported formats and limits. Ask only for genuinely missing information; make routine reversible decisions autonomously.
2. Define interaction.
   - Specify selection behavior, mouse actions, keyboard shortcuts, right-click actions, UI location and useful error/recovery behavior.
   - Use SOLIDWORKS default interaction conventions as the reference, including middle-button orbit, Ctrl+middle pan, Shift+middle zoom, wheel zoom and middle-button double-click fit.
   - Do not claim full compatibility without comparison testing. Reuse existing interaction conventions across features.
3. Implement and verify.
   - Implement real behavior and revise its UI together.
   - Run relevant geometry, import/export, interaction and Windows build checks.
   - Record what passed and what remains unverified. Provide a reviewable working result or clearly explain a concrete blocker.
   - Keep unsupported capabilities visibly distinct from working tools. Never substitute a visual mockup for working functionality.

## Product direction

Build toward a professional Windows 3D CAD/CAM application through tested increments. A familiar UI is a starting point, not proof of equivalent capabilities.

Near-term priorities:
1. Reliable file import with accurate geometry, clear units and no silently omitted entities.
2. Consistent viewport navigation and selection of faces, edges and vertices.
3. Reliable length, distance and angle measurements.
4. 2D sketches and constraints, then solid features and surface modeling.
5. Later CAM/NC verification, holder interference and air-cut optimization, with explicit verification limits.

## Quality requirements

- Preserve original files; perform conversion through separate outputs and avoid corrupting existing outputs on failure.
- Make coordinate systems, units and tolerances explicit where they affect user decisions.
- Preserve a responsive UI and report loading/conversion errors usefully.
- Use bounded processing for large/malformed files.
- Keep project save/load and version compatibility deliberate as the data model evolves.
- Verify native file support by format and version using suitable test files.
- Proprietary SOLIDWORKS, CATIA, Cimatron/Elite/PFM and DWG readers need actual translator support and applicable licensing. An extension filter is not support.
- Current STEP/IGES/STL/basic DXF support does not imply support for proprietary CAD originals, assemblies, edit history, PMI or materials.
- Do not promise complete SOLIDWORKS parity. Describe current capabilities and limitations precisely.
- Keep a concise record of changes, validation and next dependencies in user-facing handoffs.

## How to handle new requests

Interpret even short feature requests in the context of this agreement. Do not make the user repeat the 1–2–3 process. Briefly explain the purpose/interaction decisions when useful, then proceed within the authorized scope. Ask approval only for actions that actually require it, including paid SDK commitments or genuinely destructive operations.
