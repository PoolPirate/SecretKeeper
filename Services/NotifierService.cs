using Discord;
using Discord.Webhook;

namespace SecretKeeper.Services;
public class NotifierService
{
    private DiscordWebhookClient? _client;
    private string? _webhookUserName;
    private ulong? _discordUserId;

    public void InitializeWebhookUrl(string webhookUrl, string? webhookUsername, ulong? userId)
    {
        _client = new DiscordWebhookClient(webhookUrl);
        _webhookUserName = webhookUsername;
        _discordUserId = userId;
    }

    public async Task SendNotificationAsync(string title, string message, bool isHighPriority)
    {
        if(_client is null)
        {
            return;
        }

        await _client.SendMessageAsync(
            isHighPriority && _discordUserId.HasValue ? MentionUtils.MentionUser(_discordUserId.Value) : null,
            embeds: [
                CreateEmbed()
                    .WithTitle("Notification from SecretKeeper")
                    .AddField(title, message)
                    .Build()
            ]
        );
    }

    private EmbedBuilder CreateEmbed()
    {
        var builder = new EmbedBuilder();

        if (_webhookUserName is not null)
        {
            builder.WithAuthor(_webhookUserName);
        }

        return builder;
    }
}
