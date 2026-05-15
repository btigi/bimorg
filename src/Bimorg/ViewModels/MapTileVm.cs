using Bimorg.Services;

namespace Bimorg.ViewModels;

public sealed class MapTileVm(BattleMapSummary source)
{
    public BattleMapSummary Source { get; } = source;
    public byte[]? ThumbnailData => Source.ThumbnailData;
    public string FileName => Source.FileName;
    public string FilePath => Source.FilePath;
    public string Description => Source.Description;
    public string KeywordsLabel => Source.Keywords.Count > 0 ? string.Join(", ", Source.Keywords) : "—";
    public string DimensionsLabel => $"{Source.Width}x{Source.Height}";
}