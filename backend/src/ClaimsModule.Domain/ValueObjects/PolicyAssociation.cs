namespace ClaimsModule.Domain.ValueObjects;

// A policy snapshot supplied by Application; Domain does not query the policy catalogue.
public sealed record PolicyAssociation(Guid Id, string PolicyNumber, string ClientName,
    DateTimeOffset EffectiveDate, DateTimeOffset ExpirationDate);
