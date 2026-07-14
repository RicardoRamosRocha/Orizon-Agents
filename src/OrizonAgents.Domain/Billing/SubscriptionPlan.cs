using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Billing;

public sealed class SubscriptionPlan : AuditableEntity
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int CurrencyMaxLength = 3;

    private readonly List<PlanEntitlement> _entitlements = [];

    private SubscriptionPlan()
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        Currency = "BRL";
        ConcurrencyStamp = string.Empty;
    }

    private SubscriptionPlan(
        string name,
        string code,
        string description,
        decimal monthlyPrice,
        decimal yearlyPrice,
        string currency,
        int trialDays,
        bool isPublic,
        int sortOrder)
    {
        Name = Ensure(name, NameMaxLength, nameof(name));
        Code = PlanCode.Create(code);
        Description = EnsureOptional(description, DescriptionMaxLength);
        MonthlyPrice = EnsureNonNegative(monthlyPrice, nameof(monthlyPrice));
        YearlyPrice = EnsureNonNegative(yearlyPrice, nameof(yearlyPrice));
        Currency = EnsureCurrency(currency);
        TrialDays = trialDays >= 0 ? trialDays : throw new ArgumentOutOfRangeException(nameof(trialDays));
        IsPublic = isPublic;
        IsActive = true;
        SortOrder = sortOrder;
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public string Description { get; private set; }

    public decimal MonthlyPrice { get; private set; }

    public decimal YearlyPrice { get; private set; }

    public string Currency { get; private set; }

    public int TrialDays { get; private set; }

    public bool IsPublic { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public int SortOrder { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    public IReadOnlyCollection<PlanEntitlement> Entitlements => _entitlements.AsReadOnly();

    public static SubscriptionPlan Create(
        string name,
        string code,
        string description,
        decimal monthlyPrice,
        decimal yearlyPrice,
        string currency = "BRL",
        int trialDays = 0,
        bool isPublic = true,
        int sortOrder = 0)
    {
        return new SubscriptionPlan(name, code, description, monthlyPrice, yearlyPrice, currency, trialDays, isPublic, sortOrder);
    }

    public void Update(
        string name,
        string description,
        decimal monthlyPrice,
        decimal yearlyPrice,
        string currency,
        int trialDays,
        bool isPublic,
        int sortOrder)
    {
        Name = Ensure(name, NameMaxLength, nameof(name));
        Description = EnsureOptional(description, DescriptionMaxLength);
        MonthlyPrice = EnsureNonNegative(monthlyPrice, nameof(monthlyPrice));
        YearlyPrice = EnsureNonNegative(yearlyPrice, nameof(yearlyPrice));
        Currency = EnsureCurrency(currency);
        TrialDays = trialDays >= 0 ? trialDays : throw new ArgumentOutOfRangeException(nameof(trialDays));
        IsPublic = isPublic;
        SortOrder = sortOrder;
        TouchConcurrency();
    }

    public void Activate()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Plano arquivado não pode ser ativado.");
        }

        IsActive = true;
        TouchConcurrency();
    }

    public void Deactivate()
    {
        IsActive = false;
        TouchConcurrency();
    }

    public void Archive()
    {
        IsArchived = true;
        IsActive = false;
        IsPublic = false;
        TouchConcurrency();
    }

    public void SetEntitlement(string featureKey, bool isEnabled, int? limitValue)
    {
        string key = EnsureFeatureKey(featureKey);
        if (limitValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitValue), "Limite não pode ser negativo.");
        }

        PlanEntitlement? entitlement = _entitlements.SingleOrDefault(candidate => candidate.FeatureKey == key);
        if (entitlement is null)
        {
            _entitlements.Add(PlanEntitlement.Create(Id, key, isEnabled, limitValue));
        }
        else
        {
            entitlement.Update(isEnabled, limitValue);
        }

        TouchConcurrency();
    }

    public void EnsureConcurrencyStamp(string concurrencyStamp)
    {
        if (!string.Equals(ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("O plano foi alterado por outro usuário. Recarregue a página e tente novamente.");
        }
    }

    private static string Ensure(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
    }

    private static string EnsureOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : throw new ArgumentException($"Description cannot exceed {maxLength} characters.", nameof(value));
    }

    private static decimal EnsureNonNegative(decimal value, string parameterName)
    {
        return value >= 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "Preço não pode ser negativo.");
    }

    private static string EnsureCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        string normalized = currency.Trim().ToUpperInvariant();
        return normalized.Length == CurrencyMaxLength ? normalized : throw new ArgumentException("Moeda deve usar código ISO de 3 letras.", nameof(currency));
    }

    private static string EnsureFeatureKey(string featureKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);
        return featureKey.Trim().ToLowerInvariant();
    }

    private void TouchConcurrency()
    {
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    private static string NewConcurrencyStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
