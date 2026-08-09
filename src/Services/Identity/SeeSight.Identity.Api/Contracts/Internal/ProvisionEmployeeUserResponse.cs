namespace SeeSight.Identity.Api.Contracts.Internal;

public sealed record ProvisionEmployeeUserResponse(Guid UserId, string TempPassword);
