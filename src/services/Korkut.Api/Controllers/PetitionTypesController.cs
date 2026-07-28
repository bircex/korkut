using Korkut.Core;
using Korkut.Contract.PetitionTypes.Responses;
using Korkut.Infrastructure.Security;
using Korkut.Contract.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// The petition-type picker, read from the EMBEDDED template catalog. Petitions ship with the
/// product, so this is static reference data — there is no per-office or per-user override.
/// </summary>
[ApiController]
[Route("api/petition-types")]
public sealed class PetitionTypesController(TemplateCatalog catalog) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public IActionResult List()
        => Ok(catalog.Categories(TemplateCatalog.Dilekce)
            .Select(c => new PetitionCategoryResponse(
                c.Kategori,
                c.Types.Select(t => new PetitionTypeResponse(t.Id, t.Ad, t.Ref, t.Sik)).ToList()))
            .ToList());
}
