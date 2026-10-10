namespace EventHive.Shared.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public List<Error> Errors { get; }

    private Result(bool isSuccess, T? value, List<Error> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value)
        => new(true, value, []);

    public static Result<T> Failure(List<Error> errors)
        => new(false, default, errors);

    public static Result<T> Failure(Error error)
        => new(false, default, [error]);
}
