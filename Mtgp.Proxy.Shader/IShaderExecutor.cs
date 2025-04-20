using Mtgp.Shader;
using System;

namespace Mtgp.Proxy.Shader;

public interface IShaderExecutor
{
	ShaderIoMappings InputMappings { get; }
	ShaderIoMappings OutputMappings { get; }
	void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, Span<byte> input, Span<byte> output);
}

public record ShaderIoMappings(Dictionary<int, int> Locations, Dictionary<Builtin, int> Builtins, int Size)
{
	public Span<byte> GetLocation(Span<byte> data, int location, int index = 0)
	{
		if (!this.Locations.TryGetValue(location, out int offset))
		{
			throw new KeyNotFoundException($"Location {location} not found in mappings.");
		}

		return data[(index * this.Size + offset)..];
	}

	public Span<byte> GetBuiltin(Span<byte> data, Builtin builtin, int index = 0)
	{
		if (!this.Builtins.TryGetValue(builtin, out int offset))
		{
			throw new KeyNotFoundException($"Builtin {builtin} not found in mappings.");
		}

		return data[(index * this.Size + offset)..];
	}
}

public class ShaderIoMappingsBuilder
{
	private readonly Dictionary<int, int> locations = [];
	private readonly Dictionary<Builtin, int> builtins = [];
	private int runningOffset = 0;

	public ShaderIoMappingsBuilder AddLocation(ShaderType type, int location)
	{
		this.locations[location] = this.runningOffset;
		this.runningOffset += type.Size;
		return this;
	}

	public ShaderIoMappingsBuilder AddBuiltin(ShaderType type, Builtin builtin)
	{
		this.builtins[builtin] = this.runningOffset;
		this.runningOffset += type.Size;
		return this;
	}

	public ShaderIoMappings Build() => new(locations, builtins, this.runningOffset);
}