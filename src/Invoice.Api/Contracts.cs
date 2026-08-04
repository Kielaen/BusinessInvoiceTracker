namespace Invoice.Api;

public sealed record AntiforgeryResponse(string HeaderName, string Token);
public sealed record LoginRequest(string Email, string Password);
public sealed record MembershipSummary(
    Guid BusinessId,
    string BusinessName,
    string Role);
public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    IReadOnlyList<MembershipSummary> Memberships);
public sealed record BusinessResponse(Guid Id, string Name, string Role);
public sealed record BusinessPageResponse(
    IReadOnlyList<BusinessResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
public sealed record MembershipResponse(
    Guid UserId,
    string Email,
    string Role,
    bool IsActive);
public sealed record MembershipPageResponse(
    IReadOnlyList<MembershipResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
public sealed record WalkingSkeletonAccepted(
    Guid ProbeId,
    Guid MessageId,
    string Status);
public sealed record WalkingSkeletonStatus(
    Guid ProbeId,
    string Status,
    DateTime? ProcessedAtUtc);
