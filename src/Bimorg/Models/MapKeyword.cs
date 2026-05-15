namespace Bimorg.Models;

public sealed class MapKeyword
{
    public int Id { get; set; }
    public int BattleMapId { get; set; }
    public required string Word { get; set; }
    public BattleMap? BattleMap { get; set; }
}