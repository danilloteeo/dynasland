using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Shortcuts;

namespace Dynasland.App.ViewModels.Settings;

public sealed partial class ShortcutItemViewModel(GlobalShortcutBinding binding, ShortcutsSettingsViewModel owner) : ObservableObject
{
    public string Id => binding.Shortcut.Id;

    public string Label => binding.Shortcut.Label;

    public string Category => binding.Shortcut.Category;

    public string GestureText => binding.Gesture?.ToString() ?? "Sem atalho";

    public bool HasGesture => binding.Gesture is not null;

    public bool CanReset => binding.IsCustom || (binding.Gesture is null && binding.Shortcut.DefaultGesture is not null);

    public string? Error => binding.Error;

    public bool HasError => !string.IsNullOrEmpty(binding.Error);

    public string DefaultText => binding.Shortcut.DefaultGesture is { } gesture ? $"Padrão: {gesture}" : "Sem atalho padrão";

    [ObservableProperty]
    private bool _isRecording;

    [RelayCommand]
    private void Record() => owner.StartRecording(this);

    [RelayCommand]
    private Task ClearAsync() => owner.SetAsync(Id, null);

    [RelayCommand]
    private Task ResetAsync() => owner.ResetAsync(Id);
}

/// <summary>Categoria Atalhos: combinações globais de cada ação, com gravação pelo teclado.</summary>
public sealed partial class ShortcutsSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IGlobalShortcutRegistry _registry;
    private readonly HotkeyService _hotkeys;
    private readonly IUiDispatcher _ui;

    public ShortcutsSettingsViewModel(IGlobalShortcutRegistry registry, HotkeyService hotkeys, IUiDispatcher ui)
    {
        _registry = registry;
        _hotkeys = hotkeys;
        _ui = ui;
        registry.Changed += OnRegistryChanged;
        Refresh();
    }

    public ObservableCollection<ShortcutItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecording))]
    private ShortcutItemViewModel? _recording;

    [ObservableProperty]
    private string? _message;

    public bool IsRecording => Recording is not null;

    public void StartRecording(ShortcutItemViewModel item)
    {
        if (Recording is { } previous)
        {
            previous.IsRecording = false;
        }

        Recording = item;
        item.IsRecording = true;
        Message = $"Pressione a nova combinação para “{item.Label}” (Esc cancela).";

        // Enquanto grava, os atalhos atuais não disparam (senão a própria combinação executaria a ação).
        _hotkeys.IsPaused = true;
    }

    /// <summary>Tecla pressionada durante a gravação (código virtual do Windows e modificadores).</summary>
    /// <returns><c>true</c> se a tecla foi consumida.</returns>
    public bool OnKey(int virtualKey, HotkeyModifiers modifiers)
    {
        if (Recording is not { } item)
        {
            return false;
        }

        if (virtualKey == 0x1B)
        {
            StopRecording("Gravação cancelada.");
            return true;
        }

        var gesture = new HotkeyGesture(modifiers, virtualKey);
        if (!gesture.IsValid)
        {
            // Só modificadores, ou sem Ctrl/Alt/Win: espera a combinação completa.
            Message = modifiers == HotkeyModifiers.None || modifiers == HotkeyModifiers.Shift
                ? "Use Ctrl, Alt ou Win junto com outra tecla."
                : Message;
            return true;
        }

        StopRecording(null);
        _ = SetAsync(item.Id, gesture);
        return true;
    }

    public Task SetAsync(string id, HotkeyGesture? gesture) => _registry.SetGestureAsync(id, gesture);

    public Task ResetAsync(string id) => _registry.ResetAsync(id);

    /// <summary>Relê as combinações gravadas (ex.: depois de restaurar os padrões).</summary>
    public Task ReloadAsync() => _registry.LoadAsync();

    public void Dispose()
    {
        _registry.Changed -= OnRegistryChanged;
        _hotkeys.IsPaused = false;
    }

    private void StopRecording(string? message)
    {
        if (Recording is { } item)
        {
            item.IsRecording = false;
        }

        Recording = null;
        Message = message;
        _hotkeys.IsPaused = false;
    }

    private void OnRegistryChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void Refresh()
    {
        var recordingId = Recording?.Id;
        Items.Clear();
        foreach (var binding in _registry.Bindings.OrderBy(b => b.Shortcut.Category, StringComparer.CurrentCulture).ThenBy(b => b.Shortcut.Label, StringComparer.CurrentCulture))
        {
            var item = new ShortcutItemViewModel(binding, this) { IsRecording = binding.Shortcut.Id == recordingId };
            Items.Add(item);
            if (item.IsRecording)
            {
                Recording = item;
            }
        }
    }
}
