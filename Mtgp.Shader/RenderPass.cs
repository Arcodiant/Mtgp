using System.Runtime.CompilerServices;
using System.Text;

namespace Mtgp.Shader;

public class RenderPass
{
	private static readonly FragmentOutputMapping[] fragmentOutputMappings =
	[
		new(0),
		new(4),
		new(8)
	];

	private readonly IPresentReceiver receiver;
	private readonly VertexShader vertex;
	private readonly ShaderInterpreter fragment;

	public RenderPass(IPresentReceiver receiver, VertexShader vertex, Memory<byte> fragmentShader, (int X, int Y) viewportSize)
	{
		this.receiver = receiver;
		this.vertex = vertex;

		var (fragmentInputs, fragmentOutputs) = GetAttributes(fragmentShader);

		var fragmentInputMappings = fragmentInputs.Select(x => x.Type.Size).RunningOffset().ToArray();
		var fragmentOutputMappings = fragmentOutputs.Select(x => x.Type.Size).RunningOffset().ToArray();

		int offset = 0;

		for (int i = 0; i < fragmentOutputs.Length; i++)
		{
			fragmentOutputMappings[i] = offset;

			offset += fragmentOutputs[i].Type.Size;
		}

		this.fragment = new(fragmentShader, fragmentInputMappings, fragmentOutputMappings);
		this.ViewportSize = viewportSize;
	}

	private record ShaderAttribute(ShaderType Type, int Location);

	private static (ShaderAttribute[] Inputs, ShaderAttribute[] Outputs) GetAttributes(Memory<byte> compiledShader)
	{
		var shaderReader = new ShaderReader(compiledShader.Span);

		while (!shaderReader.EndOfStream && shaderReader.Next != ShaderOp.EntryPoint)
		{
			shaderReader = shaderReader.Skip();
		}

		if (shaderReader.EndOfStream)
		{
			throw new InvalidOperationException("No entry point found");
		}

		shaderReader.EntryPoint(out uint variableCount);

		var inputs = new List<ShaderAttribute>();
		var outputs = new List<ShaderAttribute>();
		Span<int> variables = stackalloc int[(int)variableCount];

		shaderReader.EntryPoint(variables, out _);

		shaderReader = shaderReader.EntryPoint(out _);

		var locations = new Dictionary<int, uint>();
		var storageClasses = new Dictionary<int, ShaderStorageClass>();

		while (!shaderReader.EndOfStream)
		{
			var op = shaderReader.Next;

			switch (op)
			{
				case ShaderOp.Decorate:
					shaderReader.Decorate(out int target, out var decoration);

					if (variables.Contains(target) && decoration == ShaderDecoration.Location)
					{
						shaderReader.DecorateLocation(out _, out uint location);

						locations[target] = location;
					}
					break;
				case ShaderOp.Variable:
					shaderReader.Variable(out int result, out var storageClass);

					if (variables.Contains(result))
					{
						storageClasses[result] = storageClass;
					}
					break;
			}

			shaderReader = shaderReader.Skip();
		}

		foreach (var variable in variables)
		{
			if (locations.TryGetValue(variable, out var location) && storageClasses.TryGetValue(variable, out var storageClass))
			{
				var attribute = new ShaderAttribute(ShaderType.Int32, (int)location);

				if (storageClass == ShaderStorageClass.Input)
				{
					inputs.Add(attribute);
				}
				else if (storageClass == ShaderStorageClass.Output)
				{
					outputs.Add(attribute);
				}
			}
		}

		return (inputs.ToArray(), outputs.ToArray());
	}

	public Memory<byte>[] Attachments { get; } = new Memory<byte>[8];

	public (int X, int Y) ViewportSize { get; set; }

	public void Execute(int instanceCount, int vertexCount)
	{
		const int instanceSize = 12;
		Span<byte> vertexOutput = stackalloc byte[4 * 2];
		Span<byte> vertexInput = stackalloc byte[vertex.InputMappings.Select(x => x.Type.Size).Sum()];

		Span<RuneDelta> deltas = stackalloc RuneDelta[80];
		Span<byte> output = stackalloc byte[12];
		Span<char> chars = stackalloc char[2];

		Span<byte> fragmentInput = stackalloc byte[8];

		var inputBuiltins = new ShaderInterpreter.Builtins();

		for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
		{
			for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
			{
				int vertexInputOffset = 0;

				foreach (var mapping in vertex.InputMappings)
				{
					var attachment = this.Attachments[mapping.AttachmentIndex!.Value];
					attachment.Span[(instanceIndex * instanceSize + mapping.Offset!.Value)..][..mapping.Type.Size].CopyTo(vertexInput[vertexInputOffset..]);

					vertexInputOffset += mapping.Type.Size;
				}

				inputBuiltins.VertexIndex = vertexIndex;

				this.vertex.Execute(inputBuiltins, vertexInput, vertexOutput[(vertexIndex * 4)..][..4]);
			}

			(float X, float Y) uScale = (1f, 0f);
			(float X, float Y) vScale = (0f, 1f);

			float uLength = MathF.Sqrt(uScale.X * uScale.X + uScale.Y * uScale.Y);
			uScale = (uScale.X / uLength, uScale.Y / uLength);

			float vLength = MathF.Sqrt(vScale.X * vScale.X + vScale.Y * vScale.Y);
			vScale = (vScale.X / vLength, vScale.Y / vLength);

			static float DotProduct((float X, float Y) a, (float X, float Y) b) => a.X * b.X + a.Y * b.Y;

			int fromX = vertexOutput[0];
			int fromY = vertexOutput[1];
			int fromU = vertexOutput[2];
			int fromV = vertexOutput[3];
			int toX = vertexOutput[4];
			int toY = vertexOutput[5];
			int toU = vertexOutput[6];
			int toV = vertexOutput[7];
			int deltaX = toX - fromX;
			int deltaY = toY - fromY;
			int deltaU = toU - fromU;
			int deltaV = toV - fromV;

			for (int y = fromY; y < toY + 1; y++)
			{
				float yNormalised = deltaY == 0f ? 0f : (float)(y - fromY) / deltaY;

				for (int x = fromX; x < toX + 1; x++)
				{
					float xNormalised = deltaX == 0f ? 0f : (float)(x - fromX) / deltaX;

					int u = fromU + (int)MathF.Round(deltaU * DotProduct(uScale, (xNormalised, yNormalised)));
					int v = fromV + (int)MathF.Round(deltaV * DotProduct(vScale, (xNormalised, yNormalised)));

					var outputBuiltins = new ShaderInterpreter.Builtins();

					inputBuiltins = new ShaderInterpreter.Builtins
					{
						VertexIndex = 0,
						InstanceIndex = instanceIndex,
						PositionX = x,
						PositionY = y
					};

					new BitWriter(fragmentInput)
						.Write(u)
						.Write(v);

					this.fragment.Execute(this.Attachments, inputBuiltins, fragmentInput, ref outputBuiltins, output);

					output[0] += (int)'A';

					var rune = Unsafe.As<byte, Rune>(ref output[0]);

					rune.TryEncodeToUtf16(chars, out int charsWritten);

					deltas[x] = new RuneDelta
					{
						X = x,
						Y = y,
						Value = rune,
						Foreground = (AnsiColour)output[4],
						Background = (AnsiColour)output[5]
					};
				}
			}

			this.receiver.Present(deltas[..((deltaX + 1) * (deltaY + 1))]);
		}
	}
}
