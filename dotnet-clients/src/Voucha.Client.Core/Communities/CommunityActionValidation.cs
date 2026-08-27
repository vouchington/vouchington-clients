namespace Voucha.Client.Core.Communities;

public sealed record CommunityActionValidation(
    bool IsValid,
    string? ErrorMessage = null,
    bool RequiresCaptcha = false);
