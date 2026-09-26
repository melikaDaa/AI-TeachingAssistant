namespace AI.TeachingAssistant.Application.Common;

/// <summary>Simple result type so services never throw for expected failures.</summary>
public sealed class Result<T>
{
    private Result(bool ok, T? value, string? error)
    {
        Ok = ok;
        Value = value;
        Error = error;
    }

    public bool Ok { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
