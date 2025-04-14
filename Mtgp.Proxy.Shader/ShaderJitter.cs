using Mtgp.Shader;
using Sigil;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mtgp.Proxy.Shader;

internal static class JitterMethods
{
	public static int ReadInt32(Span<byte> buffer) => BitConverter.ToInt32(buffer);
	public static float ReadFloat32(Span<byte> buffer) => BitConverter.ToSingle(buffer);
	public static Span<byte> Slice(Span<byte> buffer, int start, int length) => buffer.Slice(start, length);

	public record struct Vector_2<T>(T V1, T V2)
		where T: unmanaged;

	public record struct Vector_3<T>(T V1, T V2, T V3)
		where T: unmanaged;

	public record struct Vector_4<T>(T V1, T V2, T V3, T V4)
		where T: unmanaged;

	public static Vector_2<int> Vec_Int32_2(int x, int y) => new(x, y);
	public static Vector_3<int> Vec_Int32_3(int x, int y, int z) => new(x, y, z);
	public static Vector_4<int> Vec_Int32_4(int x, int y, int z, int w) => new(x, y, z, w);

	public static Vector_2<float> Vec_Float_2(float x, float y) => new(x, y);
	public static Vector_3<float> Vec_Float_3(float x, float y, float z) => new(x, y, z);
	public static Vector_4<float> Vec_Float_4(float x, float y, float z, float w) => new(x, y, z, w);

	public static void Write<T>(Span<byte> buffer, T value)
		where T : unmanaged
		=> MemoryMarshal.Write(buffer, value);
}

