using Discord;
using Discord.Webhook;

namespace SecretKeeper.Services;
public class NotifierService
{
    private DiscordWebhookClient? _client;
    private ulong? _discordUserId;

    public void InitializeWebhookUrl(string webhookUrl, ulong? userId)
    {
        _client = new DiscordWebhookClient(webhookUrl);
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
                new EmbedBuilder()
                    .WithTitle("Notification from SecretKeeper")
                    .AddField(title, message)
                    .Build()
            ]
        );
    }
}
