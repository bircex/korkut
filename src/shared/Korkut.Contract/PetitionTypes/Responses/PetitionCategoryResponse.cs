namespace Korkut.Contract.PetitionTypes.Responses;

/// <summary>A catalog category with the petition types under it.</summary>
public sealed record PetitionCategoryResponse(string Kategori, IReadOnlyList<PetitionTypeResponse> Dilekceler);
