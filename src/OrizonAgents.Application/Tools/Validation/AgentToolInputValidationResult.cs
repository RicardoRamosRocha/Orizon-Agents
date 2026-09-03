namespace OrizonAgents.Application.Tools.Validation;

public sealed record AgentToolInputValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static AgentToolInputValidationResult Success() =>
        new(true, Array.Empty<string>());

    public static AgentToolInputValidationResult Failure(
        IEnumerable<string> errors) =>
        new(false, errors.ToArray());
}
