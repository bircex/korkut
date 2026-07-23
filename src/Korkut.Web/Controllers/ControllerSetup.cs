using Korkut.Web.Errors;
using Korkut.Web.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Web.Controllers;

/// <summary>
/// Controller hosting on the shared JSON contract, with model-validation failures rendered
/// as the parity 422 body (<c>{ "detail": [ { type, loc, msg, input } ] }</c>) rather than
/// the ASP.NET default 400 ProblemDetails.
/// </summary>
public static class ControllerSetup
{
    public static IServiceCollection AddKorkutControllers(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(o => KorkutJson.Apply(o.JsonSerializerOptions));

        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(error => new ValidationError(
                    Type: "value_error",
                    Loc: string.IsNullOrEmpty(entry.Key) ? ["body"] : ["body", ToSnake(entry.Key)],
                    Msg: error.ErrorMessage,
                    Input: null)))
                .ToList();

            return new ObjectResult(new Errors.ValidationProblem(errors))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            };
        });

        return services;
    }

    private static string ToSnake(string name)
    {
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
