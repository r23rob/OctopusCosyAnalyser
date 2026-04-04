namespace OctopusCosyAnalyser.Shared;

public sealed class ServiceResult<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;
    public int? StatusCode { get; }

    private ServiceResult(T? value, string? error, int? statusCode)
    {
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    public static ServiceResult<T> Ok(T value) => new(value, null, null);
    public static ServiceResult<T> Fail(string error, int statusCode = 500) => new(default, error, statusCode);
    public static ServiceResult<T> NotFound(string error) => new(default, error, 404);
    public static ServiceResult<T> BadRequest(string error) => new(default, error, 400);
}
