using System.Text.Json.Serialization;

namespace FileDownloadApp;

public class DownloadRequest
{
    [JsonPropertyName("guidType")]
    public GuidType GuidType { get; set; } = 0;

    [JsonPropertyName("guidToQuantity")]
    public Dictionary<Guid, int> GuidToQuantity { get; set; } = new();

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("sharedComponentSetLookupCode")]
    public string? SharedComponentSetLookupCode { get; set; }

    [JsonPropertyName("memberType")]
    // only used for machinery TRS downloads, ignored otherwise
    public DownloadMachineryTrsRequestMemberTypes? MemberType { get; set; }
}

public class AssemblyGroupResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("componentDesigns")]
    public Dictionary<Guid, StationComponentDesign> ComponentDesigns { get; set; } = new();
}

public class StationComponentDesign
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;
}

public enum GuidType
{
    ComponentDesign = 0
}

public enum DownloadMachineryTrsRequestMemberTypes
{
    All = 0,
    Chords = 1,
    Webs = 2,
    Blocks = 3,
}
