namespace Mtgp.WorldSeed.World;

internal class WorldDefinition
{
	public readonly Dictionary<string, LocationDefinition> Locations = [];

	public readonly List<LinkDefinition> Links = [];

	public required string StartingArea { get; set; }
}
