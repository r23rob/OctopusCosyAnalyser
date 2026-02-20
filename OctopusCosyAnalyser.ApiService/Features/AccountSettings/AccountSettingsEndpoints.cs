namespace OctopusCosyAnalyser.ApiService.Features.AccountSettings;

/// <summary>Wires all AccountSettings feature operations onto the /api/settings route group.</summary>
public static class AccountSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAccountSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/settings");

        GetAccountSettings.Register(group);
        GetAccountSettingsByAccount.Register(group);
        UpsertAccountSettings.Register(group);

        return routes;
    }
}
