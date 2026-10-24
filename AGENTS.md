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

## Joint NC development objective (2026-10-24)

The user explicitly wants NCBrain-like NC verification and machining-time optimization developed alongside CAD. Treat this as a continuing product objective, not an unrelated future idea. Apply the same purpose → interaction → implementation/verification workflow.

NCBrain is a research/comparison reference, not a source of copied implementation or assets. Research public documentation when defining specific features and cite sources; distinguish documented product behavior from hypotheses and implemented functionality. Do not claim that research has occurred unless it actually has.

Stage the work:
1. Obtain representative NC programs and identify target controller dialect, machine axes/kinematics, tool/holder geometry, stock/part/fixture models, work offsets and units.
2. Parse supported NC commands and simulate modal state and tool motion. Report unsupported commands, macros and cycles explicitly; never silently approximate them as safe moves.
3. Estimate machining time with explicit machine limits and acceleration assumptions. Distinguish estimates from actual measured cycle times.
4. Track stock removal and identify air motion with the changing remaining stock. Tool travel outside the finished part alone does not prove air cutting.
5. Check cutter/holder/stock/part/fixture interference using coordinate-consistent geometry. Intentional cutter-stock contact is cutting, not automatically a collision. Machine-component collisions additionally require machine geometry and kinematics.
6. Introduce reviewed air-motion/feed optimization only after simulation and validation support it. Faster rapid travel through nominally empty space is not inherently safe. Respect entry/exit motion, controller limits, tool/material/load constraints, and tolerances.
7. Preserve original NC files. Show proposed changes and compare before/after geometry, time estimates, assumptions and remaining uncertainties before exporting a separate optimized file. Never send generated NC directly to a machine.

Initial scope should be one controller and a 3-axis machine unless the user's actual equipment supports a different bounded choice. Do not claim production-grade collision avoidance or a guaranteed time reduction from a prototype. Actual machining release needs appropriate independent verification and machine procedures.

## Integrated engineering platform direction (2026-10-24)

The user clarified that the product is one extensible engineering application with separate workspaces, sharing model data:
- Part CAD: accurate solid/surface modeling, sketches, constraints and feature editing.
- Drafting: associative drawing views/sections/details, dimensions/tolerances and drawing output.
- Assembly: multiple components, instances, placement, mating constraints and interference checks.
- Mold design: opening/draw direction, draft/undercut analysis, parting lines/surfaces, core/cavity separation, slider/lifter design and placement, with later mold-base/ejection/cooling support.
- CAM/NC: tool/holder library, setups/stock/fixtures, machining operations, toolpaths, controller/machine-specific postprocessing and NC output, then NC verification/optimization described above.
- Simulation/extensions: allow later validated structural, motion, thermal or mold-flow capabilities through explicit modules. Each domain requires its own solver, inputs, licenses and validation.

CAD foundation is the first delivery priority. A workspace tab does not establish that its functionality exists. Do not label this roadmap as an already complete professional CAD or a guarantee of full SOLIDWORKS equivalence. Implement a bounded validated subset and expand.

Architectural priorities:
- One document/project model for geometry, units, coordinate frames, stable identifiers, feature history, component instances and file versions.
- Shared navigation/selection, commands, undo/redo, persistence and geometry services.
- Versioned interfaces for modules and explicit capability availability; avoid prebuilding speculative solver frameworks.
- Drafting/assembly/mold/CAM should reference shared source geometry rather than unrelated duplicated meshes.
- Imported tessellated viewer geometry is not an editable precise boundary representation. Preserve or implement access to exact geometry before supporting precision modeling and associative drawings.
- Define change propagation and stale-result invalidation: when source geometry changes, mark dependent drawings, mold features, toolpaths and simulation results for regeneration/revalidation.
- Core/cavity separation requires actual parting geometry and valid solid operations; slider placement requires undercut/access/motion checks. Do not present generic boolean cuts as a complete mold-design solution.
- Reserve workspace organization when justified, but make unavailable capabilities explicit and do not build misleading nonfunctional controls as completed features.

Delivery progression: exact CAD foundation → drafting and assembly foundations → mold workflows → validated CAM/NC generation/verification → specialist simulation extensions, adjusting bounded work to user priorities and dependencies.

## Machining instruction sheets (2026-10-24)

The user requires operator-facing machining/NC setup sheets automatically populated from CAM/NC work using their existing Excel template. They will supply the template, cutting/machining-condition data and dimensions later.

Preserve the future Excel layout and print settings when the actual template is supplied. Define explicit cell mappings, units, source references and condition-table versions; never invent template cells or acceptable process limits.

Populate sheets from the same authoritative operation/tool/setup data used to generate NC, then compare relevant fields against exported NC modal state. Show missing data, mismatches and out-of-range conditions. Numeric syntax or a user-entered maximum does not establish safe machining. Reference limits must match material, cutter, holder, machine and operation context.

Track drawing/NC revisions, work coordinates, tool offsets, units, rotation/feed modes, dimensions/tolerances and approval state. Changed NC/model/conditions must mark sheets stale and require revalidation. Intended future outputs include the user's actual Excel sheet and printed/PDF operator instructions.

Current initial prototype is a manually populated single-operation instruction draft with required/numeric checks, optional reference maximum checks, Vc/fz consistency checks and printable HTML. It is NOT NC parsing, automatic synchronization, dimensional NC verification, Excel template integration or a machining-safety approval system. Clearly state these limits. Do not use a green all-safe status when evidence is missing.

## User clarification: prioritize real CAD modeling (2026-10-24)

The user rejected treating the mobile machining-sheet demo or a file viewer as the requested CAD. Prioritize directly drawing sketches, dimensional/geometric constraints, genuine solid extrusion/cuts, face/edge/vertex selection, feature edits, undo/redo and project persistence. A parametric display cuboid is not completion of this goal. Keep manufacturing/documentation work connected to this core rather than allowing peripheral previews to replace it.

## Help and training are product deliverables (2026-10-24)

The user wants other users to understand purpose and operation from documentation and videos without repeated personal explanations. Treat this as a continuing requirement for every feature.

Entry point: docs/README.md.
Current authored deliverables:
- docs/USER-GUIDE.ko.md: beginner handbook of actual current features and troubleshooting.
- docs/PRODUCT-AND-TRAINING.ko.md: product purpose, extensibility and detailed help/training standard.
- docs/VIDEO-PLAN.ko.md: tutorial structure/scripts, not finished videos.

For each feature, document purpose, use case, prerequisites, exact UI entry, step-by-step actions, units/options, expected result, mistakes/recovery, scope/limits, version/build and example files. Explain unfamiliar terminology at first use and provide a glossary. Error messages should identify what is wrong, why and how to fix it.

Update help alongside code and use real screenshots/recordings of the shipped behavior. Provide task-based short videos with Korean captions, cursor/key indicators, examples and clear expected outcomes. Distinguish planned tutorials from completed video artifacts. Do not film unreleased/nonfunctional tools as working demonstrations.

Product help/F1 links, PDF handbook and actual tutorial videos are not yet implemented merely because Markdown and video plans exist. Keep their status accurate. Before presenting a workflow as ready for independent users, have a novice follow it and record/correct points requiring extra explanation. Do not promise universal first-pass comprehension without such testing.

Follow the same purpose → interaction → implementation/verification process for documentation and instructional media. Preserve the development purpose and extension roadmap separately from current user instructions.
