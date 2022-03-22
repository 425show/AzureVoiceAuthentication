using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class StorageService
{
    private static string fileName = "data.json";
    private static string storageFolder = AppDomain.CurrentDomain.BaseDirectory;
    private static string filePath = $"{storageFolder}/{fileName}";
    public static async Task Save(UserData data)
    {
        var content = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(filePath, content);
    }

    public static async Task<UserData> Read()
    {
        var content = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<UserData>(content);
    }

    public static bool HasEnrolledUser()
    {
        if(!File.Exists(filePath))
            return false;
        
        var data = Read().GetAwaiter().GetResult();
        return data.IsEnrolled;
    }
}

public class UserData
{
    public string UserId { get; set; }
    public string ProfileId {get; set;}
    public string Username { get; set; }
    public bool IsEnrolled { get; set; }
}