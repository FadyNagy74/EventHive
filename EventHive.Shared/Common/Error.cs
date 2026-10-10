namespace EventHive.Shared.Common;

public class Error
{
    public string Description { get; }
    public ErrorType Type { get; }

    private Error(string description, ErrorType type)
    {
        Description = description;
        Type = type;
    }

    public static Error NotFound(string description)
        => new(description, ErrorType.NotFound);

    public static Error Validation(string description)
        => new(description, ErrorType.Validation);

    public static Error Unauthorized(string description)
        => new(description, ErrorType.Unauthorized);

    public static Error Failure(string description)
        => new(description, ErrorType.Failure);

    public static Error Conflict(string description)
        => new(description, ErrorType.Conflict);
}
