namespace Mtgp.Shader;

public class VertexShader
{
	public VertexInputMapping[] InputMappings { get; } =
		[
			new() { Location = 0, AttachmentIndex = 1, Offset = 0, Type = ShaderType.Int32 }, // X
			new() { Location = 1, AttachmentIndex = 1, Offset = 4, Type = ShaderType.Int32 }, // Y
			new() { Location = 2, AttachmentIndex = 1, Offset = 8, Type = ShaderType.Int32 }, // Length
		];

	public void Execute(ShaderInterpreter.Builtins inputBuiltins, ReadOnlySpan<byte> input, Span<byte> output)
	{
		int initialX = BitConverter.ToInt32(input[0..][..4]);
		int initialY = BitConverter.ToInt32(input[4..][..4]);
		int length = BitConverter.ToInt32(input[8..][..4]);

		(int x, int y) = inputBuiltins.VertexIndex switch
		{
			1 => (initialX + length - 1, initialY),
			_ => (initialX, initialY)
		};

		(output[0], output[1], output[2], output[3]) = ((byte)x, (byte)y, (byte)x, (byte)y);
	}
}

public struct VertexInputMapping
{
	public required int Location { get; set; }
	public required ShaderType Type { get; set; }
	public int? AttachmentIndex { get; set; }
	public int? Offset { get; set; }
}

public class ShaderType
{
	private ShaderType(int size)
	{
		this.Size = size;
	}

	public int Size { get; set; }

	public static ShaderType Float32 { get; } = new(4);
	public static ShaderType Int32 { get; } = new(4);
}