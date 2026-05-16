using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Bimorg.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace Bimorg.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MapsSearchService _search;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _keywordDebounce;

    public MainViewModel(MapsSearchService search, IConfiguration configuration, Dispatcher dispatcher)
    {
        _search = search;
        Configuration = configuration;
        _dispatcher = dispatcher;
    }

    public IConfiguration Configuration { get; }

    [ObservableProperty]
    private string _keywordsFilter = "";

    [ObservableProperty]
    private string _statusLine = "Ready.";

    [ObservableProperty]
    private int _resultCount;

    public ObservableCollection<MapTileVm> Results { get; } = new();

    [RelayCommand]
    private async Task RefreshSearchAsync()
    {
        var filters = BuildFiltersFromUi();
        StatusLine = "Searching.";
        try
        {
            var rows = await _search.QueryAsync(filters).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                Results.Clear();
                foreach (var r in rows)
                    Results.Add(new MapTileVm(r));
                ResultCount = Results.Count;
                StatusLine = ResultCount == 0 ? "No matches." : $"Showing {ResultCount} results.";
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => StatusLine = "Search failed: " + ex.Message);
        }
    }

    public Task<IReadOnlyList<string>> GetDistinctKeywordsAlphabeticalAsync(CancellationToken ct = default) =>
        _search.GetDistinctKeywordsAlphabeticalAsync(ct);

    [RelayCommand(CanExecute = nameof(CanOpenMap))]
    private void OpenMap(MapTileVm? tile)
    {
        if (tile is null)
            return;

        var path = tile.FilePath;
        if (!File.Exists(path))
        {
            StatusLine = "File not found: " + path;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusLine = "Open failed: " + ex.Message;
        }
    }

    private static bool CanOpenMap(MapTileVm? tile) => tile is not null && !string.IsNullOrWhiteSpace(tile.FilePath);

    [RelayCommand]
    private void ClearKeywords()
    {
        KeywordsFilter = "";
        _ = RefreshSearchAsync();
    }

    public void AppendKeywordToSearch(string rawWord)
    {
        var word = rawWord.Trim().ToLowerInvariant();
        if (word.Length == 0)
            return;

        foreach (var existing in EnumerateDistinctSearchTerms(KeywordsFilter))
        {
            if (string.Equals(existing, word, StringComparison.Ordinal))
                return;
        }

        var cur = KeywordsFilter.TrimEnd();
        KeywordsFilter = string.IsNullOrEmpty(cur) ? word : $"{cur} {word}";
    }

    private static IEnumerable<string> EnumerateDistinctSearchTerms(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var s in raw.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (s.Length == 0)
                continue;

            yield return s.ToLowerInvariant();
        }
    }

    partial void OnKeywordsFilterChanged(string value) => DebounceKeywords();

    private void DebounceKeywords()
    {
        _keywordDebounce?.Cancel();
        _keywordDebounce = new CancellationTokenSource();
        var token = _keywordDebounce.Token;
        _ = DebouncedKeywordRefreshAsync(token);
    }

    private async Task DebouncedKeywordRefreshAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token).ConfigureAwait(false);
            await RefreshSearchAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // :(
        }
    }

    private SearchFilters BuildFiltersFromUi() => new(KeywordsFilter);
}