using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;

var baseUrl = "http://synologyds920:5000/webapi/entry.cgi";
var queryString = HttpUtility.ParseQueryString(string.Empty);
JsonSerializerOptions jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
var synoToken = string.Empty;
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var account = config.GetSection("synoApiAuth")["account"]; ;
var passwd = config.GetSection("synoApiAuth")["passwd"]; ;

using (HttpClient client = new())
{
    queryString["api"] = "SYNO.API.Auth";
    queryString["version"] = "6";
    queryString["method"] = "login";
    queryString["account"] = account;
    queryString["passwd"] = passwd;
    queryString["enable_syno_token"] = "yes";
    var url = string.Join("?", baseUrl, queryString.ToString());
    var response = await client.GetAsync(url);
    if (response.IsSuccessStatusCode)
    {
        string result = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<ApiAuthResponse>(result, jsonOptions);
        if (authResponse?.Success == true)
            synoToken = authResponse.Data.Synotoken;
    }

    queryString.Clear();
    queryString["api"] = "SYNO.FileStation.List";
    queryString["version"] = "2";
    queryString["method"] = "list";
    queryString["folder_path"] = "/music";
    queryString["sort_by"] = "crtime";
    queryString["sort_direction"] = "desc";
    queryString["filetype"] = "dir";
    queryString["additional"] = "[\"real_path\",\"time\"]";
    queryString["SynoToken"] = synoToken;
    url = string.Join("?", baseUrl, queryString.ToString());
    response = await client.GetAsync(url);
    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadAsStringAsync();
        var listResponse = JsonSerializer.Deserialize<FileStationListResponse>(result, jsonOptions);
        if (listResponse?.Success == true && listResponse.Data.Files != null)
        {
            foreach (var file in listResponse.Data.Files)
                Console.WriteLine(file.Name);

            Console.WriteLine("Total files: " + listResponse.Data.Files.Count);
        }
    }
}

internal class ApiAuthResponse
{
    public required AuthData Data { get; set; }
    public bool Success { get; set; }
}
internal class AuthData
{
    public string? Did { get; set; }
    public bool IsPortalPort { get; set; }
    public string? Sid { get; set; }
    public string? Synotoken { get; set; }
}

internal class FileStationListResponse
{
    public required FileListData Data { get; set; }
    public bool Success { get; set; }
}

internal class FileListData
{
    public List<ListFile>? Files { get; set; }
}

internal class ListFile
{
    public Additional? Additional { get; set; }
    public bool Isdir { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
}

internal class Additional
{
}
