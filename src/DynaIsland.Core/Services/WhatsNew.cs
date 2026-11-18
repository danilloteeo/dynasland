namespace DynaIsland.Core.Services;

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

    private static readonly IReadOnlyList<WhatsNewPage> Notes170 =
    [
        new("Ilha igual à do Volum", "Pílula preta de 250 px com a hora no meio, o clima na esquerda e a música tocando na direita. Aberta, mostra data e hora e as páginas dos módulos, com setas e bolinhas que aparecem quando o mouse passa."),
        new("Configurações novas", "A janela de Configurações foi refeita com o visual do Volum, dentro do WebView2. As opções que não eram usadas saíram."),
        new("Cotações, placar e teclas", "Três módulos novos: dólar, euro e bitcoin em reais, o placar da NBA do dia e um aviso rápido ao ligar o Caps Lock ou o Num Lock."),
    ];

    // A 1.8.0 saiu sem páginas; quem atualiza dela (ou da 1.7.x) vê estas.
    private static readonly IReadOnlyList<WhatsNewPage> Notes181 =
    [
        new("Agora é DynaIsland", "O Dynasland mudou de nome. O programa, o ícone da bandeja e o Menu Iniciar passam a mostrar DynaIsland; o jeito de usar é o mesmo."),
        new("Seus dados foram mantidos", "As configurações e os históricos foram copiados para a pasta nova (%LocalAppData%\\DynaIsland). A pasta antiga, Dynasland, ficou intacta: pode apagá-la quando quiser."),
        new("Mais firme", "Os cliques fora da ilha e a tecla Esc agora são percebidos por um motor em C++ separado da interface, e a ilha volta para cima quando outra janela, como a barra de tarefas, passa na frente."),
    ];

    private static readonly IReadOnlyList<WhatsNewPage> Notes190 =
    [
        new("Visual novo: Vidro", "A ilha ficou translúcida, com borda clara, brilho no topo e sombra macia. A opacidade se ajusta em Configurações › Aparência."),
        new("A sua cor de destaque", "Escolha entre seis cores ou use a cor do Windows. Ela aparece nas barras, nos botões principais e nos interruptores."),
        new("Movimento mais suave", "A ilha abre e fecha sem passar do ponto, e as páginas trocam deslizando de leve para o lado."),
        new("A ilha tem olhos", "Dois olhinhos na pílula fechada piscam, olham para os lados e fecham curtindo quando toca música. Dá para desligar em Configurações › Aparência."),
    ];

    /// <summary>As páginas da versão, ou vazio se ela não tem novidades a mostrar.</summary>
    public static IReadOnlyList<WhatsNewPage> For(string version) => version switch
    {
        "1.9.0" => Notes190,
        "1.8.1" => Notes181,
        "1.7.0" => Notes170,
        "1.6.0" => Notes160,
        _ => [],
    };

    /// <summary>Mostra quando a versão atual tem novidades e ainda não foi vista (ou nunca se registrou uma).</summary>
    public static bool ShouldShow(string? lastSeenVersion, string currentVersion) =>
        !string.Equals(lastSeenVersion, currentVersion, StringComparison.Ordinal) && For(currentVersion).Count > 0;
}
