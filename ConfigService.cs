using Microsoft.Extensions.Configuration;

public static class ConfigService
{
    public static IConfiguration GetAppConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<Program>();

        return builder.Build();
    }
}