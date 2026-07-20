using System.ComponentModel.DataAnnotations;

namespace Korkut.Api.Errors;

/// <summary>One field-level validation failure, matching the Python/FastAPI shape.</summary>
public sealed record ValidationError(string Type, IReadOnlyList<string> Loc, string Msg, object? Input);

/// <summary>422 body: <c>{ "detail": [ { type, loc, msg, input }, ... ] }</c>.</summary>
public sealed record ValidationProblem(IReadOnlyList<ValidationError> Detail);

/// <summary>
/// Validates a request DTO with DataAnnotations and, on failure, returns HTTP 422
/// with the parity-contract validation body. Keeps the .NET validation response
/// shape aligned with the Python contract (docs/migration/compat/CONTRACTS.md §2).
/// </summary>
public static class DtoValidation
{
    public static IResult? Validate<T>(T dto)
        where T : notnull
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(dto, context, results, validateAllProperties: true))
        {
            return null;
        }

        var errors = new List<ValidationError>();
        foreach (var result in results)
        {
            var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
            foreach (var member in members)
            {
                var field = ToSnake(member);
                errors.Add(new ValidationError(
                    Type: "value_error",
                    Loc: string.IsNullOrEmpty(field) ? ["body"] : ["body", field],
                    Msg: result.ErrorMessage ?? "Invalid value",
                    Input: null));
            }
        }

        return Results.Json(new ValidationProblem(errors), statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static string ToSnake(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
