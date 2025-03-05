// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using VRCOSC.Modules.HTTP.UI;

namespace VRCOSC.Modules.HTTP;

public class ModuleSetting : ListModuleSetting<Request>
{
    public ModuleSetting()
        : base("Shockers", "Individual shockers. Name them something recognisable", typeof(ModuleSettingView), [])
    {
    }

    protected override Request CreateItem() => new();
}

[JsonObject(MemberSerialization.OptIn)]
public class Request : IEquatable<Request>
{
    [JsonProperty("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public Observable<string> Name { get; set; } = new("New Shocker");

    [JsonProperty("sharecode")]
    public Observable<string> Sharecode { get; set; } = new(string.Empty);

    [JsonConstructor]
    public Request()
    {
    }

    public bool Equals(Request? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name.Equals(other.Name) && Sharecode.Equals(other.Sharecode);
    }
}