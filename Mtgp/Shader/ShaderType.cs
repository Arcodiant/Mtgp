namespace Mtgp.Shader;

public record ShaderType(string Id, int Size, ShaderStorageClass? StorageClass = null, int ElementCount = 1, ShaderType? ElementType = null, ShaderType[]? Members = null)
{
	public static ShaderType Textel => StructOf([Int(4), VectorOf(Float(4), 3), VectorOf(Float(4), 3), Float(4)]);
	public static ShaderType Void => new("void", 0);
	public static ShaderType Bool => new("bool", 4);
	public static ShaderType Float(int width) => new("float", width);
	public static ShaderType Int(int width) => new("int", width);
	public static ShaderType ImageOf(ShaderType type, int dim)
		=> new("image", 4, ElementCount: dim, ElementType: type);
	public static ShaderType PointerOf(ShaderType type, ShaderStorageClass storageClass)
		=> new("ptr", 4, StorageClass: storageClass, ElementType: type);
	public static ShaderType RuntimeArrayOf(ShaderType type)
		=> new("rtarray", 4, ElementType: type);
	public static ShaderType VectorOf(ShaderType type, int count)
		=> new("vec", type.Size * count, ElementCount: count, ElementType: type);
	public static ShaderType StructOf(params ShaderType[] members)
		=> new("struct", members.Sum(m => m.Size), Members: members);

	public bool IsOrPointsTo(Func<ShaderType, bool> predicate)
		=> predicate(this) || (this.IsPointer() && ElementType is not null && predicate(ElementType));

	public int GetOffset(int index)
	{
		if (Members is not null)
		{
			if (index < 0 || index >= Members.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range for shader type members.");
			}

			return Members.Take(index).Sum(x => x.Size);
		}
		else if (ElementCount > 0)
		{
			if (index < 0 || index >= ElementCount)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range for shader type elements.");
			}

			return index * ElementType!.Size;
		}
		else
		{
			throw new InvalidOperationException("Shader type does not have members or elements to index into.");
		}
	}
}

public static class ShaderTypeExtensions
{
	public static bool IsInt(this ShaderType type)
		=> type.Id == "int";
	public static bool IsFloat(this ShaderType type)
		=> type.Id == "float";
	public static bool IsBool(this ShaderType type)
		=> type.Id == "bool";
	public static bool IsPointer(this ShaderType type)
		=> type.Id == "ptr";
	public static bool IsVector(this ShaderType type)
		=> type.Id == "vec";
	public static bool IsImage(this ShaderType type)
		=> type.Id == "image";
	public static bool IsStruct(this ShaderType type)
		=> type.Id == "struct";
	public static bool IsRuntimeArray(this ShaderType type)
		=> type.Id == "rtarray";
	public static bool IsVoid(this ShaderType type)
		=> type.Id == "void";
}