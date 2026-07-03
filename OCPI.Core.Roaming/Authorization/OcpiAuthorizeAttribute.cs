using BitzArt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using OCPI.Core.Roaming.Services;

public class OcpiAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string token;
        try
        {
            token = GetToken(context.HttpContext.Request);
        }
        catch
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var credentialsService = context.HttpContext.RequestServices
            .GetRequiredService<IOcpiCredentialsService>();

        var partner = await credentialsService.GetPartnerByTokenAsync(token);

        if (partner == null)
        {
            Console.WriteLine("Partner Not Found");
            context.Result = new UnauthorizedResult();
            return;
        }

        Console.WriteLine($"Partner Info: {partner.BusinessName} - {partner.PartyId} - {partner.Role}");

        // Expose the resolved partner to downstream controllers via HttpContext.Items
        context.HttpContext.Items["OcpiPartner"] = partner;
    }

    private static string GetToken(HttpRequest request)
    {
        var authHeaders = request.Headers["Authorization"];

        if (!authHeaders.Any()) throw ApiException.Unauthorized("Authorization header not found");
        if (authHeaders.Count > 1) throw ApiException.Unauthorized("Multiple Authorization headers not allowed.");

        var header = authHeaders.First()!;
        if (string.IsNullOrWhiteSpace(header)) throw ApiException.Unauthorized("Invalid authorization token.");

        var token = header.Split(" ").Last();

        return token;
    }
}