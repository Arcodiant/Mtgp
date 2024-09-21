using System.Text;

namespace Mtgp.Proxy.Shader;

public class StringSplitPipeline(Memory<byte> characterBuffer, Memory<byte> instanceBuffer, Memory<byte> drawCommandBuffer, int maxLineCount, int regionWidth)
	: IFixedFunctionPipeline
{
	private readonly byte[] lineBuffer = new byte[maxLineCount * regionWidth];

	public void Execute(Memory<byte> pipeData)
	{
		const int instanceSize = 16;

		var newLine = Encoding.UTF32.GetString(pipeData.Span);

		new BitReader(lineBuffer.AsSpan()).Read(out int nextIndex);
		int lastIndex = 0;

		var bufferLines = new List<string>();

		while(nextIndex != 0)
		{
			var lineReader = new BitReader(lineBuffer.AsSpan(lastIndex));
			int count = (nextIndex - lastIndex) / 4 - 1;

			lineReader.Read(out lastIndex)
						.ReadRunes(out var line, count)
						.Read(out nextIndex);

			bufferLines.Add(line);
		}

		new BitWriter(lineBuffer.AsSpan(lastIndex)).Write(lastIndex + newLine.Length * 4 + 4).WriteRunes(newLine);

		bufferLines.Add(newLine);

		var lines = bufferLines.AsEnumerable().Reverse().Take(maxLineCount).Reverse().SelectMany(SplitString).Reverse().Take(maxLineCount).Reverse();

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
