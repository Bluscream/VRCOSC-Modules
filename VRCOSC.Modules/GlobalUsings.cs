// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

#region COMPAT — VRCOSC dual-SDK support (see AGENTS.md §3, §8)
// ---------------------------------------------------------------------------------------
// EVERYTHING IN THIS REGION EXISTS ONLY TO SPAN TWO VRCOSC NODE APIs. It is temporary by
// design. See the matching region in BetaSdkShims.cs — those two files are the ONLY places
// compat code lives, deliberately, so retiring a generation is a two-file edit.
//
//   stable (SDK 2026.501.0)  concrete PulseContext, ModuleNode<T>, FlowContinuation,
//                            FlowCall, IFlowInput marker, NodeGenericTypeFilter
//   beta/dev (2026.702.0+)   IPulseContext, IModuleNode<T>, FlowOutput, FlowInput,
//                            FlowInput *field*, NodeGenerics
//
// A DLL only loads on the generation it was compiled against — mixing them throws
// TypeLoadException at package import and leaves VRCOSC with ZERO modules. Build with
// -p:VrcoscTarget=stable|beta|dev.
//
// WHEN BETA BECOMES STABLE — the retirement procedure:
//   1. Delete every `#else` arm below (the stable spellings) and the `#if BETA_SDK` /
//      `#endif` lines, leaving the beta spelling unconditional.
//   2. Delete the aliases that are beta-only no-ops once stable catches up
//      (FlowContinuation, FlowCall, NodeGenericTypeFilterAttribute) and rename their usages
//      to the real names — or keep them as permanent aliases if you prefer the old names.
//   3. Do the same in BetaSdkShims.cs (§ its own region).
//   4. Drop the `stable` arm from Bluscream.Modules.csproj and update AGENTS.md §2/§3.
// Nothing outside these two files needs touching — no node file has a #if in it.
// ---------------------------------------------------------------------------------------

#region COMPAT: PulseContext → IPulseContext
// The node processing context. This is the change that makes the two generations
// fundamentally incompatible: Node.Process is *abstract* and takes PulseContext on stable
// but IPulseContext on beta, so a compiled override can only ever satisfy one of them.
// That is why a single universal DLL is impossible — see AGENTS.md §8.
#if BETA_SDK
global using PulseCtx = VRCOSC.App.Nodes.IPulseContext;
#else
global using PulseCtx = VRCOSC.App.Nodes.PulseContext;
#endif
#endregion

#region COMPAT: event pins (FlowCall → FlowOutput)
// Event-style flow outputs (the "On..." pins a node fires with .Execute(c)).
//
// Stable models these as FlowCall. beta/dev moved Execute onto the new FlowOutput type,
// so the same field changes type per target.
#if BETA_SDK
global using FlowEvent = VRCOSC.App.Nodes.FlowOutput;
#else
global using FlowEvent = VRCOSC.App.Nodes.FlowCall;
#endif
#endregion

#region COMPAT: continuation pins (FlowContinuation → FlowOutput)
// The "Next" / "On Error" pins a node fires to carry the flow on.
//
// Stable calls this FlowContinuation; beta/dev renamed it FlowOutput, keeping the same
// shape — FlowOutput(string name = "", bool scope = false) with Execute(IPulseContext) —
// so `new("Next")` and `.Execute(c)` work unchanged on both. A pure rename, hence a pure
// alias, and no #else arm is needed because stable already has the real type.
//
// FlowEvent above ALSO maps to FlowOutput on beta/dev. Not a mistake: stable modelled
// event pins and continuation pins as two distinct types (FlowCall and FlowContinuation),
// and beta/dev merged both into FlowOutput.
#if BETA_SDK
global using FlowContinuation = VRCOSC.App.Nodes.FlowOutput;
#endif
#endregion

#region COMPAT: flow input pins (FlowCall → FlowInput)
// The "Call" pin that triggers a node. Stable spells it FlowCall, beta/dev FlowInput;
// same role, same `new()` usage. Mirror of FlowEvent above: stable's FlowCall was an
// input and stable's FlowContinuation was an output.
#if BETA_SDK
global using FlowCall = VRCOSC.App.Nodes.FlowInput;
#endif
#endregion

#region COMPAT: generic type filters (NodeGenericTypeFilter → NodeGenerics)
// Restricts which T a generic node accepts.
//
//   stable  [NodeGenericTypeFilter([typeof(a), typeof(b)])]   NodeGenericTypeFilterAttribute
//   beta    [NodeGenerics(typeof(a), typeof(b))]              NodeGenerics(params Type[])
//
// A straight rename with a compatible signature, so this alias lets the existing attribute
// usages stand unchanged — C#'s attribute shorthand resolves [NodeGenericTypeFilter(...)]
// through the "...Attribute" alias name.
//
// The array form `[...]` is kept at the usage sites because it binds to both stable's
// Type[] parameter and beta's params Type[]. An earlier attempt used a (index, types)
// overload under BETA_SDK; that overload only ever existed on the feat/cli FORK, never on
// any released beta, so it is gone. Don't reintroduce it.
#if BETA_SDK
global using NodeGenericTypeFilterAttribute = VRCOSC.App.Nodes.NodeGenerics;
#endif
#endregion

#endregion
