using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppConnection : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 120;
    public const int BusinessAccountIdMaxLength = 80;
    public const int PhoneNumberIdMaxLength = 80;
    public const int DisplayPhoneNumberMaxLength = 32;
    public const int VerifiedNameMaxLength = 120;
    public const int EncryptedTokenMaxLength = 4096;
    public const int ConcurrencyStampMaxLength = 64;

    private WhatsAppConnection()
    {
        Name = string.Empty;
        WhatsAppBusinessAccountId = string.Empty;
        PhoneNumberId = string.Empty;
        DisplayPhoneNumber = string.Empty;
        VerifiedName = string.Empty;
        EncryptedAccessToken = string.Empty;
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    private WhatsAppConnection(Guid tenantId, string name, string businessAccountId, string phoneNumberId, string displayPhoneNumber, string verifiedName, string encryptedAccessToken, bool isDefault)
        : this()
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        TenantId = tenantId;
        SetName(name);
        WhatsAppBusinessAccountId = RequireLength(businessAccountId, BusinessAccountIdMaxLength, nameof(businessAccountId));
        PhoneNumberId = RequireLength(phoneNumberId, PhoneNumberIdMaxLength, nameof(phoneNumberId));
        DisplayPhoneNumber = RequireLength(displayPhoneNumber, DisplayPhoneNumberMaxLength, nameof(displayPhoneNumber));
        VerifiedName = TrimOptional(verifiedName, VerifiedNameMaxLength);
        ReplaceEncryptedToken(encryptedAccessToken);
        Status = WhatsAppConnectionStatus.PendingValidation;
        QualityRating = WhatsAppQualityRating.Unknown;
        IsDefault = isDefault;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string WhatsAppBusinessAccountId { get; private set; }

    public string PhoneNumberId { get; private set; }

    public string DisplayPhoneNumber { get; private set; }

    public string VerifiedName { get; private set; }

    public string EncryptedAccessToken { get; private set; }

    public WhatsAppConnectionStatus Status { get; private set; }

    public WhatsAppQualityRating QualityRating { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTime? LastValidatedAtUtc { get; private set; }

    public DateTime? LastWebhookAtUtc { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    public static WhatsAppConnection Create(Guid tenantId, string name, string businessAccountId, string phoneNumberId, string displayPhoneNumber, string verifiedName, string encryptedAccessToken, bool isDefault)
        => new(tenantId, name, businessAccountId, phoneNumberId, displayPhoneNumber, verifiedName, encryptedAccessToken, isDefault);

    public void Update(string name, string displayPhoneNumber, string verifiedName)
    {
        EnsureNotDisconnected();
        SetName(name);
        DisplayPhoneNumber = RequireLength(displayPhoneNumber, DisplayPhoneNumberMaxLength, nameof(displayPhoneNumber));
        VerifiedName = TrimOptional(verifiedName, VerifiedNameMaxLength);
        TouchConcurrency();
    }

    public void ReplaceEncryptedToken(string encryptedAccessToken)
    {
        EncryptedAccessToken = RequireLength(encryptedAccessToken, EncryptedTokenMaxLength, nameof(encryptedAccessToken));
        TouchConcurrency();
    }

    public void MarkValidated(DateTime utcNow, WhatsAppQualityRating qualityRating, string verifiedName)
    {
        EnsureUtc(utcNow);
        Status = WhatsAppConnectionStatus.Active;
        QualityRating = qualityRating;
        VerifiedName = TrimOptional(verifiedName, VerifiedNameMaxLength);
        LastValidatedAtUtc = utcNow;
        TouchConcurrency();
    }

    public void MarkInvalid(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Status = WhatsAppConnectionStatus.Invalid;
        LastValidatedAtUtc = utcNow;
        TouchConcurrency();
    }

    public void MarkWebhookReceived(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        LastWebhookAtUtc = utcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        TouchConcurrency();
    }

    public void Disconnect()
    {
        Status = WhatsAppConnectionStatus.Disconnected;
        IsDefault = false;
        TouchConcurrency();
    }

    public void EnsureConcurrencyStamp(string concurrencyStamp)
    {
        if (!string.Equals(ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("O registro foi alterado por outro processo.");
        }
    }

    private void SetName(string name)
    {
        Name = RequireLength(name, NameMaxLength, nameof(name));
    }

    private void EnsureNotDisconnected()
    {
        if (Status == WhatsAppConnectionStatus.Disconnected)
        {
            throw new InvalidOperationException("Conexão desconectada não pode ser alterada.");
        }
    }

    private void TouchConcurrency()
    {
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    private static string RequireLength(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Valor obrigatório.", parameterName);
        string trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName, $"Valor deve ter até {maxLength} caracteres.");
        return trimmed;
    }

    private static string TrimOptional(string? value, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value), $"Valor deve ter até {maxLength} caracteres.");
        return trimmed;
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
    }
}
