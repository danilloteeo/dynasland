using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace Dynasland.App.Services;

/// <summary>
/// Liga a página web das configurações (WebView2) ao view model: envia o estado inteiro como JSON sempre que
/// algo muda e aplica o que a página pede. A página só alcança propriedades e comandos públicos do próprio
/// view model (por caminho, ex.: <c>Privacy.ClipboardHistoryEnabled</c> ou <c>Modules[2].IsEnabled</c>).
/// </summary>
public sealed class SettingsBridge : IDisposable
{
    private const int MaxDepth = 6;

    /// <summary>Métodos públicos sem parâmetros que a página pode chamar além dos comandos.</summary>
    private static readonly HashSet<string> CallableMethods = new(StringComparer.Ordinal) { "RestoreDefaultsAsync" };

    private readonly object _root;
    private readonly Action<string> _post;
    private readonly Action<string>? _action;
    private readonly DispatcherTimer _debounce;
    private readonly HashSet<object> _attached = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    public SettingsBridge(object root, Action<string> post, Dispatcher dispatcher, Action<string>? action = null)
    {
        _root = root;
        _post = post;
        _action = action;
        _debounce = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Background, OnDebounce, dispatcher);
        _debounce.Stop();
    }

    /// <summary>Trata uma mensagem da página: <c>ready</c>, <c>set</c> ou <c>call</c>.</summary>
    public void Handle(string? json)
    {
        if (_disposed || string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var message = document.RootElement;
            var path = message.TryGetProperty("path", out var p) ? p.GetString() : null;
            switch (message.GetProperty("type").GetString())
            {
                case "ready":
                    PostState();
                    break;
                case "set" when path is not null:
                    Set(path, message.GetProperty("value"));
                    break;
                case "call" when path is not null:
                    Call(path, message.TryGetProperty("arg", out var arg) ? arg : null);
                    break;
                case "action" when message.TryGetProperty("name", out var name) && name.GetString() is { } action:
                    // Ações que precisam de janelas do Windows (exportar, importar, restaurar): a janela decide.
                    _action?.Invoke(action);
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or TargetInvocationException)
        {
            // Mensagem inválida da página: ignora (o estado seguinte volta a corrigir a tela).
            System.Diagnostics.Debug.WriteLine($"SettingsBridge: {ex.Message}");
            PostState();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce.Stop();
        foreach (var item in _attached)
        {
            if (item is INotifyPropertyChanged property)
            {
                property.PropertyChanged -= OnChanged;
            }

            if (item is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= OnChanged;
            }
        }

        _attached.Clear();
    }

    private void PostState()
    {
        if (_disposed)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["type"] = "state", ["data"] = Describe(_root, 0) });
        _post(json);
    }

    private void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();
        PostState();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (!_disposed && !_debounce.IsEnabled)
        {
            _debounce.Start();
        }
    }

    // ---- estado -> JSON ----

    private object? Describe(object? value, int depth)
    {
        switch (value)
        {
            case null:
                return null;
            case string or bool or int or long or double or decimal:
                return value;
            case float single:
                return (double)single;
            case Enum e:
                return e.ToString();
            case DateTime or DateTimeOffset or TimeSpan:
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            case IEnumerable list when depth < MaxDepth:
                Attach(value);
                return list.Cast<object?>().Select(item => Describe(item, depth + 1)).ToList();
        }

        var type = value.GetType();
        if (depth >= MaxDepth || type.Namespace?.StartsWith("Dynasland.", StringComparison.Ordinal) != true)
        {
            return null;
        }

        Attach(value);
        var result = new Dictionary<string, object?>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || !IsExposed(property.PropertyType))
            {
                continue;
            }

            try
            {
                result[property.Name] = Describe(property.GetValue(value), depth + 1);
            }
            catch (TargetInvocationException)
            {
                // Propriedade que falha ao ler (ex.: serviço parado): fica de fora.
            }
        }

        return result;
    }

    private static bool IsExposed(Type type) =>
        !typeof(ICommand).IsAssignableFrom(type)
        && !typeof(Delegate).IsAssignableFrom(type)
        && !typeof(Type).IsAssignableFrom(type)
        && type.Namespace?.StartsWith("System.Windows", StringComparison.Ordinal) != true
        && type.Namespace?.StartsWith("System.Threading", StringComparison.Ordinal) != true;

    private void Attach(object value)
    {
        if (!_attached.Add(value))
        {
            return;
        }

        if (value is INotifyPropertyChanged property)
        {
            property.PropertyChanged += OnChanged;
        }

        if (value is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnChanged;
        }
    }

    // ---- pedidos da página ----

    private void Set(string path, JsonElement value)
    {
        var (target, property) = ResolveProperty(path);
        if (!property.CanWrite)
        {
            return;
        }

        property.SetValue(target, ToClr(value, property.PropertyType));
    }

    private void Call(string path, JsonElement? arg)
    {
        var segments = path.Split('.');
        var owner = segments.Length == 1 ? _root : ResolveObject(string.Join('.', segments[..^1]));
        var name = segments[^1];
        var type = owner.GetType();

        if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner) is ICommand command)
        {
            object? parameter = arg is { } element ? ToClr(element, typeof(object)) : null;
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }

            return;
        }

        if (CallableMethods.Contains(name) && type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is { } method)
        {
            _ = method.Invoke(owner, null);
        }
    }

    private (object Target, PropertyInfo Property) ResolveProperty(string path)
    {
        var segments = path.Split('.');
        var owner = segments.Length == 1 ? _root : ResolveObject(string.Join('.', segments[..^1]));
        var property = owner.GetType().GetProperty(segments[^1], BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException($"Propriedade desconhecida: {path}");
        return (owner, property);
    }

    /// <summary>Segue um caminho como <c>Modules[2].Options[0]</c> a partir do view model raiz.</summary>
    private object ResolveObject(string path)
    {
        var current = _root;
        foreach (var segment in path.Split('.'))
        {
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? segment : segment[..bracket];
            current = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(current)
                ?? throw new ArgumentException($"Caminho inválido: {path}");
            if (bracket >= 0)
            {
                var index = int.Parse(segment[(bracket + 1)..segment.IndexOf(']', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
                current = (current as IList)?[index] ?? throw new ArgumentException($"Índice inválido: {path}");
            }
        }

        return current;
    }

    private static object? ToClr(JsonElement value, Type target)
    {
        var type = Nullable.GetUnderlyingType(target) ?? target;
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when type.IsEnum => Enum.Parse(type, value.GetString()!, ignoreCase: true),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when type == typeof(object) => value.GetDouble(),
            JsonValueKind.Number => System.Convert.ChangeType(value.GetDouble(), type, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("Valor não suportado."),
        };
    }
}
