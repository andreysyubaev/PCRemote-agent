namespace PCRemote.Agent.Configuration;

public class TelegramSettings
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;

    public long AllowedUserId { get; set; }
}