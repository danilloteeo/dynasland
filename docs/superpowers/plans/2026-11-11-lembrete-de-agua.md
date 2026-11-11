# Lembrete de água Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Módulo "Água": lembrete periódico na ilha com botões Bebi/Adiar, contador de copos do dia com meta, página na Central, glance na tela inicial e atalho global.

**Architecture:** `WaterModule` (Dynasland.Modules/Water) segue o `CalendarModule`: um único `ITimer` reagendado a cada mudança, opções via `ModuleOption`, glance e atalhos. O horário dos avisos é uma função pura (`WaterSchedule.Next`), alinhada ao início do dia ativo. O contador fica em `IWaterStore` (SQLite, tabela `water_log`, migration M003). A Central ganha `HubPage.Water` com `WaterPageViewModel` + `WaterPage`.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, xUnit, `FakeTimeProvider`.

**Desvios do spec (descobertos lendo o código):**
- `Ctrl+Alt+W` já é "capturar janela" (CaptureModule). O atalho da água é **`Ctrl+Alt+G`** (livre: A D H M P R S V W e Shift+S estão em uso).
- `StatusContent` só tem **uma** ação. O aviso usa um conteúdo próprio (`WaterReminderContent`) com **Bebi** e **Adiar 10 min**, mais `WaterTemplates.xaml`.
- `TemporaryDatabase.CreateRunner()` (testes) lista as migrations à mão: precisa incluir a M003.

Comandos de teste abaixo usam `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~<Nome>"`.

---

## File Structure

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `src/Dynasland.Core/Interfaces/HistoryStores.cs` | modificar | interface `IWaterStore` |
| `src/Dynasland.Core/Interfaces/IClipboardHistory.cs` | modificar | `HubPage.Water` |
| `src/Dynasland.Infrastructure/Database/Migrations/M003_WaterLog.cs` | criar | tabela `water_log` |
| `src/Dynasland.Infrastructure/Repositories/SqliteWaterStore.cs` | criar | implementação SQLite |
| `src/Dynasland.Infrastructure/DependencyInjection.cs` | modificar | registrar M003 e `IWaterStore` |
| `src/Dynasland.Modules/Water/WaterSchedule.cs` | criar | cálculo puro do próximo aviso |
| `src/Dynasland.Modules/Water/WaterReminderContent.cs` | criar | conteúdo da ilha (Bebi/Adiar) |
| `src/Dynasland.Modules/Water/WaterTemplates.xaml` | criar | templates Compact/Expanded/Interactive |
| `src/Dynasland.Modules/Water/WaterModule.cs` | criar | módulo |
| `src/Dynasland.Modules/DependencyInjection.cs` | modificar | registrar módulo e templates |
| `src/Dynasland.App/ViewModels/Hub/WaterPageViewModel.cs` | criar | página da Central |
| `src/Dynasland.App/Views/Hub/WaterPage.xaml` + `.xaml.cs` | criar | view da página |
| `src/Dynasland.App/Views/HubWindow.xaml` | modificar | template + item do menu |
| `src/Dynasland.App/App.xaml.cs` | modificar | registrar `IHubPage`, `--central=agua` |
| `src/Dynasland.Tests/Infrastructure/TemporaryDatabase.cs` | modificar | incluir M003 |
| `src/Dynasland.Tests/Infrastructure/WaterStoreTests.cs` | criar | testes do store |
| `src/Dynasland.Tests/Modules/WaterScheduleTests.cs` | criar | testes da função pura |
| `src/Dynasland.Tests/Modules/WaterModuleTests.cs` | criar | testes do módulo e da página |
| `Directory.Build.props`, `CHANGELOG.md`, spec | modificar | versão 1.5.0 e notas |

---

### Task 1: Store e migration

**Files:**
- Create: `src/Dynasland.Infrastructure/Database/Migrations/M003_WaterLog.cs`
- Create: `src/Dynasland.Infrastructure/Repositories/SqliteWaterStore.cs`
- Modify: `src/Dynasland.Core/Interfaces/HistoryStores.cs` (após `ICalendarStore`)
- Modify: `src/Dynasland.Infrastructure/DependencyInjection.cs`
- Modify: `src/Dynasland.Tests/Infrastructure/TemporaryDatabase.cs`
- Test: `src/Dynasland.Tests/Infrastructure/WaterStoreTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

`src/Dynasland.Tests/Infrastructure/WaterStoreTests.cs`:

```csharp
using Dynasland.Infrastructure.Repositories;

namespace Dynasland.Tests.Infrastructure;

public class WaterStoreTests
{
    [Fact]
    public async Task Counts_glasses_per_day_and_never_goes_below_zero()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(db.Factory);
        var monday = new DateOnly(2026, 11, 9);
        var tuesday = new DateOnly(2026, 11, 10);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        Assert.Equal(0, await store.GetAsync(monday));
        Assert.Equal(1, await store.AddAsync(monday, 1));
        Assert.Equal(2, await store.AddAsync(monday, 1));
        Assert.Equal(1, await store.AddAsync(monday, -1));
        Assert.Equal(0, await store.AddAsync(monday, -5));
        Assert.Equal(0, await store.AddAsync(tuesday, -1));

