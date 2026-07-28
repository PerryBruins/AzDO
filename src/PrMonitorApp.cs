using System.Collections.ObjectModel;
using System.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using GuiAttribute = Terminal.Gui.Drawing.Attribute;
using GuiColor = Terminal.Gui.Drawing.Color;
using GuiScheme = Terminal.Gui.Drawing.Scheme;

namespace AzDoPrMonitor;

public sealed class PrMonitorApp
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly AzureDevOpsClient _client;
    private readonly PrDataService _dataService;
    private readonly string _myId;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private IApplication _app = null!;
    private Window _main = null!;
    private ListView _createdList = null!;
    private ListView _reviewingList = null!;
    private TextView _detailsView = null!;
    private Label _statusLabel = null!;
    private Label _columnHeader = null!;

    private List<PrEntry> _created = new();
    private List<PrEntry> _reviewing = new();
    private DateTime _nextRefreshAt;

    public PrMonitorApp(AzureDevOpsClient client, string org, string myId)
    {
        _client = client;
        _myId = myId;
        _dataService = new PrDataService(client, org);
    }

    public void Run()
    {
        using var app = Application.Create();
        _app = app;
        app.Init();

        _main = new Window();

        var createdFrame = new FrameView
        {
            Title = "My Pull Requests",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(55, DimPercentMode.Position)
        };
        var detailsFrame = new FrameView
        {
            Title = "Details",
            X = 0,
            Y = Pos.Bottom(createdFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        _columnHeader = new Label { X = 0, Y = 0, Width = Dim.Fill() };
        _createdList = new ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        // Kept off-screen: still fetched in the background, just not shown (hidden per request).
        _reviewingList = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        // The built-in keystroke navigator swallows plain letter keys (jump-to-item-by-letter)
        // before they ever reach KeyDown, which is why letter shortcuts silently did nothing.
        _createdList.KeystrokeNavigator = null;
        _reviewingList.KeystrokeNavigator = null;
        createdFrame.Add(_columnHeader, _createdList);
        // Column widths depend on terminal size; re-layout whenever the list is resized.
        _createdList.FrameChanged += (_, _) => RebuildCreatedListDisplay();

        _detailsView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            CanFocus = false,
            Text = "Select a PR to see details."
        };
        detailsFrame.Add(_detailsView);

        _createdList.Accepted += (_, _) => ShowCommentsForSelected();
        _createdList.ValueChanged += (_, _) => UpdateDetails();
        _createdList.RowRender += (_, e) =>
        {
            if (e.Row >= 0 && e.Row < _created.Count && Formatting.CompletionReadiness(_created[e.Row].Pr).CanComplete)
            {
                e.RowAttribute = new GuiAttribute(GuiColor.Black, GuiColor.BrightGreen);
            }
        };

        // Intercept shortcut keys directly on the focusable view, before its own
        // built-in key handling (e.g. ListView's type-ahead jump-to-item) can consume them.
        _createdList.KeyDown += HandleShortcutKey;
        _main.KeyDown += HandleShortcutKey;

        _statusLabel = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Text = "Loading..." };

        var statusBar = new StatusBar(new[]
        {
            new Shortcut(Key.C.WithAlt, "Comments", ShowCommentsForSelected),
            new Shortcut(Key.O.WithAlt, "Open in browser", OpenSelectedInBrowser),
            new Shortcut(Key.U.WithAlt, "Copy URL", CopySelectedUrlToClipboard),
            new Shortcut(Key.R.WithAlt, "Refresh now", () => _ = RefreshAsync(manual: true)),
            new Shortcut(Key.Q.WithAlt, "Quit", () => _main.RequestStop())
        });

        _main.Add(createdFrame, detailsFrame, _statusLabel, statusBar);

        // Match the details pane's colors to the PR list's, keeping ReadOnly from
        // rendering as the theme's dim/low-contrast role.
        var listScheme = _createdList.GetScheme();
        _detailsView.SetScheme(listScheme with { ReadOnly = listScheme.Normal });

        // Make the column header stand out from the row content below it.
        var headerAttr = new GuiAttribute(listScheme.Normal.Foreground, listScheme.Normal.Background, Terminal.Gui.Drawing.TextStyle.Bold);
        _columnHeader.SetScheme(listScheme with { Normal = headerAttr });

        _nextRefreshAt = DateTime.UtcNow + PollInterval;
        UpdateCountdownTitle();

        _ = PollLoopAsync();
        _ = CountdownLoopAsync();

        app.Run(_main);

        _cts.Cancel();
    }

    private async Task PollLoopAsync()
    {
        await RefreshAsync(manual: false);
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                await RefreshAsync(manual: false);
                _nextRefreshAt = DateTime.UtcNow + PollInterval;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CountdownLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                _app.Invoke(UpdateCountdownTitle);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateCountdownTitle()
    {
        var remaining = _nextRefreshAt - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        _main.Title = $"AzDO PR Monitor (refreshing in {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2})";
    }

    private async Task RefreshAsync(bool manual)
    {
        if (!await _refreshGate.WaitAsync(0, _cts.Token))
        {
            return;
        }

        try
        {
            _app.Invoke(() => _statusLabel.Text = manual ? "Refreshing..." : "Auto-refreshing...");
            var snapshot = await _dataService.FetchAsync(_myId, _cts.Token);
            _app.Invoke(() => ApplySnapshot(snapshot));
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            _app.Invoke(() => _statusLabel.Text = $"Refresh failed: {ex.Message}");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void ApplySnapshot(PrSnapshot snapshot)
    {
        _created = snapshot.CreatedByMe;
        _reviewing = snapshot.ReviewingForMe;

        RebuildCreatedListDisplay();

        var reviewingDisplay = new ObservableCollection<string>(_reviewing.Select(e => Formatting.ReviewingRow(e, _myId)));
        _reviewingList.SetSource(reviewingDisplay);

        _statusLabel.Text = $"Last refresh: {snapshot.FetchedAt:HH:mm:ss}   " +
                             $"My PRs: {_created.Count}   " +
                             "Alt+C Comments  Alt+O Open browser  Alt+U Copy URL  Alt+R Refresh  Alt+Q/Esc Quit";

        UpdateDetails();
    }

    private void RebuildCreatedListDisplay()
    {
        var (projectWidth, repoWidth) = Formatting.ComputeColumnWidths(_createdList.Frame.Width);
        _columnHeader.Text = Formatting.CreatedHeader(projectWidth, repoWidth);

        var selected = _createdList.SelectedItem;
        var display = new ObservableCollection<string>(_created.Select(e => Formatting.CreatedRow(e, projectWidth, repoWidth)));
        _createdList.SetSource(display);
        if (selected.HasValue && selected.Value < display.Count)
        {
            _createdList.SelectedItem = selected.Value;
        }
    }

    private void UpdateDetails()
    {
        var entry = GetSelectedEntry();
        _detailsView.Text = entry is null ? "Select a PR to see details." : Formatting.DetailText(entry, _myId);
    }

    private void HandleShortcutKey(object? sender, Key e)
    {
        switch (e.KeyCode)
        {
            case KeyCode.C | KeyCode.AltMask:
                ShowCommentsForSelected();
                e.Handled = true;
                break;
            case KeyCode.O | KeyCode.AltMask:
                OpenSelectedInBrowser();
                e.Handled = true;
                break;
            case KeyCode.U | KeyCode.AltMask:
                CopySelectedUrlToClipboard();
                e.Handled = true;
                break;
            case KeyCode.R | KeyCode.AltMask:
                _ = RefreshAsync(manual: true);
                e.Handled = true;
                break;
            case KeyCode.Q | KeyCode.AltMask:
            case KeyCode.Esc:
                _main.RequestStop();
                e.Handled = true;
                break;
        }
    }

    private PrEntry? GetSelectedEntry()
    {
        var idx = _createdList.SelectedItem;
        return idx.HasValue && idx.Value >= 0 && idx.Value < _created.Count ? _created[idx.Value] : null;
    }

    private void OpenSelectedInBrowser()
    {
        var entry = GetSelectedEntry();
        if (entry is null) return;
        OpenBrowser(entry.WebUrl);
    }

    private void CopySelectedUrlToClipboard()
    {
        var entry = GetSelectedEntry();
        if (entry is null) return;

        var ok = _app.Clipboard.IsSupported && _app.Clipboard.TrySetClipboardData(entry.WebUrl);
        _statusLabel.Text = ok
            ? $"Copied URL for !{entry.Pr.PullRequestId} to clipboard."
            : "Clipboard not available on this platform/terminal.";
    }

    private static GuiScheme ReadableScheme()
    {
        var normal = new GuiAttribute(GuiColor.White, GuiColor.Black);
        return new GuiScheme
        {
            Normal = normal,
            ReadOnly = normal,
            Focus = new GuiAttribute(GuiColor.Black, GuiColor.White)
        };
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch
        {
            // Best-effort; nothing sensible to do in a TUI if no browser launcher exists.
        }
    }

    private void ShowCommentsForSelected()
    {
        var entry = GetSelectedEntry();
        if (entry is null) return;

        _statusLabel.Text = $"Loading comments for !{entry.Pr.PullRequestId}...";

        _ = Task.Run(async () =>
        {
            string text;
            try
            {
                var threads = await _client.GetThreadsAsync(entry.ProjectName, entry.Pr.Repository.Id, entry.Pr.PullRequestId, _cts.Token);
                text = Formatting.RenderThreads(threads);
            }
            catch (Exception ex)
            {
                text = $"Failed to load comments: {ex.Message}";
            }

            _app.Invoke(() =>
            {
                _statusLabel.Text = $"Last refresh: reviewing !{entry.Pr.PullRequestId}";
                OpenCommentsDialog(entry, text);
            });
        });
    }

    private void OpenCommentsDialog(PrEntry entry, string text)
    {
        var dialog = new Dialog
        {
            Title = $"!{entry.Pr.PullRequestId} — {entry.Pr.Title}",
            Width = Dim.Percent(85, DimPercentMode.Position),
            Height = Dim.Percent(85, DimPercentMode.Position)
        };

        var textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            Text = text
        };
        textView.SetScheme(ReadableScheme());

        var closeButton = new Button { Text = "Close", IsDefault = true, X = Pos.Center(), Y = Pos.AnchorEnd(1) };
        closeButton.Accepted += (_, _) => dialog.RequestStop();

        dialog.Add(textView, closeButton);
        _app.Run(dialog);
    }
}
