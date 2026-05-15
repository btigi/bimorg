using Bimorg.Data;
using Microsoft.EntityFrameworkCore;

namespace Bimorg.Services;

public sealed class MapsSearchService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<BattleMapSummary>> QueryAsync(SearchFilters filters, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var q = db.BattleMaps.AsNoTracking().AsQueryable();

        var terms = SplitKeywordTerms(filters.KeywordText);
        foreach (var t in terms)
        {
            var term = t;
            q = q.Where(m => m.Keywords.Any(k => k.Word.Contains(term)));
        }

        var rows = await q
            .Select(m => new
            {
                m.Id,
                m.FileName,
                m.FilePath,
                m.Description,
                m.ThumbnailData,
                m.Width,
                m.Height,
                m.FileSizeBytes,
                m.DateAdded,
                Keywords = m.Keywords.Select(k => k.Word).OrderBy(w => w).ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .OrderByDescending(r => r.DateAdded)
            .Select(r => new BattleMapSummary(
                r.Id,
                r.FileName,
                r.FilePath,
                r.Description,
                r.ThumbnailData,
                r.Width,
                r.Height,
                r.FileSizeBytes,
                r.Keywords))
            .ToList();
    }

    private static List<string> SplitKeywordTerms(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}

public readonly record struct SearchFilters(string? KeywordText);

public readonly record struct BattleMapSummary(
    int Id,
    string FileName,
    string FilePath,
    string Description,
    byte[]? ThumbnailData,
    int Width,
    int Height,
    long FileSizeBytes,
    IReadOnlyList<string> Keywords);