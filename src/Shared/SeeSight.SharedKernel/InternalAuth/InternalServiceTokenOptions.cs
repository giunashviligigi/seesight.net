namespace SeeSight.SharedKernel.InternalAuth;

public sealed class InternalServiceTokenOptions
{
    public const string SectionName = "Internal";

    public string ServiceToken { get; set; } = string.Empty;
}