        Assert.Equal(0, await store.GetAsync(tuesday));
        Assert.Equal(1, await store.AddAsync(tuesday, 1));
        Assert.Equal(0, await store.GetAsync(monday));
        Assert.Equal(6, changes);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterStoreTests"`
Expected: erro de compilação (`SqliteWaterStore` não existe).

- [ ] **Step 3: Implementar**

Em `HistoryStores.cs`, logo depois de `ICalendarStore`:

```csharp
/// <summary>Copos de água bebidos por dia (data local).</summary>
public interface IWaterStore
{
    /// <summary>Copos do dia; 0 se nada foi registrado.</summary>
    Task<int> GetAsync(DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>Soma (ou subtrai) copos no dia, sem passar de 0 para baixo.</summary>
    /// <returns>A contagem do dia depois da alteração.</returns>
    Task<int> AddAsync(DateOnly day, int delta, CancellationToken cancellationToken = default);

    /// <summary>Disparado (em qualquer thread) quando a contagem muda.</summary>
    event EventHandler? Changed;
}
```

`M003_WaterLog.cs`:

```csharp
namespace Dynasland.Infrastructure.Database.Migrations;

/// <summary>Copos de água por dia (data local, "yyyy-MM-dd").</summary>
public sealed class M003_WaterLog : SqlMigration
{
    public override int Version => 3;

    public override string Name => "Registro de água";

    protected override string Sql => """
        CREATE TABLE water_log (
            day     TEXT PRIMARY KEY NOT NULL,
            glasses INTEGER NOT NULL
        );
        """;
}
```

`SqliteWaterStore.cs`:

```csharp
using System.Globalization;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Database;

namespace Dynasland.Infrastructure.Repositories;

public sealed class SqliteWaterStore(SqliteConnectionFactory factory) : IWaterStore
{
    public event EventHandler? Changed;

    public async Task<int> GetAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var rows = await Db.QueryAsync(factory, "SELECT glasses FROM water_log WHERE day = $day;",
            r => r.GetInt32(0), cancellationToken, ("$day", Key(day)));
        return rows.Count == 0 ? 0 : rows[0];
    }

    public async Task<int> AddAsync(DateOnly day, int delta, CancellationToken cancellationToken = default)
    {
        await Db.ExecuteAsync(factory, """
            INSERT INTO water_log (day, glasses) VALUES ($day, MAX(0, $delta))
            ON CONFLICT (day) DO UPDATE SET glasses = MAX(0, glasses + $delta);
            """,
            cancellationToken, ("$day", Key(day)), ("$delta", delta));
        Changed?.Invoke(this, EventArgs.Empty);
        return await GetAsync(day, cancellationToken);
    }

    private static string Key(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
```

Em `DependencyInjection.cs` (Infrastructure): depois de `services.AddSingleton<IMigration, M002_Histories>();` acrescente `services.AddSingleton<IMigration, M003_WaterLog>();` e depois da linha do `ICalendarStore` acrescente `services.AddSingleton<IWaterStore, SqliteWaterStore>();`.

Em `TemporaryDatabase.CreateRunner()` troque a lista por `[new M001_InitialSchema(), new M002_Histories(), new M003_WaterLog()]`.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterStoreTests"`
Expected: PASS (1 teste).

- [ ] **Step 5: Commit**

```bash
git add src/Dynasland.Core/Interfaces/HistoryStores.cs src/Dynasland.Infrastructure src/Dynasland.Tests/Infrastructure
git commit -m "Registro de agua: IWaterStore, tabela water_log e migration M003"
```

---

### Task 2: Cálculo do próximo aviso (`WaterSchedule`)

**Files:**
- Create: `src/Dynasland.Modules/Water/WaterSchedule.cs`
- Test: `src/Dynasland.Tests/Modules/WaterScheduleTests.cs`

Regra: avisos em horários fixos `início + k × intervalo`, dentro de `[início, fim)`. O próximo é o primeiro horário estritamente depois de agora; depois do último do dia, volta para o início do dia seguinte. Avisos vencidos com o app fechado simplesmente não existem.

- [ ] **Step 1: Escrever o teste que falha**

```csharp
using Dynasland.Modules.Water;

namespace Dynasland.Tests.Modules;

public class WaterScheduleTests
{
    private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(11, 8, 30, 11, 9, 0)]   // dentro do dia: próximo horário cheio
    [InlineData(11, 9, 0, 11, 10, 0)]   // exatamente num horário: o seguinte
    [InlineData(11, 7, 0, 11, 8, 0)]    // antes do início: o início
    [InlineData(11, 21, 30, 12, 8, 0)]  // 22h não vale (fim exclusivo): amanhã
    [InlineData(11, 23, 30, 12, 8, 0)]  // depois do fim: amanhã
    public void Next_reminder_follows_the_active_window(int d, int h, int m, int ed, int eh, int em) =>
        Assert.Equal(At(ed, eh, em), WaterSchedule.Next(At(d, h, m), startHour: 8, endHour: 22, intervalMinutes: 60));

    [Fact]
    public void Interval_longer_than_an_hour_stays_aligned_to_the_start()
    {
        // 8:00, 8:45, 9:30 ...
        Assert.Equal(At(11, 9, 30), WaterSchedule.Next(At(11, 9, 0), 8, 22, 45));
    }

    [Fact]
    public void Offset_of_the_local_clock_is_preserved()
    {
        var now = new DateTimeOffset(2026, 11, 11, 8, 30, 0, TimeSpan.FromHours(-3));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.FromHours(-3)), WaterSchedule.Next(now, 8, 22, 60));
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterScheduleTests"`
Expected: erro de compilação (`WaterSchedule` não existe).

- [ ] **Step 3: Implementar**

```csharp
namespace Dynasland.Modules.Water;

/// <summary>Quando lembrar de beber água: horários fixos a partir do início do dia ativo.</summary>
public static class WaterSchedule
{
    /// <summary>
    /// Primeiro horário estritamente depois de <paramref name="localNow"/>, entre
    /// <paramref name="startHour"/> (inclusive) e <paramref name="endHour"/> (exclusive), a cada
    /// <paramref name="intervalMinutes"/>. Passado o último do dia, é o início do dia seguinte.
    /// </summary>
    public static DateTimeOffset Next(DateTimeOffset localNow, int startHour, int endHour, int intervalMinutes)
    {
        var step = TimeSpan.FromMinutes(intervalMinutes);
        var start = localNow.Date.AddHours(startHour);
        var end = localNow.Date.AddHours(endHour);
        for (var slot = start; slot < end; slot += step)
        {
            if (slot > localNow.DateTime)
            {
                return new DateTimeOffset(slot, localNow.Offset);
            }
        }

        return new DateTimeOffset(start.AddDays(1), localNow.Offset);
    }

    /// <summary>O horário cai no dia ativo (<paramref name="startHour"/> inclusive, <paramref name="endHour"/> exclusive).</summary>
    public static bool IsActive(DateTimeOffset localTime, int startHour, int endHour)
    {
        var time = localTime.DateTime;
        return time >= time.Date.AddHours(startHour) && time < time.Date.AddHours(endHour);
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterScheduleTests"`
Expected: PASS (7 casos).

- [ ] **Step 5: Commit**

```bash
git add src/Dynasland.Modules/Water/WaterSchedule.cs src/Dynasland.Tests/Modules/WaterScheduleTests.cs
git commit -m "Horario dos avisos de agua: WaterSchedule"
```

---

### Task 3: Conteúdo e templates da ilha

**Files:**
- Create: `src/Dynasland.Modules/Water/WaterReminderContent.cs`
- Create: `src/Dynasland.Modules/Water/WaterTemplates.xaml`

Sem teste próprio: o conteúdo é exercitado nos testes do módulo (Task 4) e o XAML na verificação manual (Task 7).

- [ ] **Step 1: Criar o conteúdo**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;

namespace Dynasland.Modules.Water;

/// <summary>Aviso "Hora de beber água" com as ações Bebi e Adiar.</summary>
public sealed partial class WaterReminderContent(Action drink, Action snooze, string message) : ObservableObject, IActivityContent
{
    public string Glyph => "💧";

