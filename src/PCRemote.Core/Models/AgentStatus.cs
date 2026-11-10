using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PCRemote.Core.Models;

public class AgentStatus : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _pairingCode;
    private DateTimeOffset? _pairingCodeExpiresAt;
    private string? _botUsername;
    private bool _isRunning;
    private bool _isTelegramConnected;
    private bool _isPaired;

    public string? PairingCode
    {
        get => _pairingCode;
        set => SetField(ref _pairingCode, value);
    }

    public DateTimeOffset? PairingCodeExpiresAt
    {
        get => _pairingCodeExpiresAt;
        set => SetField(ref _pairingCodeExpiresAt, value);
    }

    public string? BotUsername
    {
        get => _botUsername;
        set => SetField(ref _botUsername, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public bool IsTelegramConnected
    {
        get => _isTelegramConnected;
        set => SetField(ref _isTelegramConnected, value);
    }
    
    public bool IsPaired
    {
        get => _isPaired;
        set => SetField(ref _isPaired, value);
    }

    private void SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}