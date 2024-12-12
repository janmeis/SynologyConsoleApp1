using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;

internal class Program
{
    private static string baseUrl = "http://synologyds920:5000/webapi/entry.cgi";
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static async Task Main(string[] args)
    {

        string synoToken = string.Empty;
        const int maxDirCount = 25;

        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        var section = config.GetSection("synoApiAuth");
        var account = section["account"]; ;
        var passwd = section["passwd"];

        using (HttpClient client = new())
        {
            var authData = await GetAuthAsync(account, passwd, client);
            synoToken = authData.Data.Synotoken ?? string.Empty;

            var listResponse = await GetFileListAsync(client, synoToken);
            if (listResponse?.Success == true && listResponse?.Data?.Files != null)
            {
                for (int i = 0; i < Math.Min(listResponse.Data.Files.Count, maxDirCount); i++)
                {
                    var file = listResponse.Data?.Files[i];
                    var dateTime = UnixTimeStampToDateTime(file?.Additional?.Time?.Crtime ?? 0);
                    Console.WriteLine($"{file?.Name}\t({dateTime.ToString("dd.MM.yyyy HH:mm")})");
                    var actualPath = file?.Path ?? string.Empty;
                    var listResponse2 = await GetFileListAsync(client, synoToken, actualPath);
                    if (listResponse2?.Success == true && listResponse2?.Data?.Files != null)
                        foreach (var file2 in listResponse2.Data.Files)
                        {
                            Console.WriteLine($"\t{file2.Name}");
                            actualPath = file2?.Path ?? string.Empty;
                            var listResponse3 = await GetFileListAsync(client, synoToken, actualPath, "name", "asc", "file");
                            if (listResponse3?.Success == true && listResponse3?.Data?.Files != null)
                                foreach (var file3 in listResponse3.Data.Files)
                                    Console.WriteLine($"\t\t{file3.Name}");
                        }
                }

                Console.WriteLine("\nTotal files: " + listResponse.Data.Files.Count);
            }
        }
    }

    private static async Task<ApiAuthResponse> GetAuthAsync(string? account, string? passwd, HttpClient client)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
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
                return authResponse;
        }

        throw new Exception("Failed to authenticate");
    }

    private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }

    private static async Task<FileStationListResponse> GetFileListAsync(HttpClient client, string synoToken, string folderPath = "/music", string sortBy = "crtime", string sortDirection = "desc", string fileType = "dir")
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["api"] = "SYNO.FileStation.List";
        queryString["version"] = "2";
        queryString["method"] = "list";
        queryString["folder_path"] = folderPath;
        queryString["sort_by"] = sortBy;
        queryString["sort_direction"] = sortDirection;
        queryString["filetype"] = fileType;
        queryString["additional"] = "[\"owner\",\"time\"]";
        queryString["SynoToken"] = synoToken;
        var url = string.Join("?", baseUrl, queryString.ToString());
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var listResponse = JsonSerializer.Deserialize<FileStationListResponse>(result, jsonOptions);
            if (listResponse?.Success == true && listResponse?.Data?.Files != null)
                return listResponse;
        }

        throw new Exception($"Failed to read files from {folderPath}");
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
    public Owner? Owner { get; set; }
    public AdditionalTime? Time { get; set; }
    public string? RealPath { get; set; }
}

internal class AdditionalTime
{
    public double? Crtime { get; set; }
    public double? Ctime { get; set; }
    public double? Mtime { get; set; }
}

internal class Owner
{
    public int? Gid { get; set; }
    public string? Group { get; set; }
    public int? Uid { get; set; }
    public string? User { get; set; }
}