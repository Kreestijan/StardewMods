# Stardew Valley modding rules

## Goal
Build maintainable Stardew Valley mods using the safest supported integration points first, with the lowest practical risk of breakage, conflicts, and maintenance burden.

## Required workflow
Before writing code:
1. Inspect official Stardew/SMAPI modding documentation relevant to the task.
2. Inspect the current repo structure and existing implementation patterns.
3. Check whether a standard SMAPI API, event, content edit, asset edit, or data-driven approach already solves the problem.
4. Check whether an established Stardew framework or existing mod ecosystem already provides the needed capability.
5. Only consider reflection or Harmony if supported hooks are insufficient.

Do not jump straight to Harmony.

## Architecture priority
Always prefer, in this order:
1. SMAPI public APIs and events
2. Content/data-driven modding approaches
3. Asset editing / data injection / config-driven approaches
4. Existing repo patterns and project-local abstractions
5. Established Stardew frameworks that are actively used and well-supported
6. Reflection, only if necessary
7. Harmony patches as a last resort

## Framework and ecosystem check
For any feature request, check whether the need is already solved cleanly by:
- SMAPI itself
- Content Patcher
- Generic Mod Config Menu
- Expanded Preconditions Utility
- Json Assets or successor/appropriate content frameworks if relevant
- SpaceCore if a feature genuinely depends on it
- Farm Type Manager or other established ecosystem tools if relevant
- another actively maintained mod/framework commonly used for that exact problem

Before building a custom system, determine:
- whether an existing framework already does it,
- whether depending on that framework is reasonable,
- whether reusing that ecosystem approach is safer than bespoke code.

If a framework/mod already solves the problem cleanly, prefer integration over rebuilding unless the task explicitly requires a standalone implementation.

## Harmony policy
Harmony is a last resort.

Do not introduce Harmony unless you can explain all of the following:
- which official docs or APIs were checked first,
- why SMAPI events/APIs/content hooks are insufficient,
- whether an existing framework/mod already solves the problem,
- why that framework/mod is not suitable here if it exists,
- which exact game method must be patched,
- why the patch target is the narrowest viable point,
- what compatibility and maintenance risks this adds.

Any solution using Harmony without that written justification is invalid.

## Harmony implementation rules
If Harmony is truly necessary:
- patch the narrowest possible method,
- avoid broad or global patches,
- avoid patching hot paths unless unavoidable,
- prefer postfix/prefix over transpilers when feasible,
- use transpilers only when simpler patches cannot work,
- document the reason directly above the patch,
- note expected failure modes and compatibility risks,
- keep patch logic minimal and delegate real logic to normal classes,
- avoid patching the same behavior in multiple places unless required.

## Stardew-specific decision ladder
When solving a task, explicitly check these questions in order:
1. Can this be done with a SMAPI event?
2. Can this be done by editing or supplying game data/assets?
3. Can this be done with Content Patcher or another established framework?
4. Can this be done by extending the current repo’s architecture cleanly?
5. Can this be done with limited reflection?
6. Is Harmony truly unavoidable?

Do not skip steps.

## Compatibility and versioning rules
- Be careful about Stardew and SMAPI version compatibility.
- Prefer public, documented APIs over internal implementation details.
- Be cautious with game internals that may change across updates.
- Avoid relying on undocumented behavior unless there is no practical alternative.
- If a task touches a version-sensitive area, call that out explicitly.

## Mod interoperability
Assume other mods may be present.
- Avoid invasive behavior when a local change will do.
- Prefer approaches that compose well with other mods.
- Minimize conflict-prone patches.
- Note likely compatibility issues when they exist.
- If using Harmony, consider how multiple mods might patch the same method.

## Logging, diagnostics, and failure behavior
- Use clear, minimal logging for important state changes and failures.
- Do not spam logs.
- Fail gracefully where possible.
- Surface misconfiguration clearly.
- Prefer predictable fallback behavior over crashes when safe.

## Output requirements
Before implementation, provide:
- docs inspected,
- repo files inspected,
- frameworks/mods considered,
- options considered,
- chosen approach,
- why it is the safest maintainable option,
- whether Harmony is avoidable.

After implementation, provide:
- what changed,
- why this approach was selected,
- validation performed,
- unresolved risks or compatibility notes.

## Hard constraints
- Do not use Harmony by default.
- Do not invent SMAPI APIs or Stardew internals.
- Do not rebuild framework functionality without first checking whether that framework already solves the problem.
- If official docs are unclear and the solution would require risky internals, stop after analysis and report uncertainty instead of forcing an unsafe implementation.
