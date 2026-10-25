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

## Existing company branding (2026-10-24)

Use the user's existing original DMI logo. The user said TMI in speech, but the located original is DMI로고.png and the explained initials are Design, Mold, Injection. Do not invent a new TMI logo or silently substitute unrelated branding. If the user later supplies a different mark, clarify and use the designated original.

Asset source: https://drive.google.com/file/d/1oo4KLBoZrp6ecmCcKki18gxx8SVH5LFM/view
Original image: https://static.wixstatic.com/media/b61e9b_4c68109deaca422494f28c350edb38de~mv2.png
Desktop embedded original bytes: Brand.cs.

The mark contains orange D (Design), green M (Mold), blue I (Injection). Preserve original pixels, aspect ratio, labels and colors; do not redraw, crop, stretch or recolor it. Place on a contrasting, preferably white, background so labels remain legible. Use across application headers, help, instruction sheets and educational materials. Apply the identity without replacing functional engineering UI with decorative branding.

## 0.3 exact modeling implementation (2026-10-25)

Default Windows window is now ModelingWindow, using PartDocument/PartFeature/PartSketch and engine/parametric.py. Exact OCCT solids are regenerated from versioned mm-based .dmipart feature projects.

Implemented bounded scope: principal XY/XZ/YZ planes, offsets/depths, circles and closed polygons (including dimension-defined rectangles), additive extrusion and boolean cuts, single-solid validation and actual volume changes, feature edits/deletion/recomputation, undo/redo snapshots, save/reopen and STEP/IGES/STL output. The existing mesh viewport displays tessellation; exact BRep operations occur in the engine. Basic model name/revision/mesh bounds can seed a manual machining-sheet draft.

This is not a general sketch constraint solver, face-attached sketching, topology selection/measurement, fillet/surface suite, drafting, assembly, mold design, CAM/NC, or automatic sheet synchronization. Keep those absent capabilities explicit.

See docs/MODELING-0.3.ko.md and examples/plate.dmipart. CI checks analytic volumes, edited volume, STEP reread, principal planes, invalid/self-intersecting/disconnected/no-effect features, document snapshots, and actual GUI generation of a three-feature part. preview/modeler-0.3.png is captured from that GUI when CI succeeds. Do not describe those checks as manual mouse testing or production machining validation.

## 0.3.1 interface direction and staged interaction (2026-10-25)

The user explicitly requested SOLIDWORKS 2020 default interface as a reference and instructed us to verify and proceed without requiring user screenshots. Official 2020 docs were researched for CommandManager, Manager Pane, PropertyManager, Heads-up View and default Workspace.

ModelingWindow is now partial. ModelingShell.cs owns the native menu, quick tools, command tabs below icon groups, manager tabs, staged property confirmation/cancel, context editing and view toolbar. Own DMI logo and own drawn icons remain. Existing exact geometry engine and principal-plane coordinates remain unchanged.

Do not revert to a flat wall of large buttons or permanently expose all property fields beside the tree. Do not describe unavailable fillet/drafting/assembly/mold/CAM as working. Do not claim pixel-identical UI or full SOLIDWORKS behavior.

CI additionally exercises sketch completion, property pane activation, feature confirmation, cancellation preserving committed model, undo/redo and feature filtering in the real Windows GUI. It captures model and property screens. This is not exhaustive manual mouse/accessibility/DPI testing or a side-by-side audit of a running SOLIDWORKS installation. Documentation: docs/INTERFACE-0.3.1.ko.md.

## 0.3.2–0.3.3 extrusion modes and project compatibility

0.3.2 added Reverse for principal-plane extrusion/cut. 0.3.3 adds Midplane: total Depth extends half in each direction around the offset plane. Reverse is retained but irrelevant/disabled while Midplane is enabled. Native UI confirm/cancel/undo/redo and exact STEP bounds/volume tests cover these settings.

0.3.3 saves project version 2; readers accept v1 and v2 and upgrade v1 in memory. Earlier applications reject v2 instead of silently ignoring new geometry semantics. Preserve originals until explicit save. Do not revert the version to v1. The standalone geometry engine accepts v1/v2. New behavior is documented in docs/MODELING-0.3.3.ko.md. No live geometry preview or general constraint/face selection was added by these modes.

