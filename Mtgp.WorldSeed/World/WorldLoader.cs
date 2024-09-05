using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mtgp.WorldSeed.World;

internal static class WorldLoader
{
	private static readonly IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

	public static WorldDefinition LoadFromFolder(string folderPath)
	{
		var baseAreaFile = File.ReadAllText(Path.Combine(folderPath, ".area"));

		var world = deserializer.Deserialize<WorldDefinition>(baseAreaFile);

		foreach (var areaFile in Directory.EnumerateFiles(folderPath, "*.md"))
		{
			var areaName = "./" + Path.GetRelativePath(folderPath, areaFile);

			var fileContents = File.ReadAllText(areaFile);

			var areaDoc = Markdown.Parse(fileContents);

			var titleInline = ((HeadingBlock)areaDoc.First()).Inline!;

			var title = titleInline.OfType<LiteralInline>().First().Content.ToString()!;

			var descriptionBuilder = new StringBuilder();

			foreach (var child in areaDoc.Skip(1))
			{
				if (child is ParagraphBlock paragraph)
				{
					if (paragraph.Inline!.FirstChild is LiteralInline)
					{
						if (descriptionBuilder.Length > 0)
						{
							descriptionBuilder.AppendLine();
							descriptionBuilder.AppendLine();
						}

						descriptionBuilder.Append(string.Join(" ", paragraph.Inline!.OfType<LiteralInline>().Select(x => x.Content.ToString())));
					}
					else if (paragraph.Inline!.FirstChild is LinkInline link)
					{
						world.Links.Add(new(((LiteralInline)link.FirstChild!).Content.ToString(), areaName, link.Url!));
					}
				}
			}

			var description = ((ParagraphBlock)areaDoc.Skip(1).First()).Inline!.OfType<LiteralInline>().First().Content.ToString()!;

			world.Locations[areaName] = new(title, description);
		}

		return world;
	}
}
