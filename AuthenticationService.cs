using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

public static class AuthenticationService
{
    public static async Task <(string Id, string Username)> SignInAndGetUserId()
    {
        var clientId = "a79ccd89-0bb4-4909-92fa-737e246e3bec";
        var tenantId = "b55f0c51-61a7-45c3-84df-33569b247796";
        
        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .Build();
        
        AuthenticationResult result = null;
        try
        {
            result = await app.AcquireTokenWithDeviceCode(new string[] {"user.read"}, 
                deviceCodeCallback =>
                {
                    Console.WriteLine(deviceCodeCallback.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync().ConfigureAwait(false);
        }
        catch(MsalServiceException ex)
        {
            Console.WriteLine($"MSAL error: {ex.Message}");
        }

        var handler = new JwtSecurityTokenHandler();
        var idToken = handler.ReadJwtToken(result.IdToken);
        return 
        (
            idToken.Claims.First(x => x.Type.Equals("oid")).Value, 
            idToken.Claims.First(x => x.Type.Equals("preferred_username")).Value
        );
    }
}