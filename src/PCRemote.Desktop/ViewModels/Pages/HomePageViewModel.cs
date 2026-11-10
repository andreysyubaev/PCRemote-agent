using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Models;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class HomePageViewModel : ViewModelBase
{
    private readonly AgentRuntime _agentRuntime;

    private readonly DispatcherTimer _pairingTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public HomePageViewModel(
        AgentRuntime agentRuntime,
        AgentStatus agentStatus)
    {
        _agentRuntime = agentRuntime;
        AgentStatus = agentStatus;

        AgentStatus.PropertyChanged += AgentStatus_PropertyChanged;

        _pairingTimer.Tick += PairingTimer_Tick;

        UpdatePairingTimer();
    }

    public AgentStatus AgentStatus { get; }

    public bool CanStartAgent => !AgentStatus.IsRunning && !IsBusy;

    public bool CanStopAgent => AgentStatus.IsRunning && !IsBusy;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "PCRemote is ready.";

    [ObservableProperty]
    public partial string RemainingPairingTime { get; set; } = "05:00";

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAgent));
        OnPropertyChanged(nameof(CanStopAgent));
    }

    [RelayCommand]
    private async Task StartAgentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Starting PCRemote Agent...";

            await _agentRuntime.StartAsync();

            StatusMessage = "PCRemote Agent is running.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopAgentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Stopping PCRemote Agent...";

            await _agentRuntime.StopAsync();

            StatusMessage = "PCRemote Agent stopped.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AgentStatus_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentStatus.IsRunning))
        {
            OnPropertyChanged(nameof(CanStartAgent));
            OnPropertyChanged(nameof(CanStopAgent));
        }

        if (e.PropertyName == nameof(AgentStatus.PairingCodeExpiresAt) ||
            e.PropertyName == nameof(AgentStatus.IsPaired))
        {
            Dispatcher.UIThread.Post(UpdatePairingTimer);
        }
    }

    private void PairingTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePairingTimer();
    }

    private void UpdatePairingTimer()
    {
        if (AgentStatus.IsPaired ||
            AgentStatus.PairingCodeExpiresAt is not DateTimeOffset expiresAt)
        {
            _pairingTimer.Stop();
            RemainingPairingTime = "05:00";
            return;
        }

        var remaining = expiresAt - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            RemainingPairingTime = "00:00";
        }
        else
        {
            var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            RemainingPairingTime =
                $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        _pairingTimer.Start();
    }
}