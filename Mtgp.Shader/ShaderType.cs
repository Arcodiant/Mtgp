namespace Mtgp.Shader;

public record ShaderType(int Size)
{
	public static ShaderType Float32 { get; } = new(4);
	public static ShaderType Int32 { get; } = new(4);
}