    public string Title => "Hora de beber água";

    public string Message { get; } = message;

    public string SnoozeLabel => "Adiar 10 min";

    public string DrinkLabel => "Bebi";

    [RelayCommand]
    private void Drink() => drink();

    [RelayCommand]
    private void Snooze() => snooze();
}
```

(`DrinkCommand` e `SnoozeCommand` são gerados pelo `[RelayCommand]`.)

- [ ] **Step 2: Criar os templates**

`WaterTemplates.xaml` (mesmas chaves de recurso do `TimerTemplates.xaml`/`AppTemplates.xaml`):

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <DataTemplate x:Key="Dynasland.Modules.Water.WaterReminderContent:Compact">
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
            <TextBlock Text="{Binding Glyph}" FontSize="13" VerticalAlignment="Center" />
            <TextBlock Text="Água" Margin="8,0,0,0" FontFamily="{DynamicResource IslandTextFont}" FontSize="12"
                       Foreground="{DynamicResource IslandPrimaryTextBrush}" VerticalAlignment="Center" />
        </StackPanel>
    </DataTemplate>

    <DataTemplate x:Key="Dynasland.Modules.Water.WaterReminderContent:Expanded">
        <Grid Margin="20,0">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="{Binding Glyph}" FontSize="22" VerticalAlignment="Center" />
                <StackPanel Margin="12,0,0,0" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Title}" FontFamily="{DynamicResource IslandTextFont}" FontSize="14" FontWeight="SemiBold"
                               Foreground="{DynamicResource IslandPrimaryTextBrush}" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding Message}" FontFamily="{DynamicResource IslandTextFont}" FontSize="12" TextTrimming="CharacterEllipsis"
                               Foreground="{DynamicResource IslandSecondaryTextBrush}" />
                </StackPanel>
            </StackPanel>
        </Grid>
    </DataTemplate>

    <DataTemplate x:Key="Dynasland.Modules.Water.WaterReminderContent:Interactive">
        <Grid Margin="22,16">
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="{Binding Glyph}" FontSize="26" VerticalAlignment="Top" />
                <StackPanel Margin="12,0,0,0" MaxWidth="320">
                    <TextBlock Text="{Binding Title}" FontFamily="{DynamicResource IslandTextFont}" FontSize="14" FontWeight="SemiBold"
                               Foreground="{DynamicResource IslandPrimaryTextBrush}" TextWrapping="Wrap" />
                    <TextBlock Text="{Binding Message}" FontFamily="{DynamicResource IslandTextFont}" FontSize="12" TextWrapping="Wrap" Margin="0,4,0,0"
                               Foreground="{DynamicResource IslandSecondaryTextBrush}" />
                </StackPanel>
            </StackPanel>
            <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Style="{DynamicResource IslandTextButtonStyle}" Content="{Binding SnoozeLabel}" Command="{Binding SnoozeCommand}" />
                <Button Style="{DynamicResource IslandTextButtonStyle}" Content="{Binding DrinkLabel}" Command="{Binding DrinkCommand}" Margin="8,0,0,0" />
            </StackPanel>
        </Grid>
    </DataTemplate>

</ResourceDictionary>
```

- [ ] **Step 3: Compilar**

Run: `dotnet build src/Dynasland.Modules/Dynasland.Modules.csproj`
Expected: Build succeeded, 0 erros.

- [ ] **Step 4: Commit**

```bash
git add src/Dynasland.Modules/Water
git commit -m "Conteudo e templates do aviso de agua"
```

---

### Task 4: `WaterModule`

**Files:**
- Create: `src/Dynasland.Modules/Water/WaterModule.cs`
- Modify: `src/Dynasland.Core/Interfaces/IClipboardHistory.cs` (`HubPage.Water`)
- Test: `src/Dynasland.Tests/Modules/WaterModuleTests.cs`

- [ ] **Step 1: Adicionar `HubPage.Water`**

No enum `HubPage`, depois de `Captures,` acrescente `Water,` (no fim, para não mudar os valores existentes).

- [ ] **Step 2: Escrever os testes que falham**

```csharp
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Modules.Water;
using Dynasland.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class WaterModuleTests : IDisposable
{
    // 08:30 UTC; FakeTimeProvider usa UTC como fuso local.
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 8, 30, 0, TimeSpan.Zero);
    private readonly ActivityRecorder _recorder = new();
    private readonly TemporaryDatabase _db = new();

    public void Dispose()
    {
        _recorder.Dispose();
        _db.Dispose();
    }

    private sealed record Fixture(WaterModule Module, SqliteWaterStore Store, FakeTimeProvider Time, GlanceRegistry Glances, ModuleOptionsService Options);

    private async Task<Fixture> StartAsync(DateTimeOffset now)
    {
        await _db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(_db.Factory);
        var time = new FakeTimeProvider(now);
        var glances = new GlanceRegistry();
        var options = TestOptions.Create();
        var module = new WaterModule(store, new NoHub(), _recorder.Bus, time, new ShortcutRegistry(), glances, new SilentSound(),
            options, new GlobalShortcutRegistry(new InMemorySettingsStore()), NullLogger<WaterModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        await module.RescheduleAsync();
        return new Fixture(module, store, time, glances, options);
    }

    private List<WaterReminderContent> Reminders => [.. _recorder.Posted.Select(a => a.Content).OfType<WaterReminderContent>()];

    [Fact]
    public async Task Reminds_at_the_next_slot_with_progress_and_shows_the_glance()
    {
        var f = await StartAsync(T0);

        Assert.Equal("0/8 copos", Assert.Single(f.Glances.Glances).Text);
        Assert.Empty(Reminders);

        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Assert.Equal("0 de 8 copos hoje", Reminders[0].Message);
        var posted = _recorder.Posted.Single(a => a.Content is WaterReminderContent);
        Assert.Equal(ActivityPriority.High, posted.Priority);
        Assert.True(posted.OpenInteractive);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Drinking_counts_a_glass_updates_the_glance_and_confirms()
    {
        var f = await StartAsync(T0);
        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Reminders[0].DrinkCommand.Execute(null);

        await WaitUntil(() => f.Glances.Glances.Single().Text == "1/8 copos");
        var confirmation = Assert.IsType<StatusContent>(_recorder.Posted[^1].Content);
        Assert.Equal("1 de 8 copos hoje", confirmation.Message);
        Assert.Equal(1, await f.Store.GetAsync(new DateOnly(2026, 11, 11)));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Snooze_repeats_the_reminder_after_ten_minutes_without_changing_the_count()
    {
        var f = await StartAsync(T0);
        f.Time.Advance(TimeSpan.FromMinutes(30));
        await WaitUntil(() => Reminders.Count == 1);

        Reminders[0].SnoozeCommand.Execute(null);
        await Task.Delay(50);
        f.Time.Advance(TimeSpan.FromMinutes(9));
        await Task.Delay(50);
        Assert.Single(Reminders);

        f.Time.Advance(TimeSpan.FromMinutes(1));
        await WaitUntil(() => Reminders.Count == 2);
        Assert.Equal(0, await f.Store.GetAsync(new DateOnly(2026, 11, 11)));
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Reaching_the_goal_stops_the_reminders_for_the_day_and_congratulates()
    {
        var f = await StartAsync(T0);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.Goal, 1);

        await f.Module.DrinkAsync();

        var congrats = Assert.IsType<StatusContent>(_recorder.Posted[^1].Content);
        Assert.Equal("Meta de água batida!", congrats.Title);
        f.Time.Advance(TimeSpan.FromHours(3));
        await Task.Delay(100);
        Assert.Empty(Reminders);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_count_restarts_on_the_next_day()
    {
        var f = await StartAsync(T0);
        await f.Module.DrinkAsync();
        await WaitUntil(() => f.Glances.Glances.Single().Text == "1/8 copos");

        f.Time.Advance(TimeSpan.FromHours(16)); // 00:30 do dia seguinte
        await f.Module.RescheduleAsync();

        Assert.Equal("0/8 copos", f.Glances.Glances.Single().Text);
        await f.Module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_active_window_falls_back_to_the_defaults()
    {
        var f = await StartAsync(T0);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.StartHour, 20);
        await f.Options.SetAsync(WaterModule.ModuleId, WaterModule.EndHour, 10);
        await f.Module.RescheduleAsync();

        f.Time.Advance(TimeSpan.FromMinutes(30)); // 09:00 está no horário padrão 8–22
        await WaitUntil(() => Reminders.Count == 1);
        await f.Module.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class NoHub : IHubService
    {
        public void Open(HubPage page)
        {
        }
    }
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterModuleTests"`
Expected: erro de compilação (`WaterModule` não existe).

- [ ] **Step 4: Implementar o módulo**

`src/Dynasland.Modules/Water/WaterModule.cs`:

```csharp
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Session;
using Dynasland.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Water;

/// <summary>
/// Lembrete de água: avisa na ilha em horários fixos do dia ativo (Bebi soma um copo, Adiar repete em
/// 10 min), conta os copos do dia contra a meta e mostra o progresso na tela inicial e na Central.
/// </summary>
/// <remarks>
/// Um único timer, reagendado quando a contagem ou as opções mudam e ao retomar da suspensão. Passada a
/// meta, o próximo evento é a virada do dia. Avisos que venceram com o Dynasland fechado são descartados.
/// </remarks>
public sealed class WaterModule(
    IWaterStore store,
    IHubService hub,
    IEventBus bus,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IGlanceRegistry glances,
    IAlertSound sound,
    IModuleOptions options,
    IGlobalShortcutRegistry globalShortcuts,
    ILogger<WaterModule> logger) : IIslandModule
{
    public const string ModuleId = "water";
    public const string GlanceId = "water.today";

    private const string ActivityId = "water.reminder";
    private const int DefaultStartHour = 8;
    private const int DefaultEndHour = 22;
    private static readonly TimeSpan ReminderDisplayDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SnoozeDelay = TimeSpan.FromMinutes(10);

    public static readonly ModuleOption Sound = ModuleOption.Toggle(
        "sound", "Som nos lembretes", "Toca o som de alerta do Windows quando o lembrete aparece.");

    public static readonly ModuleOption Interval = ModuleOption.Slider(
        "intervalMinutes", "Intervalo entre lembretes", "De quanto em quanto tempo lembrar, a partir do início do dia.", 60, 15, 180, 15, "min");

    public static readonly ModuleOption StartHour = ModuleOption.Slider(
        "startHour", "Início do dia", "Hora do primeiro lembrete do dia.", DefaultStartHour, 0, 23, 1, "h");

    public static readonly ModuleOption EndHour = ModuleOption.Slider(
        "endHour", "Fim do dia", "A partir desta hora não há mais lembretes. Precisa ser depois do início.", DefaultEndHour, 1, 24, 1, "h");

    public static readonly ModuleOption Goal = ModuleOption.Slider(
        "goal", "Meta diária", "Quantos copos beber por dia.", 8, 1, 20, 1, "copos");

    private readonly Lock _lock = new();
    private readonly List<IDisposable> _registrations = [];
    private ITimer? _timer;
    private bool _running;
    private DateTimeOffset? _snoozeUntil;
    private DateTimeOffset _lastReminder = DateTimeOffset.MinValue;

    public string Id => ModuleId;

    public string DisplayName => "Água";

    public string Description => "Lembra de beber água e conta os copos do dia. Acompanhe na Central (Ctrl+Alt+G registra um copo).";

    public IReadOnlyList<ModuleOption> Options => [Sound, Interval, StartHour, EndHour, Goal];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        store.Changed += OnChanged;
        options.Changed += OnOptionChanged;
        _registrations.Add(shortcuts.Register(new IslandShortcut("water.drink", ModuleId, "", "Bebi um copo", () => _ = DrinkAsync())));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "water.drink", "Registrar um copo de água", "Água",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'G'), () => _ = DrinkAsync())));
        _registrations.Add(bus.Subscribe<SessionChangedEvent>(e =>
        {
            if (e.Change == SessionChange.Resumed)
            {
                _ = RescheduleAsync();
            }
        }));
        _ = RescheduleAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        store.Changed -= OnChanged;
        options.Changed -= OnOptionChanged;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        glances.Remove(GlanceId);
        return Task.CompletedTask;
    }

    private void OnChanged(object? sender, EventArgs e) => _ = RescheduleAsync();

    private void OnOptionChanged(object? sender, (string ModuleId, string Key) change)
    {
        if (change.ModuleId == ModuleId)
        {
            _ = RescheduleAsync();
        }
    }

    /// <summary>Registra um copo (botão Bebi, atalho da ilha e atalho global).</summary>
    public async Task DrinkAsync()
    {
        try
        {
            var glasses = await store.AddAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime), 1);
            var goal = (int)options.Get(ModuleId, Goal);
            lock (_lock)
            {
                _snoozeUntil = null;
            }

            PostStatus(glasses >= goal ? "Meta de água batida!" : "Boa!", $"{glasses} de {goal} copos hoje");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao registrar o copo de água.");
        }
    }

    private void Snooze()
    {
        lock (_lock)
        {
            _snoozeUntil = time.GetLocalNow() + SnoozeDelay;
        }

        PostStatus("Adiado", "Aviso de novo em 10 min");
        _ = RescheduleAsync();
    }

    /// <summary>Atualiza o glance e agenda o próximo evento (aviso, fim do adiamento ou virada do dia).</summary>
    public async Task RescheduleAsync()
    {
        if (!_running)
        {
            return;
        }

        try
        {
            var now = time.GetLocalNow();
            var (start, end, interval, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            glances.Set(new IslandGlance(GlanceId, "", $"{glasses}/{goal} copos", Order: 2, Invoke: () => hub.Open(HubPage.Water)));

            DateTimeOffset next;
            lock (_lock)
            {
                if (glasses >= goal)
                {
                    next = new DateTimeOffset(now.Date.AddDays(1), now.Offset);
                }
                else if (_snoozeUntil is { } snoozed && snoozed > now && WaterSchedule.IsActive(snoozed, start, end))
                {
                    next = snoozed;
                }
                else
                {
                    next = WaterSchedule.Next(now, start, end, interval);
                }

                _timer?.Dispose();
                _timer = _running
                    ? time.CreateTimer(_ => _ = OnTimerAsync(), null, next - now, Timeout.InfiniteTimeSpan)
                    : null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao agendar os lembretes de água.");
        }
    }

    private async Task OnTimerAsync()
    {
        try
        {
            var now = time.GetLocalNow();
            var (start, end, _, goal) = ReadSettings();
            var glasses = await store.GetAsync(DateOnly.FromDateTime(now.DateTime));
            var due = glasses < goal
                && WaterSchedule.IsActive(now, start, end)
                && now - _lastReminder > TimeSpan.FromMinutes(1);
            if (due)
            {
                _lastReminder = now;
                lock (_lock)
                {
                    _snoozeUntil = null;
                }

                Announce(glasses, goal);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao disparar o lembrete de água.");
        }

        await RescheduleAsync();
    }

    private void Announce(int glasses, int goal)
    {
        var content = new WaterReminderContent(() => _ = DrinkAsync(), Snooze, $"{glasses} de {goal} copos hoje");
        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
        {
            Priority = ActivityPriority.High,
            Duration = ReminderDisplayDuration,
            OpenInteractive = true,
            GroupKey = ActivityId,
        });

        if (options.IsOn(ModuleId, Sound))
        {
            sound.Play();
        }
    }

    /// <summary>Resposta curta que substitui o aviso (mesmo Id) depois de Bebi ou Adiar.</summary>
    private void PostStatus(string title, string message) =>
        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, new StatusContent(StatusKind.Info, title, message))
        {
            Duration = ConfirmationDuration,
            GroupKey = ActivityId,
        });

    private (int Start, int End, int Interval, int Goal) ReadSettings()
    {
        var start = (int)options.Get(ModuleId, StartHour);
        var end = (int)options.Get(ModuleId, EndHour);
        if (end <= start)
        {
            (start, end) = (DefaultStartHour, DefaultEndHour);
        }

        return (start, end, (int)options.Get(ModuleId, Interval), (int)options.Get(ModuleId, Goal));
    }
}
```

Nota: `RelayCommand` (using) é do `WaterReminderContent`; remova o `using CommunityToolkit.Mvvm.Input;` deste arquivo se o compilador avisar que não é usado.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~WaterModuleTests"`
Expected: PASS (6 testes). Se o teste do snooze ficar instável por tempo, aumente os `Task.Delay` de 50 para 100 — não mexa na lógica.

- [ ] **Step 6: Commit**

```bash
git add src/Dynasland.Core/Interfaces/IClipboardHistory.cs src/Dynasland.Modules/Water/WaterModule.cs src/Dynasland.Tests/Modules/WaterModuleTests.cs
git commit -m "Modulo Agua: lembrete, contador de copos, meta e glance"
```

---

### Task 5: Registro no app e página da Central

**Files:**
- Modify: `src/Dynasland.Modules/DependencyInjection.cs`
- Create: `src/Dynasland.App/ViewModels/Hub/WaterPageViewModel.cs`
- Create: `src/Dynasland.App/Views/Hub/WaterPage.xaml`, `WaterPage.xaml.cs`
- Modify: `src/Dynasland.App/Views/HubWindow.xaml`, `src/Dynasland.App/App.xaml.cs`
- Test: acrescentar à `WaterModuleTests.cs` (teste do view model)

- [ ] **Step 1: Teste do view model (falha)**

Em `WaterModuleTests.cs`, acrescente `using Dynasland.App.ViewModels.Hub;` e o teste:

```csharp
    [Fact]
    public async Task Hub_page_shows_progress_and_adjusts_the_count()
    {
        await _db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(_db.Factory);
        var viewModel = new WaterPageViewModel(store, TestOptions.Create(), new InlineDispatcher(), new FakeTimeProvider(T0), NullLogger<WaterPageViewModel>.Instance);

        await viewModel.LoadAsync();
        Assert.Equal("0 de 8 copos", viewModel.ProgressText);
        Assert.Equal(0, viewModel.Progress);

        await viewModel.AddCommand.ExecuteAsync(null);
        await viewModel.AddCommand.ExecuteAsync(null);
        await viewModel.RemoveCommand.ExecuteAsync(null);

        Assert.Equal("1 de 8 copos", viewModel.ProgressText);
        Assert.Equal(1 / 8d, viewModel.Progress);
        Assert.Equal("Faltam 7 copos para a meta.", viewModel.Message);
        viewModel.Dispose();
    }
```

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~Hub_page_shows_progress"` — Expected: erro de compilação (`WaterPageViewModel` não existe).

- [ ] **Step 2: View model**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Water;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

/// <summary>Página da Central com os copos de água de hoje contra a meta.</summary>
public sealed partial class WaterPageViewModel : ObservableObject, IHubPage
{
    private readonly IWaterStore _store;
    private readonly IModuleOptions _options;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private readonly ILogger<WaterPageViewModel> _logger;

