namespace Mtgp.Shader;

public record ShaderType(string Id, int Size, ShaderStorageClass? StorageClass = null, int ElementCount = 1, ShaderType? ElementType = null)
{
	public static ShaderType Float(int width) => new("float", width);
	public static ShaderType Int(int width) => new("int", width);
	public static ShaderType PointerOf(ShaderType type, ShaderStorageClass storageClass)
		=> new("ptr", 4, StorageClass: storageClass, ElementType: type);
	public static ShaderType VectorOf(ShaderType type, int count)
		=> new("vec", type.Size * count, ElementCount: count, ElementType: type);
}

public static class ShaderTypeExtensions
{
	public static bool IsPointer(this ShaderType type)
		=> type.Id.StartsWith("ptr");
	public static bool IsVector(this ShaderType type)
		=> type.Id.StartsWith("vec");
}