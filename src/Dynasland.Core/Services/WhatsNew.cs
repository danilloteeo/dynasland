namespace Dynasland.Core.Services;

/// <summary>Uma página da janela de novidades.</summary>
public sealed record WhatsNewPage(string Title, string Text);

/// <summary>Novidades mostradas uma vez depois de atualizar, e a regra de quando mostrar.</summary>
public static class WhatsNew
{
    private static readonly IReadOnlyList<WhatsNewPage> Notes160 =
    [
        new("Interface nova", "Fundo preto, destaque branco, fonte Sora e componentes em pílula, no estilo do Volum. Vale para a ilha, a Central e as Configurações."),
        new("Ícones de contorno", "Todos os ícones passaram a ter traço fino e pontas arredondadas, na cor do tema."),
        new("Ilha mais viva", "Abas com o ícone de cada módulo, entrada e saída suaves, som animado no volume e cascata nas listas."),
    ];

    /// <summary>As páginas da versão, ou vazio se ela não tem novidades a mostrar.</summary>
    public static IReadOnlyList<WhatsNewPage> For(string version) => version == "1.6.0" ? Notes160 : [];

    /// <summary>Mostra quando a versão atual tem novidades e ainda não foi vista (ou nunca se registrou uma).</summary>
    public static bool ShouldShow(string? lastSeenVersion, string currentVersion) =>
        !string.Equals(lastSeenVersion, currentVersion, StringComparison.Ordinal) && For(currentVersion).Count > 0;
}
