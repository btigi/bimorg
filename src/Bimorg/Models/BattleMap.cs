namespace Bimorg.Models;

public sealed class BattleMap
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public required string Description { get; set; }
    public byte[]? ThumbnailData { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTimeOffset DateAdded { get; set; }
    public ICollection<MapKeyword> Keywords { get; set; } = new List<MapKeyword>();
}