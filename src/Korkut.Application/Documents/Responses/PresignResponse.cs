namespace Korkut.Application.Documents.Responses;

/// <summary>A short-lived download URL for a document that has been proven clean.</summary>
public sealed record PresignResponse(string Url);