    public WaterPageViewModel(IWaterStore store, IModuleOptions options, IUiDispatcher ui, TimeProvider time, ILogger<WaterPageViewModel> logger)
    {
        _store = store;
        _options = options;
        _ui = ui;
        _time = time;
        _logger = logger;
        _store.Changed += OnChanged;
        _options.Changed += OnOptionChanged;
    }

    public HubPage Page => HubPage.Water;

    [ObservableProperty]
    private string _progressText = "0 de 8 copos";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _message = string.Empty;

    public async Task LoadAsync()
    {
        try
        {
            var glasses = await _store.GetAsync(Today());
            var goal = (int)_options.Get(WaterModule.ModuleId, WaterModule.Goal);
            _ui.Post(() =>
            {
                ProgressText = $"{glasses} de {goal} copos";
                Progress = Math.Min(1, glasses / (double)goal);
                Message = glasses >= goal
                    ? "Meta de hoje batida!"
                    : goal - glasses == 1 ? "Falta 1 copo para a meta." : $"Faltam {goal - glasses} copos para a meta.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar o registro de água.");
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        await _store.AddAsync(Today(), 1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _store.AddAsync(Today(), -1);
        await LoadAsync();
    }

    public void Dispose()
    {
        _store.Changed -= OnChanged;
        _options.Changed -= OnOptionChanged;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private void OnChanged(object? sender, EventArgs e) => _ = LoadAsync();

    private void OnOptionChanged(object? sender, (string ModuleId, string Key) change)
    {
        if (change.ModuleId == WaterModule.ModuleId)
        {
            _ = LoadAsync();
        }
    }
}
```

- [ ] **Step 3: View**

`WaterPage.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Dynasland.App.Views.Hub;

public partial class WaterPage : UserControl
{
    public WaterPage() => InitializeComponent();
}
```

`WaterPage.xaml` (estilos `SettingsTitle`, `SettingsSubtitle`, `SettingsCard`, `CardDescription`, `SettingsButton`, `SettingsAccentBrush` são os mesmos da `FocusPage`):

```xml
<UserControl x:Class="Dynasland.App.Views.Hub.WaterPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:hub="clr-namespace:Dynasland.App.ViewModels.Hub"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=hub:WaterPageViewModel}">
    <StackPanel>
        <TextBlock Text="Água" Style="{StaticResource SettingsTitle}" />
        <TextBlock Text="Copos de hoje. Ajuste o intervalo, o horário e a meta em Configurações › Módulos › Água."
                   Style="{StaticResource SettingsSubtitle}" Margin="0,6,0,0" />

        <Border Style="{StaticResource SettingsCard}" Margin="0,18,0,0" Padding="20,16">
            <StackPanel>
                <TextBlock Text="{Binding ProgressText}" FontSize="26" FontWeight="SemiBold" />
                <ProgressBar Maximum="1" Value="{Binding Progress, Mode=OneWay}" Height="10" Margin="0,12,0,0"
                             Foreground="{DynamicResource SettingsAccentBrush}" AutomationProperties.Name="Progresso da meta de água" />
                <TextBlock Text="{Binding Message}" Style="{StaticResource CardDescription}" Margin="0,10,0,0" />
                <StackPanel Orientation="Horizontal" Margin="0,16,0,0">
                    <Button Style="{StaticResource SettingsButton}" Content="Bebi um copo (+1)" Command="{Binding AddCommand}" />
                    <Button Style="{StaticResource SettingsButton}" Content="Desfazer (−1)" Command="{Binding RemoveCommand}" Margin="8,0,0,0" />
                </StackPanel>
            </StackPanel>
        </Border>
    </StackPanel>
</UserControl>
```

- [ ] **Step 4: Ligar na Central e no app**

`HubWindow.xaml`:
- em `Window.Resources`, depois do template de `CapturesPageViewModel`:
  ```xml
          <DataTemplate DataType="{x:Type hub:WaterPageViewModel}">
              <views:WaterPage />
          </DataTemplate>
  ```
- no menu, depois do botão "Capturas" (glifo provisório; se aparecer um quadrado vazio, troque por outro do Segoe Fluent Icons):
  ```xml
                      <RadioButton Style="{StaticResource NavButton}" Tag="&#xE9CA;" Content="Água"
                                   IsChecked="{Binding Section, Converter={StaticResource EnumMatch}, ConverterParameter=Water}" />
  ```

`App.xaml.cs`:
- depois de `AddTransient<IHubPage, CapturesPageViewModel>();` acrescente `builder.Services.AddTransient<IHubPage, WaterPageViewModel>();`
- no `switch` do `--central`, antes do `_ =>`, acrescente `"agua" or "água" or "water" => HubPage.Water,` e atualize o comentário para citar `agua`.

`src/Dynasland.Modules/DependencyInjection.cs`:
- `using Dynasland.Modules.Water;`
- depois de `CalendarModule`: `services.AddSingleton<IIslandModule, WaterModule>();`
- na lista de templates, depois de `"Life/LifeTemplates.xaml",`: `"Water/WaterTemplates.xaml",`

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj --filter "FullyQualifiedName~Water"`
Expected: PASS (todos os testes de água).

- [ ] **Step 6: Commit**

```bash
git add src
git commit -m "Pagina Agua na Central e registro do modulo"
```

---

### Task 6: Versão, notas e spec

**Files:**
- Modify: `Directory.Build.props` (linha `<Version>1.4.0</Version>` → `1.5.0`)
- Modify: `CHANGELOG.md` (nova seção no topo, antes de `## 1.4.0`)
- Modify: `docs/superpowers/specs/2026-11-11-lembrete-de-agua-design.md`

- [ ] **Step 1: CHANGELOG**

Insira depois de `# Notas da versão`:

```markdown
## 1.5.0 — 2026-11-11

### Novidades
- **Lembrete de água** (novo módulo): a ilha avisa de hora em hora, das 8h às 22h, com os botões
  **Bebi** e **Adiar 10 min**. Conta os copos do dia contra uma meta (8 por padrão) e, batida a meta,
  para os avisos até o dia seguinte. Intervalo, horário, meta e som ficam em Configurações › Módulos ›
  Água. O progresso aparece na tela inicial da ilha ("3/8 copos") e na nova página **Água** da Central.
  `Ctrl+Alt+G` registra um copo sem abrir nada.

```

- [ ] **Step 2: Versão**

Troque `1.4.0` por `1.5.0` em `Directory.Build.props`.

- [ ] **Step 3: Commit** (o spec já foi corrigido para `Ctrl+Alt+G` e `WaterReminderContent` junto com este plano)

```bash
git add Directory.Build.props CHANGELOG.md
git commit -m "Versao 1.5.0: lembrete de agua"
```

---

### Task 7: Verificação final

- [ ] **Step 1: Suíte completa e build**

Run: `dotnet build Dynasland.sln -c Release` — Expected: 0 erros, sem avisos novos.
Run: `dotnet test src/Dynasland.Tests/Dynasland.Tests.csproj` — Expected: tudo verde. Se algum teste existente quebrar por contagem de módulos/atalhos/migrations, corrija o teste para incluir o módulo Água (não o contrário).

- [ ] **Step 2: Conferir o que os testes não cobrem (XAML)**

Sem mexer no mouse do usuário (ver memória `feedback-no-mouse-control`): iniciar o app e abrir a página com `--central=agua`, pedir captura de tela ou pedir ao usuário para confirmar:
1. a página Água aparece, o glifo do menu não é um quadrado vazio e os botões +1/−1 atualizam o número;
2. em Configurações › Módulos › Água aparecem as 5 opções;
3. o aviso aparece na ilha com **Bebi** e **Adiar 10 min** clicáveis (para ver rápido sem esperar a hora cheia, reduza o intervalo para 15 min e comece perto de um múltiplo de 15).

Reportar com fidelidade o que foi e o que não foi verificado.