public class ShaderJitter
	: IShaderExecutor
{
	private readonly static Lazy<MethodInfo> ReadInt32 = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.ReadInt32))!);
	private readonly static Lazy<MethodInfo> ReadFloat32 = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.ReadFloat32))!);
	private readonly static Lazy<MethodInfo> Slice = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.Slice))!);

	private readonly static Dictionary<(Type, int), MethodInfo> vecs = [];

	private static MethodInfo Vec<T>(int count)
	{
		var key = (typeof(T), count);

		if (!vecs.TryGetValue(key, out var methodInfo))
		{
			methodInfo = typeof(JitterMethods).GetMethod($"Vec_{typeof(T).Name}_{count}")!;
			vecs[key] = methodInfo;
		}

		return methodInfo;
	}

	private readonly static Dictionary<(Type, int), MethodInfo> writeVecs = [];

	private static MethodInfo WriteVec<T>(int count)
		=> WriteVec(typeof(T), count);

	public static MethodInfo WriteVec(Type type, int count)
	{
		var key = (type, count);

		var vectorType = count switch
		{
			2 => typeof(JitterMethods.Vector_2<>),
			3 => typeof(JitterMethods.Vector_3<>),
			4 => typeof(JitterMethods.Vector_4<>),
			_ => throw new NotImplementedException($"Vectors of size {count} are not implemented.")
		};

		vectorType = vectorType.MakeGenericType(type);

		if (!writeVecs.TryGetValue(key, out var methodInfo))
		{
			methodInfo = typeof(JitterMethods).GetMethod("Write")!.MakeGenericMethod(vectorType);
			writeVecs[key] = methodInfo;
		}

		return methodInfo;
	}

	private readonly static Lazy<MethodInfo> BitConverter__TryWriteBytes_Int32 = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.TryWriteBytes), [typeof(Span<byte>), typeof(int)])!);
	private readonly static Lazy<MethodInfo> BitConverter__TryWriteBytes_Single = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.TryWriteBytes), [typeof(Span<byte>), typeof(float)])!);
	private readonly static Lazy<MethodInfo> BitConverter__ToInt32_Span = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.ToInt32), [typeof(ReadOnlySpan<byte>)])!);

	private readonly static Lazy<MethodInfo> Span_Byte__Slice_Int32 = new(() => typeof(Span<byte>).GetMethod(nameof(Span<byte>.Slice), [typeof(int)])!);

	private readonly static Lazy<MethodInfo> Unsafe__As_Int_Byte = new(() => typeof(Unsafe).GetMethods().Single(x => x.Name == nameof(Unsafe.As) && x.GetGenericArguments().Length == 2).MakeGenericMethod([typeof(int), typeof(byte)]));

	private readonly static Lazy<MethodInfo> MemoryMarshal__CreateSpan = new(() => typeof(MemoryMarshal).GetMethod(nameof(MemoryMarshal.CreateSpan))!.MakeGenericMethod([typeof(byte)]));

	private readonly static Lazy<MethodInfo> SpanCollection__GetItem = new(() => typeof(SpanCollection).GetMethod("get_Item", [typeof(int)])!);

	private const int inputIndex = 0;
	private const int inputBuiltinsIndex = 1;
	private const int outputIndex = 2;
	private const int outputBuiltinsIndex = 3;
	private const int bufferAttachmentsIndex = 4;
	private delegate void ExecuteDelegate(SpanCollection input, ref ShaderInterpreter.Builtins inputBuiltins, SpanCollection output, ref ShaderInterpreter.Builtins outputBuiltins, SpanCollection bufferAttachments);

	private readonly ExecuteDelegate execute;

	public ShaderJitter(Memory<byte> compiledShader)
	{
		var methodEmitter = Emit<ExecuteDelegate>.NewDynamicMethod();

		var reader = new ShaderReader(compiledShader.Span);

		reader = reader.EntryPointSection(out var entryPointVariables);

		reader = reader.DecorationSection(out var builtins, out var locations, out var bindings);

		reader = reader.TypeSection(out var types, out var constants, out var variables);

		var values = new Dictionary<int, Func<Emit<ExecuteDelegate>, Emit<ExecuteDelegate>>>();

		void SetValue(int id, ShaderType type, Func<Emit<ExecuteDelegate>, Emit<ExecuteDelegate>> emitAction)
		{
			values[id] = emitAction;
			types[id] = type;
		}

		Emit<ExecuteDelegate> EmitValue(Emit<ExecuteDelegate> emitter, int id)
		{
			if (values.TryGetValue(id, out var action))
			{
				return action(emitter);
			}
			else
			{
				throw new InvalidOperationException($"Value {id} not found.");
			}
		}

		Emit<ExecuteDelegate> EmitValues(Emit<ExecuteDelegate> emitter, params int[] ids)
		{
			foreach (var id in ids)
			{
				emitter = EmitValue(emitter, id);
			}

			return emitter;
		}

		foreach (var (id, constant) in constants)
		{
			if (constant.Type == ShaderType.Int(4))
			{
				SetValue(id, constant.Type, emitter => emitter.LoadConstant(constant.Value.Int32));
			}
			else if (constant.Type == ShaderType.Float(4))
			{
				SetValue(id, constant.Type, emitter => emitter.LoadConstant(constant.Value.Float));
			}
			else
			{
				throw new NotImplementedException($"Unimplemented constant type {constant.Type}.");
			}
		}

		foreach (var (id, variable) in variables)
		{
			switch (variable.StorageClass)
			{
				case ShaderStorageClass.Output:
					{
						var local = methodEmitter.DeclareLocal(typeof(Span<byte>), $"output_variable_{id}");

						if (locations.TryGetValue(id, out uint locationValue))
						{
							methodEmitter.LoadArgumentAddress(outputIndex)
											.LoadConstant((int)locationValue)
											.Call(SpanCollection__GetItem.Value);
						}
						else if (builtins.TryGetValue(id, out Builtin builtinValue))
						{
							methodEmitter.LoadArgument(outputBuiltinsIndex)
											.LoadFieldAddress(typeof(ShaderInterpreter.Builtins).GetField(builtinValue.ToString())!)
											.Call(Unsafe__As_Int_Byte.Value)
											.LoadConstant(4)
											.Call(MemoryMarshal__CreateSpan.Value);
						}
						else
						{
							throw new InvalidOperationException($"Variable {id} has no location or builtin decoration.");
						}

						methodEmitter.StoreLocal(local);

						SetValue(id, variable.Type, emitter => emitter.LoadLocal(local));

						break;
					}
				case ShaderStorageClass.Input:
					{
						var local = methodEmitter.DeclareLocal(typeof(Span<byte>), $"input_variable_{id}");

						if (locations.TryGetValue(id, out uint locationValue))
						{
							methodEmitter.LoadArgumentAddress(inputIndex)
											.LoadConstant((int)locationValue)
											.Call(SpanCollection__GetItem.Value);
						}
						else if (builtins.TryGetValue(id, out Builtin builtinValue))
						{
							methodEmitter.LoadArgument(inputBuiltinsIndex)
											.LoadFieldAddress(typeof(ShaderInterpreter.Builtins).GetField(builtinValue.ToString())!)
											.Call(Unsafe__As_Int_Byte.Value)
											.LoadConstant(4)
											.Call(MemoryMarshal__CreateSpan.Value);
						}
						else
						{
							throw new InvalidOperationException($"Variable {id} has no location or builtin decoration.");
						}

						methodEmitter.StoreLocal(local);

						SetValue(id, variable.Type, emitter => emitter.LoadLocal(local));

						break;
					}
				case ShaderStorageClass.Uniform:
					{
						var local = methodEmitter.DeclareLocal(typeof(Span<byte>), $"uniform_variable_{id}");

						if (!bindings.TryGetValue(id, out var bindingValue))
						{
							throw new InvalidOperationException($"Variable {id} has no binding decoration.");
						}

						methodEmitter.LoadArgumentAddress(bufferAttachmentsIndex)
										.LoadConstant((int)bindingValue)
										.Call(SpanCollection__GetItem.Value);

						methodEmitter.StoreLocal(local);

						SetValue(id, variable.Type, emitter => emitter.LoadLocal(local));

						break;
					}
				default:
					throw new NotImplementedException($"Variables of storage class {variable.StorageClass} are not imolemented");
			}
		}

		bool isReturned = false;

		while (!(reader.EndOfStream || isReturned))
		{
			switch (reader.Next)
			{
				case ShaderOp.Store:
					{
						reader.Store(out int targetId, out int valueId);

						bool needsPop = false;

						var storeType = types[valueId];
						MethodInfo writeMethod;

						if (storeType == ShaderType.Int(4))
						{
							writeMethod = BitConverter__TryWriteBytes_Int32.Value;

							needsPop = true;
						}
						else if (storeType == ShaderType.Float(4))
						{
							writeMethod = BitConverter__TryWriteBytes_Single.Value;

							needsPop = true;
						}
						else if (storeType.IsVector())
						{
							var elementType = storeType.ElementType!;

							if (elementType == ShaderType.Int(4))
							{
								writeMethod = WriteVec<int>(storeType.ElementCount);
							}
							else if (elementType == ShaderType.Float(4))
							{
								writeMethod = WriteVec<float>(storeType.ElementCount);
							}
							else
							{
								throw new NotSupportedException($"Cannot store type {storeType}");
							}
						}
						else
						{
							throw new NotSupportedException($"Cannot store type {storeType}");
						}

						EmitValues(methodEmitter, targetId, valueId)
							.Call(writeMethod);

						if (needsPop)
						{
							methodEmitter.Pop();
						}

						break;
					}
				case ShaderOp.Load:
					{
						reader.Load(out int resultId, out int typeId, out int pointerId);

						var loadType = types[typeId];
						MethodInfo readMethod;

						if (loadType == ShaderType.Int(4))
						{
							readMethod = ReadInt32.Value;
						}
						else if (loadType == ShaderType.Float(4))
						{
							readMethod = ReadFloat32.Value;
						}
						else
						{
							throw new NotSupportedException($"Cannot load type {loadType}");
						}

						SetValue(resultId, loadType, emitter => EmitValue(emitter, pointerId)
														.Call(readMethod));
						break;
					}
				case ShaderOp.Add:
					{
						reader.Add(out int resultId, out int typeId, out int leftId, out int rightId);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, leftId, rightId).Add());

						break;
					}
				case ShaderOp.AccessChain:
					{
						reader.AccessChain(out int count);

						if (count != 1)
						{
							throw new NotImplementedException("Only one access chain index is implemented");
						}

						var ids = new int[count];

						reader.AccessChain(out int resultId, out int typeId, out int baseId, ids.AsSpan(), out _);

						var resultType = types[typeId];

						SetValue(resultId, resultType, emitter => EmitValue(EmitValue(emitter, baseId), ids[0])
																		.LoadConstant(resultType.ElementType!.Size)
																		.Multiply()
																		.LoadConstant(resultType.ElementType!.Size)
																		.Call(Slice.Value));

						break;
					}
				case ShaderOp.Return:
					isReturned = true;
					break;
				case ShaderOp.CompositeConstruct:
					{
						reader.CompositeConstruct(out int count);

						var ids = new int[count];

						reader.CompositeConstruct(out int resultId, out int typeId, ids.AsSpan(), out _);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, ids).Call(Vec<int>(count)));

						break;
					}
				default:
					throw new NotImplementedException($"Unimplemented op {reader.Next} in main function.");
			}

			reader = reader.Skip();
		}

		methodEmitter.Return();

		this.execute = methodEmitter.CreateDelegate();
	}

	public void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, ShaderInterpreter.Builtins inputBuiltins, SpanCollection input, ref ShaderInterpreter.Builtins outputBuiltins, SpanCollection output)
	{
		var bufferCollection = new SpanCollection();

		for (int index = 0; index < bufferAttachments.Length; index++)
		{
			bufferCollection[index] = bufferAttachments[index].Span;
		}

		this.execute(input, ref inputBuiltins, output, ref outputBuiltins, bufferCollection);
	}
}

