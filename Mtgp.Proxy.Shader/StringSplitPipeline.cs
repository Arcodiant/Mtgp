using System.Text;

namespace Mtgp.Proxy.Shader;

public class StringSplitPipeline(IEnumerable<byte[]> lineBuffer, Memory<byte> characterBuffer, Memory<byte> instanceBuffer, Memory<byte> drawCommandBuffer, int maxLineCount, int regionWidth)
	: IFixedFunctionPipeline
{
	public void Execute()
	{
		const int instanceSize = 16;

		var lines = lineBuffer.Select(Encoding.UTF32.GetString).Reverse().Take(maxLineCount).Reverse().SelectMany(SplitString).Reverse().Take(maxLineCount).Reverse();

		int characterBufferIndex = 0;
		int lineIndex = 0;
		int instanceIndex = 0;

		foreach (var line in lines)
		{
			if (!string.IsNullOrEmpty(line))
			{
				new BitWriter(characterBuffer.Span[(characterBufferIndex * 4)..]).WriteRunes(line);

				var instanceWriter = new BitWriter(instanceBuffer.Span[(instanceIndex * instanceSize)..]);

				instanceWriter.Write(0)
								.Write(lineIndex)
								.Write(characterBufferIndex)
								.Write(line.Length);

				instanceIndex++;
			}

			lineIndex++;
			characterBufferIndex += line.Length;
		}

		new BitWriter(drawCommandBuffer.Span)
			.Write(instanceIndex)
			.Write(2);
	}

	private IEnumerable<string> SplitString(string line)
	{
		line = line.TrimStart();

		if (line.Length == 0)
		{
			yield return string.Empty;
			yield break;
		}

		while (line.Length > 0)
		{
			if (line.Length <= regionWidth)
			{
				yield return line;
				line = string.Empty;
			}
			else
			{
				if (char.IsWhiteSpace(line[regionWidth]) || char.IsWhiteSpace(line[regionWidth - 1]))
				{
					yield return line[..regionWidth].TrimEnd();
					line = line[regionWidth..].TrimStart();
				}
				else
				{
					int breakPoint = line.LastIndexOf(' ', regionWidth);

					if (breakPoint == -1)
					{
						breakPoint = regionWidth;
					}

					yield return line[..breakPoint].TrimEnd();
					line = line[breakPoint..].TrimStart();

				}
			}
		}
	}
}
