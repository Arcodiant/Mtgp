namespace Mtgp.Shader;

public record ShaderType(string Id, int Size, ShaderStorageClass? StorageClass = null, int ElementCount = 1, ShaderType? ElementType = null, ShaderType[]? Members = null)
{
	public static ShaderType Textel => new("textel", Int(4).Size + VectorOf(Float(4), 3).Size * 2, Members: [Int(4), VectorOf(Float(4), 3), VectorOf(Float(4), 3)]);
	public static ShaderType Bool => new("bool", 1);
	public static ShaderType Float(int width) => new("float", width);
	public static ShaderType Int(int width) => new("int", width);
	public static ShaderType ImageOf(ShaderType type, int dim)
		=> new("image", 4, ElementCount: dim, ElementType: type);
	public static ShaderType PointerOf(ShaderType type, ShaderStorageClass storageClass)
		=> new("ptr", 4, StorageClass: storageClass, ElementType: type);
	public static ShaderType VectorOf(ShaderType type, int count)
		=> new("vec", type.Size * count, ElementCount: count, ElementType: type);
}

public static class ShaderTypeExtensions
{
	public static bool IsInt(this ShaderType type)
		=> type.Id.StartsWith("int");
	public static bool IsFloat(this ShaderType type)
		=> type.Id.StartsWith("float");
	public static bool IsBool(this ShaderType type)
		=> type.Id.StartsWith("bool");
	public static bool IsPointer(this ShaderType type)
		=> type.Id.StartsWith("ptr");
	public static bool IsVector(this ShaderType type)
		=> type.Id.StartsWith("vec");
	public static bool IsImage(this ShaderType type)
		=> type.Id.StartsWith("image");
}