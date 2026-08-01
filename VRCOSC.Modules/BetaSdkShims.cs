// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

// Shims that let one source tree compile against both VRCOSC node APIs.
//
// The stable SDK (2026.501.0) ships a concrete base class that resolves the module for
// you and gates processing on it being running:
//
//     public abstract class ModuleNode<T> : Node where T : Module
//     {
//         public T Module => (T)ModuleManager.GetInstance().GetModuleInstanceFromType(typeof(T));
//         protected override bool ShouldProcess(PulseContext c) => ...IsModuleRunning(...);
//     }
//
// The beta/dev SDKs dropped it and kept only the interface IModuleNode<T>, which declares
// `T Module { get; set; }` and nothing else - the app now injects Module rather than the
// node pulling it. Upstream's own modules therefore spell every node out longhand:
//
//     public sealed class FooNode : Node, IModuleNode<FooModule> { ... }
//
// Doing that here would mean rewriting 54 node declarations and hand-implementing the
// INodeElement members the old base class supplied - which is what produced the ~360
// CS0246/CS0535 errors on the beta target. Re-declaring the base class ourselves gets the
// same result without touching a single node.
//
// It lives in VRCOSC.App.SDK.Nodes deliberately: every node file already has
// `using VRCOSC.App.SDK.Nodes;`, so the type resolves exactly where ModuleNode<T> used
// to come from and no per-file edits are needed. Guarded by BETA_SDK so it never
// collides with the real class on the stable target.
//
// Note this is the same fix as commit c8ee8577 on the feat/cli fork of VRCOSC itself,
// except applied to the module instead of the app - so it needs no patched VRCOSC.

namespace VRCOSC.App.SDK.Nodes;

using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;

#region COMPAT — node base classes spanning both VRCOSC SDKs (see AGENTS.md §3, §8)
// ---------------------------------------------------------------------------------------
// TEMPORARY BY DESIGN. Together with the COMPAT region in GlobalUsings.cs, this is the
// ONLY place compat code lives — no node file contains a #if. Retiring a generation is
// therefore a two-file edit; see the procedure in GlobalUsings.cs.
// ---------------------------------------------------------------------------------------

#region COMPAT: ModuleNode<T> — DELETE ENTIRELY when beta becomes stable
// Stable ships `ModuleNode<T> : Node` as a real base class. beta/dev dropped it and kept
// only the interface IModuleNode<T> (which declares `T Module { get; set; }` and nothing
// else), so upstream's own modules spell nodes out longhand as
// `: Node, IModuleNode<FooModule>`.
//
// Re-declaring the base class here gives all 54 nodes their base back with zero per-file
// edits. Once beta is stable, either delete this and rewrite the declarations longhand,
// or keep it as a permanent convenience base — but then drop the #if and move it out of
// this compat region, because it is no longer compatibility code.
#if BETA_SDK
using VRCOSC.App.Nodes.Types;

public abstract class ModuleNode<T> : Node, IModuleNode<T> where T : Module
{
    public T Module { get; set; } = null!;
}
#endif
#endregion

#region COMPAT: FlowModuleNode<T> — KEEP the beta arm, delete the #else when beta is stable
// A module node that is driven by an incoming flow pin. 30 of the 54 nodes use this.
//
// Unlike the aliases, this one cannot simply be deleted later: the beta arm carries a real
// `FlowInput` field that the nodes need. When beta becomes stable, keep the beta arm,
// delete the `#else` arm and the #if/#endif, and move this out of the compat region — it
// becomes an ordinary base class.
//
// The two SDKs express this in fundamentally different ways, and there is no spelling
// that compiles on both:
//
//   stable   the node implements the bare marker interface `IFlowInput`
//            (literally `public interface IFlowInput;`) and the app infers the pin.
//            There is no FlowInput type at all on this line.
//
//   beta/dev `IFlowInput` still exists but means something completely different - it was
//            repurposed into the pin hierarchy (IFlowInput : IFlowInputBase :
//            IFlowElement : INodeElement), so putting it on a Node now demands the whole
//            INodeElement surface. Flow input is instead declared as a *field*, matching
//            upstream's own AsyncActionNode: `public FlowInput FlowInput = new();`
//
// Because one form needs an interface and the other needs a field, an alias cannot bridge
// them - only a base class can. Nodes therefore derive from FlowModuleNode<T> instead of
// writing `: ModuleNode<T>, IFlowInput`, and each target supplies the right mechanism.
#if BETA_SDK
public abstract class FlowModuleNode<T> : ModuleNode<T> where T : Module
{
    public FlowInput FlowInput = new();
}
#else
public abstract class FlowModuleNode<T> : ModuleNode<T>, IFlowInput where T : Module;
#endif
#endregion

#endregion
