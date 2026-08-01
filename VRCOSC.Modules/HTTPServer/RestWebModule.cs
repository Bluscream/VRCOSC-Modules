// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Tasks;
using EmbedIO;

namespace Bluscream.Modules.HTTPServer;

/// <summary>
/// Hands every non-MCP request to <see cref="HTTPServerModule.HandleRequest"/>, which
/// keeps CORS, bearer auth, request accounting, and routing in one place.
///
/// Registered after <c>McpWebModule</c> so <c>/mcp</c> is claimed first; this module
/// is the catch-all for the REST API and docs.
/// </summary>
internal sealed class RestWebModule : WebModuleBase
{
    private readonly HTTPServerModule _module;

    public RestWebModule(string baseRoute, HTTPServerModule module) : base(baseRoute)
    {
        _module = module;
    }

    public override bool IsFinalHandler => true;

    protected override Task OnRequestAsync(IHttpContext context) => _module.HandleRequest(context);
}
