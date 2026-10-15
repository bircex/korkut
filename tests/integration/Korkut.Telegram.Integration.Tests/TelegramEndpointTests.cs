using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Offices;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Security;
using Korkut.Telegram.Services;
using Korkut.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Telegram.Integration.Tests;

/// <summary>
/// The office bot end to end, over the real API and PostgreSQL with RLS: the owner connects and
/// turns on the bot, a member links their chat with a one-time code, and their messages then run
/// as them. Telegram and the model are stand-ins; the chat stand-in answers with whoever the app
/// believes the caller is, which is what proves a message runs as the linked member.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class TelegramEndpointTests(KorkutApiFactory factory)
{
    private const string GoodToken = "123456:good-token";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private sealed class FakeTelegram : ITelegramApi
    {
        public List<(long ChatId, string Text)> Sent { get; } = [];

        public Task<TelegramUser> GetMeAsync(string token, CancellationToken ct = default)
            => token == GoodToken
                ? Task.FromResult(new TelegramUser(1, true, "Korkut", "korkut_test_bot"))
                : throw new TelegramApiException(401, "Unauthorized");

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TelegramUpdate>>([]);

        public Dictionary<string, byte[]> Files { get; } = [];

        public Task<long> SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default)
        {
            lock (Sent)
            {
                Sent.Add((chatId, text));
                return Task.FromResult((long)Sent.Count);
            }
        }

        /// <summary>An edit changes the message in place, so <see cref="Sent"/> shows what is on screen.</summary>
        public Task EditMessageTextAsync(string token, long chatId, long messageId, string text, CancellationToken ct = default)
        {
            lock (Sent)
            {
                Sent[(int)messageId - 1] = (chatId, text);
            }

            return Task.CompletedTask;
        }

        public Task<byte[]> DownloadFileAsync(string token, string fileId, CancellationToken ct = default)
            => Task.FromResult(Files[fileId]);

        public Task SendTypingAsync(string token, long chatId, CancellationToken ct = default) => Task.CompletedTask;

        /// <summary>How many each group has, as Telegram would count them (the bot included).</summary>
        public Dictionary<long, int> MemberCounts { get; } = [];

        public Task<int> GetChatMemberCountAsync(string token, long chatId, CancellationToken ct = default)
            => Task.FromResult(MemberCounts.GetValueOrDefault(chatId));
    }

    /// <summary>
    /// Answers "who am I | what was said", read from the app's current user, streaming it word by
    /// word; an upload answers with what arrived.
    /// </summary>
    private sealed class WhoAmIChat(ICurrentUser current) : ISessionChatService
    {
        private string Answer(string text) => $"{current.UserId}|{current.Permissions.Contains(Permissions.OfficeSettings)}|{text}";

        public Task<ChatOutcome?> ChatAsync(
            Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions, CancellationToken ct = default)
            => Task.FromResult<ChatOutcome?>(new ChatOutcome(Answer(text), "Telegram", null, null));

        public async Task<UploadOutcome?> UploadAsync(
            Guid officeId, Guid userId, Guid conversationId, string fileName, string contentType, Stream content, string text,
            IReadOnlySet<string> permissions, CancellationToken ct = default)
        {
            using var reader = new StreamReader(content);
            var body = await reader.ReadToEndAsync(ct);
            return new UploadOutcome(Answer($"{fileName}|{contentType}|{body}|{text}"), "Telegram", fileName);
        }

        public async Task<ChatOutcome?> ChatStreamAsync(
            Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions,
            Func<string, Task> onStep, Func<string, Task> onToken,
            Func<string, JsonElement, Task>? onToolCall = null, Func<string, string, bool, Task>? onToolResult = null,
            CancellationToken ct = default)
        {
            var answer = Answer(text);
            foreach (var piece in answer.Chunk(5))
            {
                await onToken(new string(piece));
            }

            return new ChatOutcome(answer, "Telegram", null, null);
        }
    }

    private (WebApplicationFactory<Program> App, FakeTelegram Telegram) Host()
    {
        var telegram = new FakeTelegram();
        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITelegramApi>();
            s.AddSingleton<ITelegramApi>(telegram);
            s.RemoveAll<ISessionChatService>();
            s.AddScoped<ISessionChatService, WhoAmIChat>();
        }));
        return (app, telegram);
    }

    private static HttpClient As(WebApplicationFactory<Program> app, Guid office, Guid user, string role)
    {
        var client = app.CreateClient();
        var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, SystemClock.Instance)
            .Mint(new AccessClaims(user, office, role, "s1"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<Guid> AddLawyerAsync(Guid office) => AddMemberAsync(office, Roles.Lawyer);

    private async Task<Guid> AddMemberAsync(Guid office, string role)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"uye-{Guid.CreateVersion7():N}@example.com", role, null, null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Task SendAsync(WebApplicationFactory<Program> app, Guid office, long chatId, string text, string? username = "avukat")
        => DeliverAsync(app, office, new TelegramMessage(1, new TelegramUser(chatId, false, "Avukat", username), new TelegramChat(chatId, "private"), text));

    private static async Task DeliverAsync(WebApplicationFactory<Program> app, Guid office, TelegramMessage message)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(
            new InstallOffice(office, "Test Bürosu", false), GoodToken, new TelegramUpdate(1, message));
    }

    private static async Task<HttpResponseMessage> TurnOnAsync(HttpClient owner, bool consent = true)
        => await owner.PutAsJsonAsync("/api/settings/telegram", new { token = GoodToken, enabled = true, consent }, Json);

    [Fact]
    public async Task The_bot_is_turned_on_only_with_a_token_Telegram_knows_and_the_owners_consent()
    {
        var (app, _) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = As(app, office, ownerId, Roles.OfficeOwner);

        var rejected = await owner.PutAsJsonAsync("/api/settings/telegram", new { token = "999:wrong", enabled = true, consent = true }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Contains("tanımadı", await rejected.Content.ReadAsStringAsync());

        var noConsent = await TurnOnAsync(owner, consent: false);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noConsent.StatusCode);
        Assert.Contains("onaylamanız", await noConsent.Content.ReadAsStringAsync());
        var untouched = await owner.GetFromJsonAsync<JsonElement>("/api/settings/telegram", Json);
        Assert.False(untouched.GetProperty("configured").GetBoolean());

        var on = await TurnOnAsync(owner);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        var settings = await on.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(settings.GetProperty("enabled").GetBoolean());
        Assert.Equal("korkut_test_bot", settings.GetProperty("bot_username").GetString());
        Assert.DoesNotContain(GoodToken, settings.GetRawText());

        // The token is kept sealed, never as typed.
        await using var db = factory.OwnerContext();
        var stored = await db.TelegramBots.AsNoTracking().SingleAsync(b => b.OfficeId == office);
        Assert.DoesNotContain("good-token", stored.TokenCipher!);
    }

    [Fact]
    public async Task Only_the_owner_manages_the_bot()
    {
        var (app, _) = Host();
        var (office, _) = await factory.SeedOfficeAsync();
        var lawyer = As(app, office, await AddLawyerAsync(office), Roles.Lawyer);

        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/settings/telegram")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TurnOnAsync(lawyer)).StatusCode);
    }

    [Fact]
    public async Task A_member_links_their_chat_with_a_code_and_then_speaks_as_themselves()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddLawyerAsync(office);
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var lawyer = As(app, office, lawyerId, Roles.Lawyer);
        var chatId = Random.Shared.NextInt64(1_000_000, 9_000_000);

        // No code while the bot is off.
        Assert.Equal(HttpStatusCode.Conflict, (await lawyer.PostAsync("/api/telegram/link/code", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TurnOnAsync(owner)).StatusCode);

        var issued = await (await lawyer.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var code = issued.GetProperty("code").GetString()!;
        Assert.Equal($"https://t.me/korkut_test_bot?start={code}", issued.GetProperty("url").GetString());

        // Before linking, the bot only explains how to link.
        await SendAsync(app, office, chatId, "Merhaba");
        Assert.Contains("Telegram'ı bağla", telegram.Sent[^1].Text);

        await SendAsync(app, office, chatId, $"/start {code}");
        Assert.StartsWith("Hesabınız bağlandı", telegram.Sent[^1].Text);
        var link = await lawyer.GetFromJsonAsync<JsonElement>("/api/telegram/link", Json);
        Assert.True(link.GetProperty("linked").GetBoolean());
        Assert.Equal("avukat", link.GetProperty("telegram_username").GetString());

        // A message runs as the lawyer, with a lawyer's permissions, in a conversation of theirs.
        await SendAsync(app, office, chatId, "Faiz ne kadar?");
        Assert.Equal((chatId, $"{lawyerId}|False|Faiz ne kadar?"), telegram.Sent[^1]);
        await using (var db = factory.OwnerContext())
        {
            var stored = await db.TelegramLinks.AsNoTracking().SingleAsync(l => l.UserId == lawyerId);
            Assert.NotNull(stored.ConversationId);
            Assert.True(await db.Conversations.AnyAsync(c => c.Id == stored.ConversationId && c.OfficeId == office));
        }

        // The code worked once.
        await SendAsync(app, office, chatId + 1, $"/start {code}");
        Assert.Contains("geçersiz", telegram.Sent[^1].Text);

        // The owner sees the link and can undo it; the chat is then a stranger again.
        var settings = await owner.GetFromJsonAsync<JsonElement>("/api/settings/telegram", Json);
        Assert.Equal(lawyerId, Assert.Single(settings.GetProperty("members").EnumerateArray()).GetProperty("user_id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/settings/telegram/members/{lawyerId}")).StatusCode);
        await SendAsync(app, office, chatId, "Hâlâ orada mısın?");
        Assert.Contains("Telegram'ı bağla", telegram.Sent[^1].Text);
    }

    [Fact]
    public async Task New_starts_a_fresh_conversation()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var chatId = Random.Shared.NextInt64(10_000_000, 90_000_000);
        await TurnOnAsync(owner);
        var issued = await (await owner.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        await SendAsync(app, office, chatId, $"/start {issued.GetProperty("code").GetString()}");

        await SendAsync(app, office, chatId, "ilk soru");
        Guid? first;
        await using (var db = factory.OwnerContext())
        {
            first = (await db.TelegramLinks.AsNoTracking().SingleAsync(l => l.UserId == ownerId)).ConversationId;
        }

        await SendAsync(app, office, chatId, "/yeni");
        Assert.StartsWith("Yeni bir sohbet", telegram.Sent[^1].Text);
        await SendAsync(app, office, chatId, "ikinci soru");
        Assert.Equal($"{ownerId}|True|ikinci soru", telegram.Sent[^1].Text);

        await using (var db = factory.OwnerContext())
        {
            var second = (await db.TelegramLinks.AsNoTracking().SingleAsync(l => l.UserId == ownerId)).ConversationId;
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);
        }
    }

    [Fact]
    public async Task Notices_reach_only_linked_members_and_only_while_the_bot_is_on()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddLawyerAsync(office); // never links
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var chatId = Random.Shared.NextInt64(100_000_000, 900_000_000);
        await TurnOnAsync(owner);
        var issued = await (await owner.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        await SendAsync(app, office, chatId, $"/start {issued.GetProperty("code").GetString()}");

        async Task<int> NotifyAsync(IReadOnlyCollection<Guid>? to)
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ITelegramNotifier>().NotifyAsync(office, to, "Süre: İstinaf");
        }

        Assert.Equal(1, await NotifyAsync(null));
        Assert.Equal((chatId, "Süre: İstinaf"), telegram.Sent[^1]);
        Assert.Equal(0, await NotifyAsync([lawyerId]));

        await owner.PutAsJsonAsync("/api/settings/telegram", new { enabled = false, consent = true }, Json);
        Assert.Equal(0, await NotifyAsync(null));
    }

    [Fact]
    public async Task A_document_sent_to_the_bot_is_taken_in_like_an_upload()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddLawyerAsync(office);
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var lawyer = As(app, office, lawyerId, Roles.Lawyer);
        var chatId = Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);
        await TurnOnAsync(owner);
        var issued = await (await lawyer.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        await SendAsync(app, office, chatId, $"/start {issued.GetProperty("code").GetString()}");
        telegram.Files["file-1"] = "Davacı vekili beyanları"u8.ToArray();

        await DeliverAsync(app, office, new TelegramMessage(
            7, new TelegramUser(chatId, false, "Avukat", "avukat"), new TelegramChat(chatId, "private"), null,
            Caption: "Bunu özetle", Document: new TelegramDocument("file-1", "dilekce.pdf", "application/pdf", 23)));

        Assert.Equal((chatId, $"{lawyerId}|False|dilekce.pdf|application/pdf|Davacı vekili beyanları|Bunu özetle"), telegram.Sent[^1]);
    }

    [Fact]
    public async Task A_file_too_large_for_bots_is_declined_before_downloading()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var chatId = Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);
        await TurnOnAsync(owner);
        var issued = await (await owner.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        await SendAsync(app, office, chatId, $"/start {issued.GetProperty("code").GetString()}");

        await DeliverAsync(app, office, new TelegramMessage(
            8, new TelegramUser(chatId, false, "Sahip", null), new TelegramChat(chatId, "private"), null,
            Document: new TelegramDocument("big", "dosya.pdf", "application/pdf", 40L * 1024 * 1024)));

        Assert.Contains("20 MB", telegram.Sent[^1].Text);
    }

    /// <summary>Links a member's chat to the bot, the way the app's "Telegram'ı bağla" does.</summary>
    private static async Task LinkAsync(WebApplicationFactory<Program> app, HttpClient member, Guid office, long chatId)
    {
        var issued = await (await member.PostAsync("/api/telegram/link/code", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        await SendAsync(app, office, chatId, $"/start {issued.GetProperty("code").GetString()}");
    }

    private static async Task<Guid> CreateMatterAsync(HttpClient owner, string title, Guid? member = null)
    {
        var created = await (await owner.PostAsJsonAsync("/api/matters", new { title }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        if (member is { } user)
        {
            Assert.True((await owner.PostAsJsonAsync($"/api/matters/{id}/team", new { user_id = user }, Json)).IsSuccessStatusCode);
        }

        return id;
    }

    private async Task<Guid?> ConversationMatterAsync(Guid userId)
    {
        await using var db = factory.OwnerContext();
        var conversation = (await db.TelegramLinks.AsNoTracking().SingleAsync(l => l.UserId == userId)).ConversationId;
        return conversation is null ? null : (await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == conversation)).MatterId;
    }

    [Fact]
    public async Task A_member_moves_between_their_matters_and_never_into_another()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddLawyerAsync(office);
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var lawyer = As(app, office, lawyerId, Roles.Lawyer);
        var chatId = Random.Shared.NextInt64(100_000_000, 900_000_000);
        await TurnOnAsync(owner);
        await LinkAsync(app, lawyer, office, chatId);

        await SendAsync(app, office, chatId, "/dosyalar");
        Assert.Contains("erişebildiğiniz bir dosya yok", telegram.Sent[^1].Text);

        var mine = await CreateMatterAsync(owner, "Kira tespit davası", lawyerId);
        await CreateMatterAsync(owner, "Gizli birleşme dosyası");

        // The list holds only the lawyer's matter.
        await SendAsync(app, office, chatId, "/dosyalar");
        Assert.Contains("Kira tespit davası", telegram.Sent[^1].Text);
        Assert.DoesNotContain("birleşme", telegram.Sent[^1].Text);

        // A matter they are not on gets the same answer as one that does not exist.
        await SendAsync(app, office, chatId, "/dosya Gizli birleşme");
        Assert.Equal("Bu dosya bulunamadı veya yetkiniz yok.", telegram.Sent[^1].Text);
        await SendAsync(app, office, chatId, "/dosya yok böyle bir dosya");
        Assert.Equal("Bu dosya bulunamadı veya yetkiniz yok.", telegram.Sent[^1].Text);

        await SendAsync(app, office, chatId, "/dosya KİRA TESPİT");
        Assert.StartsWith("«Kira tespit davası» dosyasına geçtim", telegram.Sent[^1].Text);
        Assert.Equal(mine, await ConversationMatterAsync(lawyerId));
        await SendAsync(app, office, chatId, "Duruşma ne zaman?");
        Assert.Equal($"{lawyerId}|False|Duruşma ne zaman?", telegram.Sent[^1].Text);

        // /yeni stays in the matter; /kisisel leaves it.
        await SendAsync(app, office, chatId, "/yeni");
        Assert.StartsWith("«Kira tespit davası» dosyasında yeni bir sohbet", telegram.Sent[^1].Text);
        Assert.Equal(mine, await ConversationMatterAsync(lawyerId));
        await SendAsync(app, office, chatId, "/kisisel");
        Assert.StartsWith("Kişisel sohbete geçtim", telegram.Sent[^1].Text);
        await SendAsync(app, office, chatId, "Genel bir soru");
        Assert.Null(await ConversationMatterAsync(lawyerId));
    }

    [Fact]
    public async Task A_member_without_personal_chats_is_pointed_to_their_matters()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var readerId = await AddMemberAsync(office, Roles.Reader);
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        var chatId = Random.Shared.NextInt64(100_000_000, 900_000_000);
        await TurnOnAsync(owner);
        await LinkAsync(app, As(app, office, readerId, Roles.Reader), office, chatId);

        await SendAsync(app, office, chatId, "Merhaba");
        Assert.StartsWith("Kişisel sohbet açma yetkiniz yok", telegram.Sent[^1].Text);
        await SendAsync(app, office, chatId, "/kisisel");
        Assert.StartsWith("Kişisel sohbet açma yetkiniz yok", telegram.Sent[^1].Text);

        await CreateMatterAsync(owner, "Miras davası", readerId);
        await SendAsync(app, office, chatId, "/dosya miras");
        Assert.StartsWith("«Miras davası» dosyasına geçtim", telegram.Sent[^1].Text);
        await SendAsync(app, office, chatId, "Özet ver");
        Assert.Equal($"{readerId}|False|Özet ver", telegram.Sent[^1].Text);
    }

    private static Task SayInGroupAsync(
        WebApplicationFactory<Program> app, Guid office, long groupId, long from, string text,
        IReadOnlyList<TelegramUser>? joined = null, TelegramUser? left = null)
        => DeliverAsync(app, office, new TelegramMessage(
            1, new TelegramUser(from, false, "Üye", null), new TelegramChat(groupId, "group", "Dosya grubu"), text,
            NewChatMembers: joined, LeftChatMember: left));

    [Fact]
    public async Task A_group_bound_to_a_matter_answers_only_while_everyone_in_it_may_see_the_matter()
    {
        var (app, telegram) = Host();
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var (aliId, ayseId) = (await AddLawyerAsync(office), await AddLawyerAsync(office));
        var owner = As(app, office, ownerId, Roles.OfficeOwner);
        await TurnOnAsync(owner);
        var ali = Random.Shared.NextInt64(100_000_000, 900_000_000);
        var ayse = ali + 1;
        var stranger = new TelegramUser(ali + 2, false, "Yabancı", null);
        var groupId = -Random.Shared.NextInt64(100_000_000, 900_000_000);
        await LinkAsync(app, As(app, office, aliId, Roles.Lawyer), office, ali);
        await LinkAsync(app, As(app, office, ayseId, Roles.Lawyer), office, ayse);
        var matter = await CreateMatterAsync(owner, "Kira tespit davası", aliId);
        await CreateMatterAsync(owner, "Başka dosya", ayseId);

        // Ali binds the group to his matter; only he and the bot are in it.
        telegram.MemberCounts[groupId] = 2;
        await SayInGroupAsync(app, office, groupId, ali, "/baglan kira tespit");
        Assert.StartsWith("Bu grup «Kira tespit davası» dosyasına bağlandı", telegram.Sent[^1].Text);
        await using (var db = factory.OwnerContext())
        {
            Assert.Equal(matter, (await db.TelegramGroups.AsNoTracking().SingleAsync(g => g.ChatId == groupId)).MatterId);
        }

        // Talk not addressed to the bot is left alone; a question to it is answered as Ali.
        var before = telegram.Sent.Count;
        await SayInGroupAsync(app, office, groupId, ali, "toplantı saat kaçtaydı?");
        Assert.Equal(before, telegram.Sent.Count);
        await SayInGroupAsync(app, office, groupId, ali, "@korkut_test_bot Duruşma ne zaman?");
        Assert.Equal((groupId, $"{aliId}|False|Duruşma ne zaman?"), telegram.Sent[^1]);

        // Someone who is not a member joins: nothing is shared while they are there.
        telegram.MemberCounts[groupId] = 3;
        await SayInGroupAsync(app, office, groupId, ali, string.Empty, joined: [stranger]);
        await SayInGroupAsync(app, office, groupId, ali, "@korkut_test_bot Özet ver");
        Assert.Equal("Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum.", telegram.Sent[^1].Text);
        telegram.MemberCounts[groupId] = 2;
        await SayInGroupAsync(app, office, groupId, ali, string.Empty, left: stranger);
        await SayInGroupAsync(app, office, groupId, ali, "@korkut_test_bot Özet ver");
        Assert.Equal($"{aliId}|False|Özet ver", telegram.Sent[^1].Text);

        // Ayşe is a member of the office but not of this matter's team.
        telegram.MemberCounts[groupId] = 3;
        await SayInGroupAsync(app, office, groupId, ayse, "@korkut_test_bot Merhaba");
        Assert.Equal("Bu dosya için yetkiniz yok.", telegram.Sent[^1].Text);
        await SayInGroupAsync(app, office, groupId, ali, "@korkut_test_bot Özet ver");
        Assert.Equal("Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum.", telegram.Sent[^1].Text);
        await SayInGroupAsync(app, office, groupId, ayse, "/baglan başka dosya");
        Assert.Equal("Bu dosya için yetkiniz yok.", telegram.Sent[^1].Text); // not hers to move

        // Once she is on the team, the group may talk again.
        Assert.True((await owner.PostAsJsonAsync($"/api/matters/{matter}/team", new { user_id = ayseId }, Json)).IsSuccessStatusCode);
        await SayInGroupAsync(app, office, groupId, ayse, "@korkut_test_bot Merhaba");
        Assert.Equal($"{ayseId}|False|Merhaba", telegram.Sent[^1].Text);

        // Someone the bot never saw (it was not an admin when they joined) counts as a stranger.
        telegram.MemberCounts[groupId] = 4;
        await SayInGroupAsync(app, office, groupId, ali, "@korkut_test_bot Özet ver");
        Assert.Equal("Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum.", telegram.Sent[^1].Text);
    }

    [Fact]
    public async Task Membership_updates_keep_track_of_who_is_in_a_group()
    {
        var (app, _) = Host();
        var (office, _) = await factory.SeedOfficeAsync();
        var groupId = -Random.Shared.NextInt64(100_000_000, 900_000_000);
        var someone = new TelegramUser(Random.Shared.NextInt64(100_000_000, 900_000_000), false, "Biri", null);

        async Task ChangeAsync(string status)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(
                new InstallOffice(office, "Test Bürosu", false), GoodToken,
                new TelegramUpdate(1, null, new TelegramChatMemberUpdated(new TelegramChat(groupId, "supergroup"), new TelegramChatMember(status, someone))));
        }

        async Task<bool> KnownAsync()
        {
            await using var db = factory.OwnerContext();
            return await db.TelegramGroupMembers.AnyAsync(m => m.ChatId == groupId && m.TelegramUserId == someone.Id);
        }

        await ChangeAsync("member");
        Assert.True(await KnownAsync());
        await ChangeAsync("kicked");
        Assert.False(await KnownAsync());
    }
}