internal static class ShaderReaderSectionExtensions
{
	public static ShaderReader EntryPointSection(this ShaderReader reader, out int[] entryPointVariables)
	{
		if (reader.Next != ShaderOp.EntryPoint)
		{
			throw new InvalidOperationException($"Invalid op {reader.Next} in EntryPoint section.");
		}

		reader.EntryPoint(out uint variableCount);

		entryPointVariables = new int[(int)variableCount];

		reader = reader.EntryPoint(entryPointVariables, out _);

		return reader;
	}

	public static ShaderReader DecorationSection(this ShaderReader reader, out Dictionary<int, Builtin> builtins, out Dictionary<int, uint> locations, out Dictionary<int, uint> bindings)
	{
		builtins = [];
		locations = [];
		bindings = [];

		while (reader.Next == ShaderOp.Decorate)
		{
			reader.Decorate(out _, out ShaderDecoration shaderDecoration);

			int target;

			switch (shaderDecoration)
			{
				case ShaderDecoration.Builtin:
					{
						reader.DecorateBuiltin(out target, out Builtin builtin);

						if (builtins.TryGetValue(target, out Builtin existingValue))
						{
							throw new InvalidOperationException($"Builtin {existingValue} already defined for variable {target}, cannot add {builtin}.");
						}
						else
						{
							builtins[target] = builtin;
						}
						break;
					}
				case ShaderDecoration.Location:
					{
						reader.DecorateLocation(out target, out uint location);

						if (locations.TryGetValue(target, out uint existingValue))
						{
							throw new InvalidOperationException($"Location {existingValue} already defined for variable {target}, cannot add {location}.");
						}
						else
						{
							locations[target] = location;
						}
						break;
					}
				case ShaderDecoration.Binding:
					{
						reader.DecorateBinding(out target, out uint binding);

						if (bindings.TryGetValue(target, out uint existingValue))
						{
							throw new InvalidOperationException($"Binding {existingValue} already defined for variable {target}, cannot add {binding}.");
						}
						else
						{
							bindings[target] = binding;
						}

						break;
					}
				default:
					throw new InvalidOperationException($"Invalid decoration {shaderDecoration}.");
			}

			reader = reader.Skip();
		}

		return reader;
	}

