namespace OrizonAgents.Application.Common.Results;

public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public IReadOnlyCollection<string> Errors { get; }

    public string? FirstError => Errors.FirstOrDefault();

    public static OperationResult<T> Success(T value)
    {
        return new OperationResult<T>(true, value, Array.Empty<string>());
    }

    public static OperationResult<T> Failure(params string[] errors)
    {
        return new OperationResult<T>(false, default, errors);
    }

    public static OperationResult<T> Failure(IEnumerable<string> errors)
    {
        return new OperationResult<T>(false, default, errors.ToArray());
    }
}
