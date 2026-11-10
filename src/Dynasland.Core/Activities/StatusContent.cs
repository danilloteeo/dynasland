using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Dynasland.Core.Activities;

public enum StatusKind
{
    Loading,
    Empty,
    Error,
    Info,
}

/// <summary>
/// Conteúdo padrão de estado (carregando, vazio, erro, aviso) para qualquer módulo exibir na ilha
/// com a mesma aparência. Também usado dentro de painéis próprios dos módulos.
/// </summary>
public sealed class StatusContent : IActivityContent, INotifyPropertyChanged
{
    private StatusKind _kind;
    private string _title;
    private string? _message;

    public StatusContent(StatusKind kind, string title, string? message = null, ICommand? action = null, string? actionLabel = null)
    {
        _kind = kind;
        _title = title;
        _message = message;
        Action = action;
        ActionLabel = actionLabel;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public StatusKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string? Message
    {
        get => _message;
        set => Set(ref _message, value);
    }

    /// <summary>Ação opcional (ex.: Tentar novamente).</summary>
    public ICommand? Action { get; }

    public string? ActionLabel { get; }

    public bool HasAction => Action is not null && !string.IsNullOrEmpty(ActionLabel);

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(Message))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMessage)));
        }
    }
}
