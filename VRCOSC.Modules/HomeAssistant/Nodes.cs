// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;
using Bluscream;

namespace Bluscream.Modules;

[Node("Call Service", "HomeAssistant")]
public sealed class HACallServiceNode : FlowModuleNode<HomeAssistantModule>
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Domain = new("Domain");
    public ValueInput<string> Service = new("Service");
    public ValueInput<string> EntityId = new("Entity ID");
    public ValueInput<Dictionary<string, object>> ServiceData = new("Service Data (Map)");
    public ValueInput<string> ServiceDataJson = new("Service Data (JSON)");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseCtx c)
    {
        try
        {
            var domain = Domain.Read(c);
            var service = Service.Read(c);
            var entityId = EntityId.Read(c);
            var serviceDataMap = ServiceData.Read(c);
            var serviceDataJson = ServiceDataJson.Read(c);

            if (domain.IsNullOrEmpty() || service.IsNullOrEmpty())
            {
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            object? data = null;
            if (serviceDataMap != null && serviceDataMap.Count > 0)
            {
                data = serviceDataMap;
            }
            else if (!serviceDataJson.IsNullOrEmpty())
            {
                try
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, object>>(serviceDataJson);
                }
                catch { }
            }

            var success = await Module.CallService(domain, service, entityId, data);
            Success.Write(success, c);

            if (success)
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"CallService Node Error: {ex.Message}");
            Success.Write(false, c);
            await OnError.Execute(c);
        }
    }
}

[Node("Get Entity State", "HomeAssistant")]
public sealed class HAGetStateNode : FlowModuleNode<HomeAssistantModule>
{
    public FlowContinuation Next = new("Next");

    public ValueInput<string> EntityId = new("Entity ID");

    public ValueOutput<string> State = new("State");
    public ValueOutput<bool> IsOn = new("Is On");
    public ValueOutput<Dictionary<string, object>> Attributes = new("Attributes");
    public ValueOutput<string> AttributesJson = new("Attributes (JSON)");

    protected override async Task Process(PulseCtx c)
    {
        try
        {
            var entityId = EntityId.Read(c);
            if (entityId.IsNullOrEmpty())
            {
                State.Write(string.Empty, c);
                IsOn.Write(false, c);
                Attributes.Write(new Dictionary<string, object>(), c);
                AttributesJson.Write("{}", c);
                await Next.Execute(c);
                return;
            }

            var (stateStr, isOn, attrDict, attrJson) = await Module.GetEntityStateDetails(entityId);

            State.Write(stateStr, c);
            IsOn.Write(isOn, c);
            Attributes.Write(attrDict, c);
            AttributesJson.Write(attrJson, c);

            await Next.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"GetState Node Error: {ex.Message}");
            State.Write(string.Empty, c);
            IsOn.Write(false, c);
            Attributes.Write(new Dictionary<string, object>(), c);
            AttributesJson.Write("{}", c);
            await Next.Execute(c);
        }
    }
}

[Node("Render Template", "HomeAssistant")]
public sealed class HARenderTemplateNode : FlowModuleNode<HomeAssistantModule>
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Template = new("Jinja Template");

    public ValueOutput<string> Result = new("Rendered Result");

    protected override async Task Process(PulseCtx c)
    {
        try
        {
            var template = Template.Read(c);
            if (template.IsNullOrEmpty())
            {
                Result.Write(string.Empty, c);
                await OnError.Execute(c);
                return;
            }

            var rendered = await Module.RenderTemplate(template);
            Result.Write(rendered, c);

            if (!rendered.StartsWith("[Template Error"))
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"RenderTemplate Node Error: {ex.Message}");
            Result.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}