	public static ShaderReader TypeSection(this ShaderReader reader,
											 out Dictionary<int, ShaderType> types,
											 out Dictionary<int, (ShaderType Type, Field Value)> constants,
											 out Dictionary<int, (ShaderType Type, ShaderStorageClass StorageClass)> variables)
	{
		types = [];
		constants = [];
		variables = [];

		bool isTypeSection = true;

		while (isTypeSection)
		{
			switch (reader.Next)
			{
				case ShaderOp.TypeBool:
					{
						reader = reader.TypeBool(out int id);

						types[id] = ShaderType.Bool;

						break;
					}
				case ShaderOp.TypeInt:
					{
						reader = reader.TypeInt(out int id, out int width);

						types[id] = ShaderType.Int(width);

						break;
					}
				case ShaderOp.TypeFloat:
					{
						reader = reader.TypeFloat(out int id, out int width);

						types[id] = ShaderType.Float(width);

						break;
					}
				case ShaderOp.TypeVector:
					{
						reader = reader.TypeVector(out int id, out int componentTypeId, out int componentCount);

						types[id] = ShaderType.VectorOf(types[componentTypeId], componentCount);

						break;
					}
				case ShaderOp.TypeImage:
					{
						reader = reader.TypeImage(out int id, out int elementTypeId, out int dim);

						types[id] = ShaderType.ImageOf(types[elementTypeId], dim);

						break;
					}
				case ShaderOp.TypeRuntimeArray:
					{
						reader = reader.TypeRuntimeArray(out int id, out int elementTypeId);

						types[id] = ShaderType.RuntimeArrayOf(types[elementTypeId]);

						break;
					}
				case ShaderOp.TypePointer:
					{
						reader = reader.TypePointer(out int id, out ShaderStorageClass storageClass, out int elementTypeId);

						types[id] = ShaderType.PointerOf(types[elementTypeId], storageClass);

						break;
					}
				case ShaderOp.Variable:
					{
						reader = reader.Variable(out int id, out ShaderStorageClass storageClass, out int typeId);

						variables[id] = (types[typeId], storageClass);

						break;
					}
				case ShaderOp.Constant:
					{
						reader = reader.Constant(out int id, out int typeId, out int value);

						var type = types[typeId];

						constants[id] = (type, value);

						break;
					}
				default:
					isTypeSection = false;
					break;
			}

		}
		return reader;
	}
}