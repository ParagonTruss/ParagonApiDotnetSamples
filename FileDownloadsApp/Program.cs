using FileDownloadApp;

// api docs https://designserver.paragontruss.com/api-docs

// !!! USER CONFIGURATION !!!
// get this from on of the assembly groups at https://production.paragontruss.com/assembly-groups
string AssemblyGroupGuid = "GUID_HERE"; 

// api key can be created within https://design.paragontruss.com/
string ApiToken = "TOKEN_HERE";

string BaseUrl = "https://designserver.paragontruss.com/";

// available file types can be found in the api docs (https://designserver.paragontruss.com/api-docs/#tag/componentdesigndownloads), these are the ones we have tested with so far
string[] fileTypes = { "tps", "tre", "machineryTrs", "omn" };

// !!!!!!!!!!!!!!!!!!!!!!!!!!!

Console.WriteLine("--- File Download Example Script ---");

if (string.IsNullOrWhiteSpace(AssemblyGroupGuid) || AssemblyGroupGuid.Contains("GUID_HERE"))
{
    Console.WriteLine("Please configure the AssemblyGroupGuid in Program.cs");
    return;
}

if (string.IsNullOrWhiteSpace(ApiToken) || ApiToken.Contains("TOKEN_HERE"))
{
    Console.WriteLine("Please configure the ApiToken in Program.cs");
    return;
}

var apiClient = new ApiClient(BaseUrl, ApiToken);

// 1. Fetch Assembly Group
var assemblyGroup = await apiClient.GetAssemblyGroupAsync(AssemblyGroupGuid);

if (assemblyGroup == null)
{
    Console.WriteLine("Could not retrieve assembly group. Exiting.");
    return;
}

Console.WriteLine($"Found Assembly Group: {assemblyGroup.Name}");
Console.WriteLine($"Found {assemblyGroup.ComponentDesigns.Count} component designs.");

// 2. Prepare Download Request Data
var guidToQuantity = assemblyGroup.ComponentDesigns.ToDictionary(
    cd => cd.Key, 
    cd => cd.Value.Quantity > 0 ? cd.Value.Quantity : 1 // specific requirement or default to 1?
);

var downloadRequest = new DownloadRequest
{
    GuidType = GuidType.ComponentDesign,
    GuidToQuantity = guidToQuantity,
    GroupName = assemblyGroup.Name,
    SharedComponentSetLookupCode = null,
    MemberType = DownloadMachineryTrsRequestMemberTypes.All
};

// 3. Download Files
var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "downloads", assemblyGroup.Name);
Directory.CreateDirectory(outputDir);
Console.WriteLine($"Files will be saved to: {outputDir}");


foreach (var type in fileTypes)
{
    await apiClient.DownloadFileAsync(type, downloadRequest, outputDir);
}

Console.WriteLine("\nDone.");
