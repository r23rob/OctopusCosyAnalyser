using OctopusCosyAnalyser.Shared;

namespace OctopusCosyAnalyser.ApiService.Extensions;

public static class ResultExtensions
{
    public static IResult ToApiResult<T>(this ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return result.StatusCode switch
        {
            400 => Results.BadRequest(new { error = result.Error }),
            404 => Results.NotFound(result.Error),
            _ => Results.Problem(result.Error)
        };
    }
}
