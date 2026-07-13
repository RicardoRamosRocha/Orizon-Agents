namespace OrizonAgents.Application.Common.Results;

public sealed class OperationResult
{
    private OperationResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    public string? FirstError => Errors.FirstOrDefault();

    public static OperationResult Success()
    {
        return new OperationResult(true, Array.Empty<string>());
    }

    public static OperationResult Failure(params string[] errors)
    {
        return new OperationResult(false, errors);
    }

    public static OperationResult Failure(IEnumerable<string> errors)
    {
        return new OperationResult(false, errors.ToArray());
    }
}
