namespace Mtgp.Shader;

public class FragmentStage
{
	public void Execute(Memory<byte>[] attachments, ReadOnlySpan<byte> input, Span<byte> output)
	{
		(int u, int v) = (input[0], input[1]);

		var texel = attachments[0].Span[((u + v * 80) * 4)..][..4];

		new BitWriter(output)
			.Write(texel)
			.Write((int)AnsiColour.White)
			.Write((int)AnsiColour.Black);
	}
}
