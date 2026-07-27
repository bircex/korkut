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
        services.AddControllers()
            // Vertical modules host their own controllers; register each module assembly as an
            // MVC application part so its endpoints are discovered alongside the host's.
            .AddApplicationPart(typeof(Korkut.Clients.ClientsModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Notes.NotesModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Tasks.TasksModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Deadlines.DeadlinesModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Channels.ChannelsModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Correspondence.CorrespondenceModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Outbox.OutboxModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Calendar.CalendarModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Matters.MattersModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Hearings.HearingsModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Finance.FinanceModule).Assembly)
            .AddApplicationPart(typeof(Korkut.Settings.SettingsModule).Assembly)
            .AddJsonOptions(o => KorkutJson.Apply(o.JsonSerializerOptions));

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