## 0.3.4 through-all and 1.0 acceptance
ThroughAll is a one-direction cut from the sketch plane to beyond the preceding exact solid bounds. It is not bidirectional. Reject additive/midplane/missing-solid combinations. Recompute after upstream edits. Numeric plane offset is not a face-linked reference. Projects now save v3 and read v1/v2/v3; never downgrade new files to older versions.
docs/ACCEPTANCE-1.0.ko.md records the user's required workflow and remaining face selection/constraints gaps. Do not call this incremental build 1.0 or claim the whole acceptance scenario passed while face-attached sketching is absent.

## 0.3.5 exact face-plane snapshots
SketchPlaneChoice lists current exact planar OCCT faces parallel to XY/XZ/YZ with offset, area, outward orientation and center. The native sketch command opens a list and starts a circle/rectangle sketch by copying plane/offset and initial inward/outward direction. This is a numeric snapshot, NOT an associative face attachment. No viewport clicking/highlight or tilted/curved surface sketches. Regeneration replaces the list and rejects stale objects. Project v3 unchanged. Help: docs/MODELING-0.3.5.ko.md.

## 0.4.0 viewport face picking
Native model display uses OCCT tessellation tagged with exact face indices. The same z-buffer display/pick projection chooses the frontmost face; curved/tilted faces occlude but cannot start principal-plane sketches. Selection highlights orange. Commands: screen face select → click → selected face sketch; Esc clears selection. Keep plane snapshot limits explicit: no associative references yet. Project v3 unchanged. docs/MODELING-0.4.0.ko.md records scope and test limits.
The user authorized continuing toward 1.0 without asking per-version permission. Keep working within that goal, and do not label incomplete milestones 1.0.

## 0.4.1 semantic end-plane references
PlaneSourceId stores the stable ID of an earlier one-direction additive extrusion in the same principal plane. Linked offset is recomputed from the source's resolved offset plus signed depth. This references a semantic end plane, not arbitrary persistent face topology or XY sketch placement. A selected face only auto-links when exactly one eligible source end plane matches position/orientation; otherwise snapshot. Reject missing, forward, cyclic, mismatched-plane and midplane sources. Source deletion/invalid edit must preserve the committed model.
Projects now save v4 and read v1–v4. Prior releases must reject v4. Link state/source name shown in properties, plane/offset locked while linked, uncheck to freeze current displayed offset. docs/MODELING-0.4.1.ko.md.

## Bundled 1.0 development preview direction
The user rejected tiny incremental releases and explicitly wants the 1.0 development bundle. Use 1.0.0-preview.1 / visible "1.0 개발판" rather than falsely declaring stable 1.0. New FeatureKind values extrude/revolve/fillet, AngleDegrees, FilletRadius, project v5. Preserve v1–v4 reads.
Revolve uses local vertical V axis at U=0; reject profiles crossing U<0, midplane and through-all. Only extrusions can be end-plane sources. Fillet is all-edge OCCT fillet, NOT selected-edge fillet; reject failed/invalid/nonchanging results without changing committed body.
AdvancedModeling.cs contains visible demos and real GUI regression. Default CI captures rounded plate/boss/holes plus a separate turned hollow shaft. Do not repeat tiny .1 release handoffs as if 1.0 were complete. General constraint solver, arcs, precision measuring, drawing/assembly remain incomplete.

## Core workflow regression priority — 1.0 preview 2
The user reports plane→2D sketch→boss→face→new sketch→boss/cut does not work smoothly. Fix this workflow before adding breadth. Root causes found: Begin cleared face plane linkage/reverse/editing state on each sketch-tool switch; normal face clicks ignored unless special mode armed; selected face sketch forced circle/cut. CoreSketchWorkflow.cs manages a sketch session, face outward normal and actual mouse-event regression with analytic volume checks.
Preserve face plane/reference when changing sketch tools and preserve existing-feature edit intent. Normal idle model clicks can select a supported face. Boss defaults outward, cut inward for a face-started sketch. End/cancel/commit clears session. Reference face mesh is visual context only, not precision snapping/constraints.
Do not claim an actual user's desktop mouse test: CI calls control mouse events. New screenshots show both 2D face sketch and resulting boss/cut. No schema change (v5).
