namespace OrizonAgents.Infrastructure.Tools.Credentials;

public interface IToolCredentialProtector
{
    string Protect(string secret);
    string Unprotect(string encryptedSecret);
}
