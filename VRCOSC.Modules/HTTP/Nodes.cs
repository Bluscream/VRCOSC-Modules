// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;
using Bluscream;

namespace Bluscream.Modules;

public enum HttpMethod
{
    GET,
    POST,
    PUT,
    PATCH,
    DELETE,
    HEAD,
    OPTIONS
}

[Node("HTTP GET Request")]
public sealed class HTTPGetRequestNode : ModuleNode<HTTPModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Url = new("URL");
    
    public ValueOutput<string> ResponseBody = new("Response Body");
    public ValueOutput<int> StatusCode = new("Status Code");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var url = Url.Read(c);
            
            if (url.IsNullOrEmpty())
            {
                Error.Write("URL is required", c);
                await OnError.Execute(c);
                return;
            }

            var response = await Module.SendRequest("GET", url);
            
            ResponseBody.Write(response.Body, c);
            StatusCode.Write(response.StatusCode, c);

            if (response.Success)
            {
                await Next.Execute(c);
            }
            else
            {
                Error.Write($"HTTP {response.StatusCode}: {response.Body}", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("HTTP POST Request")]
public sealed class HTTPPostRequestNode : ModuleNode<HTTPModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Url = new("URL");
    public ValueInput<string> Body = new("Request Body");
    
    public ValueOutput<string> ResponseBody = new("Response Body");
    public ValueOutput<int> StatusCode = new("Status Code");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var url = Url.Read(c);
            var body = Body.Read(c);
            
            if (url.IsNullOrEmpty())
            {
                Error.Write("URL is required", c);
                await OnError.Execute(c);
                return;
            }

            var response = await Module.SendRequest("POST", url, body);
            
            ResponseBody.Write(response.Body, c);
            StatusCode.Write(response.StatusCode, c);

            if (response.Success)
            {
                await Next.Execute(c);
            }
            else
            {
                Error.Write($"HTTP {response.StatusCode}: {response.Body}", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("HTTP Request")]
public sealed class HTTPRequestNode : ModuleNode<HTTPModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<HttpMethod> Method = new("Method");
    public ValueInput<string> Url = new("URL");
    public ValueInput<string> Body = new("Body (Optional)");
    
    public ValueOutput<string> ResponseBody = new("Response Body");
    public ValueOutput<int> StatusCode = new("Status Code");
    public ValueOutput<bool> Success = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var method = Method.Read(c);
            var url = Url.Read(c);
            var body = Body.Read(c);
            
            if (url.IsNullOrEmpty())
            {
                Error.Write("URL is required", c);
                await OnError.Execute(c);
                return;
            }

            var response = await Module.SendRequest(method.ToString(), url, 
                body.IsNullOrEmpty() ? null : body);
            
            ResponseBody.Write(response.Body, c);
            StatusCode.Write(response.StatusCode, c);
            Success.Write(response.Success, c);

            if (response.Success)
            {
                await Next.Execute(c);
            }
            else
            {
                Error.Write($"HTTP {response.StatusCode}: {response.Body}", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            Success.Write(false, c);
            await OnError.Execute(c);
        }
    }
}
