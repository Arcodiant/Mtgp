using Mtgp.Shader;
using Sigil;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mtgp.Proxy.Shader;

internal static class JitterMethods
{
	public static int ReadInt32(Span<byte> buffer) => BitConverter.ToInt32(buffer);
	public static float ReadFloat32(Span<byte> buffer) => BitConverter.ToSingle(buffer);
	public static Span<byte> Slice(Span<byte> buffer, int start, int length) => buffer.Slice(start, length);
	public static void Copy(Span<byte> destination, Span<byte> source) => source.CopyTo(destination);

	public record struct Vector_2<T>(T V1, T V2)
		where T : unmanaged;

	public record struct Vector_3<T>(T V1, T V2, T V3)
		where T : unmanaged;

	public record struct Vector_4<T>(T V1, T V2, T V3, T V4)
		where T : unmanaged;

	public static Vector_2<int> Vec_Int32_2(int x, int y) => new(x, y);
	public static Vector_3<int> Vec_Int32_3(int x, int y, int z) => new(x, y, z);
	public static Vector_4<int> Vec_Int32_4(int x, int y, int z, int w) => new(x, y, z, w);

	public static Vector_2<float> Vec_Single_2(float x, float y) => new(x, y);
	public static Vector_3<float> Vec_Single_3(float x, float y, float z) => new(x, y, z);
	public static Vector_4<float> Vec_Single_4(float x, float y, float z, float w) => new(x, y, z, w);

	public static void Write<T>(Span<byte> buffer, T value)
		where T : unmanaged
		=> MemoryMarshal.Write(buffer, value);

	public static T Read<T>(Span<byte> buffer)
		where T : unmanaged
		=> MemoryMarshal.Read<T>(buffer);

	public static Span<byte> Gather_Int_2(Span<byte> buffer, Vector_2<int> coordinate, Vector_3<int> dimensions, int stepSize)
	{
		int x = coordinate.V1;
		int y = coordinate.V2;

		return buffer[((y * dimensions.V1 + x) * stepSize)..][..stepSize];
	}

	public static T Component_2<T>(Vector_2<T> vector, int component)
		where T : unmanaged
		=> component switch
		{
			0 => vector.V1,
			1 => vector.V2,
			_ => throw new ArgumentOutOfRangeException(nameof(component), "Component index out of range.")
		};

	public static T Component_3<T>(Vector_3<T> vector, int component)
		where T : unmanaged
		=> component switch
		{
			0 => vector.V1,
			1 => vector.V2,
			2 => vector.V3,
			_ => throw new ArgumentOutOfRangeException(nameof(component), "Component index out of range.")
		};

	public static T Component_4<T>(Vector_4<T> vector, int component)
		where T : unmanaged
		=> component switch
		{
			0 => vector.V1,
			1 => vector.V2,
			2 => vector.V3,
			3 => vector.V4,
			_ => throw new ArgumentOutOfRangeException(nameof(component), "Component index out of range.")
		};
}

public class ShaderJitter
	: ShaderExecutor
{
	private readonly static Lazy<MethodInfo> ReadInt32 = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.ReadInt32))!);
	private readonly static Lazy<MethodInfo> ReadFloat32 = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.ReadFloat32))!);
	private readonly static Lazy<MethodInfo> Slice = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.Slice))!);
	private readonly static Lazy<MethodInfo> Copy = new(() => typeof(JitterMethods).GetMethod(nameof(JitterMethods.Copy))!);

	private static Type GetType(ShaderType type)
	{
		if (type == ShaderType.Int(4))
		{
			return typeof(int);
		}
		else if (type == ShaderType.Float(4))
		{
			return typeof(float);
		}
		else if (type.IsVector())
		{
			var elementType = type.ElementType!;

			if (elementType == ShaderType.Int(4))
			{
				return typeof(JitterMethods.Vector_2<int>);
			}
			else if (elementType == ShaderType.Float(4))
			{
				return typeof(JitterMethods.Vector_2<float>);
			}
			else
			{
				throw new NotSupportedException($"Cannot load type {type}");
			}
		}
		else
		{
			throw new NotSupportedException($"Cannot load type {type}");
		}
	}

	private readonly static Dictionary<(Type, int), MethodInfo> vecs = [];

	private static MethodInfo Vec<T>(int count)
		=> Vec(typeof(T), count);

	private static MethodInfo Vec(Type type, int count)
	{
		var key = (type, count);

		if (!vecs.TryGetValue(key, out var methodInfo))
		{
			methodInfo = typeof(JitterMethods).GetMethod($"Vec_{type.Name}_{count}")!;
			vecs[key] = methodInfo;
		}

		return methodInfo;
	}

	private readonly static Dictionary<(Type, int), MethodInfo> writeVecs = [];
	private readonly static Dictionary<(Type, int), MethodInfo> readVecs = [];

	private static MethodInfo WriteVec<T>(int count)
		=> WriteVec(typeof(T), count);

	private static MethodInfo WriteVec(Type type, int count)
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

	private static MethodInfo ReadVec<T>(int count)
		=> ReadVec(typeof(T), count);

	private static MethodInfo ReadVec(Type type, int count)
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

		if (!readVecs.TryGetValue(key, out var methodInfo))
		{
			methodInfo = typeof(JitterMethods).GetMethod("Read")!.MakeGenericMethod(vectorType);
			readVecs[key] = methodInfo;
		}

		return methodInfo;
	}

	private static MethodInfo CompVec(Type type, int count)
	{
		return typeof(JitterMethods).GetMethod($"Component_{count}")!.MakeGenericMethod(type);
	}

	private static MethodInfo ReadByType(ShaderType loadType)
	{
		ReadByType(loadType, out var readMethod);

		return readMethod ?? throw new NotSupportedException($"Cannot load type {loadType}");
	}

	private static bool ReadByType(ShaderType loadType, [NotNullWhen(true)]out MethodInfo? readMethod)
	{
		if (loadType == ShaderType.Int(4))
		{
			readMethod = ReadInt32.Value;
		}
		else if (loadType == ShaderType.Float(4))
		{
			readMethod = ReadFloat32.Value;
		}
		else if (loadType.IsVector())
		{
			var elementType = loadType.ElementType!;

			if (elementType == ShaderType.Int(4))
			{
				readMethod = ReadVec<int>(loadType.ElementCount);
			}
			else if (elementType == ShaderType.Float(4))
			{
				readMethod = ReadVec<float>(loadType.ElementCount);
			}
			else
			{
				throw new NotSupportedException($"Cannot load type {loadType}");
			}
		}
		else if (loadType.IsStruct())
		{
			readMethod = default;

			return false;
		}
		else
		{
			throw new NotSupportedException($"Cannot load type {loadType}");
		}

		return true;
	}

	private readonly static Lazy<MethodInfo> BitConverter__TryWriteBytes_Int32 = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.TryWriteBytes), [typeof(Span<byte>), typeof(int)])!);
	private readonly static Lazy<MethodInfo> BitConverter__TryWriteBytes_Single = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.TryWriteBytes), [typeof(Span<byte>), typeof(float)])!);
	private readonly static Lazy<MethodInfo> BitConverter__ToInt32_Span = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.ToInt32), [typeof(ReadOnlySpan<byte>)])!);

	private readonly static Lazy<MethodInfo> Span_Byte__Slice_Int32 = new(() => typeof(Span<byte>).GetMethod(nameof(Span<byte>.Slice), [typeof(int)])!);
	private readonly static Lazy<ConstructorInfo> Span_Byte__Ctor_VoidPtr_Int32 = new(() => typeof(Span<byte>).GetConstructor([typeof(void*), typeof(int)])!);

	private readonly static Lazy<MethodInfo> Unsafe__As_Int_Byte = new(() => typeof(Unsafe).GetMethods().Single(x => x.Name == nameof(Unsafe.As) && x.GetGenericArguments().Length == 2).MakeGenericMethod([typeof(int), typeof(byte)]));

	private readonly static Lazy<MethodInfo> MemoryMarshal__CreateSpan = new(() => typeof(MemoryMarshal).GetMethod(nameof(MemoryMarshal.CreateSpan))!.MakeGenericMethod([typeof(byte)]));

	private readonly static Lazy<MethodInfo> SpanCollection__GetItem = new(() => typeof(SpanCollection).GetMethod("get_Item", [typeof(int)])!);

	private const int inputIndex = 0;
	private const int outputIndex = 1;
	private const int bufferAttachmentsIndex = 2;
	private const int imageAttachmentsIndex = 3;
	private const int imageDimensionsIndex = 4;
	private delegate void ExecuteDelegate(Span<byte> input, Span<byte> output, SpanCollection bufferAttachments, SpanCollection imageAttachments, SpanCollection imageDimensionCollection);

	private readonly ExecuteDelegate execute;
	public override ShaderIoMappings InputMappings { get; }
	public override ShaderIoMappings OutputMappings { get; }

	public ShaderJitter(Memory<byte> compiledShader, ShaderIoMappings inputMappings, ShaderIoMappings outputMappings)
	{
		this.InputMappings = inputMappings;
		this.OutputMappings = outputMappings;

		var methodEmitter = Emit<ExecuteDelegate>.NewDynamicMethod();

		var reader = new ShaderReader(compiledShader.Span);

		reader = reader.EntryPointSection(out var entryPointVariables);

		reader = reader.DecorationSection(out var builtins, out var locations, out var bindings);

		reader = reader.TypeSection(out var types, out var constants, out var variables);

		var values = new Dictionary<int, Func<Emit<ExecuteDelegate>, Emit<ExecuteDelegate>>>();
		var constantValues = new Dictionary<int, Field>();

		var imageDimensions = new Dictionary<int, Local>();

		void SetValue(int id, ShaderType type, Func<Emit<ExecuteDelegate>, Emit<ExecuteDelegate>> emitAction, Field? constantValue = null)
		{
			values[id] = emitAction;
			types[id] = type;

			if (constantValue != null)
			{
				constantValues[id] = constantValue.Value;
			}
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
				SetValue(id, constant.Type, emitter => emitter.LoadConstant(constant.Value.Int32), constant.Value);
			}
			else if (constant.Type == ShaderType.Float(4))
			{
				SetValue(id, constant.Type, emitter => emitter.LoadConstant(constant.Value.Float), constant.Value);
			}
			else
			{
				throw new NotImplementedException($"Unimplemented constant type {constant.Type}.");
			}
		}

		foreach (var (id, variable) in variables)
		{
			var local = methodEmitter.DeclareLocal(typeof(Span<byte>), $"{variable.StorageClass.ToString().ToLower()}_variable_{id}");

			switch (variable.StorageClass)
			{
				case ShaderStorageClass.Output:
					{
						if (locations.TryGetValue(id, out uint locationValue))
						{
							methodEmitter.LoadArgument(outputIndex)
											.LoadConstant(outputMappings.Locations[(int)locationValue])
											.LoadConstant(variable.Type.ElementType!.Size)
											.Call(Slice.Value);
						}
						else if (builtins.TryGetValue(id, out Builtin builtinValue))
						{
							methodEmitter.LoadArgument(outputIndex)
											.LoadConstant(outputMappings.Builtins[builtinValue])
											.LoadConstant(variable.Type.ElementType!.Size)
											.Call(Slice.Value);
						}
						else
						{
							throw new InvalidOperationException($"Variable {id} has no location or builtin decoration.");
						}

						break;
					}
				case ShaderStorageClass.Input:
					{
						if (locations.TryGetValue(id, out uint locationValue))
						{
							methodEmitter.LoadArgument(inputIndex)
											.LoadConstant(inputMappings.Locations[(int)locationValue])
											.LoadConstant(variable.Type.ElementType!.Size)
											.Call(Slice.Value);
						}
						else if (builtins.TryGetValue(id, out Builtin builtinValue))
						{
							methodEmitter.LoadArgument(inputIndex)
											.LoadConstant(inputMappings.Builtins[builtinValue])
											.LoadConstant(variable.Type.ElementType!.Size)
											.Call(Slice.Value);
						}
						else
						{
							throw new InvalidOperationException($"Variable {id} has no location or builtin decoration.");
						}

						break;
					}
				case ShaderStorageClass.Uniform:
					{
						if (!bindings.TryGetValue(id, out var bindingValue))
						{
							throw new InvalidOperationException($"Variable {id} has no binding decoration.");
						}

						methodEmitter.LoadArgumentAddress(bufferAttachmentsIndex)
										.LoadConstant((int)bindingValue)
										.Call(SpanCollection__GetItem.Value);

						break;
					}
				case ShaderStorageClass.Image:
					{
						if (!bindings.TryGetValue(id, out var bindingValue))
						{
							throw new InvalidOperationException($"Variable {id} has no binding decoration.");
						}

						var dimensionLocal = methodEmitter.DeclareLocal(typeof(JitterMethods.Vector_3<int>), $"image_dimensions_{id}");

						methodEmitter.LoadArgumentAddress(imageDimensionsIndex)
										.LoadConstant((int)bindingValue)
										.Call(SpanCollection__GetItem.Value)
										.Call(ReadVec<int>(3))
										.StoreLocal(dimensionLocal);

						imageDimensions[id] = dimensionLocal;

						methodEmitter.LoadArgumentAddress(imageAttachmentsIndex)
										.LoadConstant((int)bindingValue)
										.Call(SpanCollection__GetItem.Value);

						break;
					}
				case ShaderStorageClass.Function:
					{
						int size = variable.Type.ElementType!.Size;

						methodEmitter.LoadConstant((uint)size)
										.LocalAllocate()
										.LoadConstant(size)
										.NewObject(Span_Byte__Ctor_VoidPtr_Int32.Value);

						break;
					}
				default:
					throw new NotImplementedException($"Variables of storage class {variable.StorageClass} are not implemented");
			}

			methodEmitter.StoreLocal(local);

			SetValue(id, variable.Type, emitter => emitter.LoadLocal(local));
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
						else if(storeType.IsStruct())
						{
							writeMethod = Copy.Value;
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

						if(ReadByType(loadType, out var readMethod))
						{
							SetValue(resultId, loadType, emitter => EmitValue(emitter, pointerId)
														.Call(readMethod));
						}
						else
						{
							SetValue(resultId, loadType, emitter => EmitValue(emitter, pointerId));
						}

						break;
					}
				case ShaderOp.IntToFloat:
					{
						reader.IntToFloat(out int resultId, out int typeId, out int valueId);
						var opType = types[typeId];
						if (opType != ShaderType.Float(4))
						{
							throw new InvalidOperationException($"IntToFloat result must be float");
						}

						Field? constantValue = null;

						if (constants.TryGetValue(valueId, out var constant))
						{
							constantValue = (float)constant.Value.Int32;
						}

						SetValue(resultId, opType, emitter => EmitValue(emitter, valueId).Convert<float>(), constantValue);
						break;
					}
				case ShaderOp.FloatToInt:
					{
						reader.FloatToInt(out int resultId, out int typeId, out int valueId);
						var opType = types[typeId];
						if (opType != ShaderType.Int(4))
						{
							throw new InvalidOperationException($"FloatToInt result must be int");
						}

						Field? constantValue = null;

						if (constants.TryGetValue(valueId, out var constant))
						{
							constantValue = (int)constant.Value.Float;
						}

						SetValue(resultId, opType, emitter => EmitValue(emitter, valueId).Convert<int>(), constantValue);
						break;
					}
				case ShaderOp.Add:
					{
						reader.Add(out int resultId, out int typeId, out int leftId, out int rightId);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, leftId, rightId).Add());

						break;
					}
				case ShaderOp.Subtract:
					{
						reader.Subtract(out int resultId, out int typeId, out int leftId, out int rightId);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, leftId, rightId).Subtract());

						break;
					}
				case ShaderOp.Multiply:
					{
						reader.Multiply(out int resultId, out int typeId, out int leftId, out int rightId);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, leftId, rightId).Multiply());

						break;
					}
				case ShaderOp.Divide:
					{
						reader.Divide(out int resultId, out int typeId, out int leftId, out int rightId);

						var opType = types[typeId];

						SetValue(resultId, opType, emitter => EmitValues(emitter, leftId, rightId).Divide());

						break;
					}
				case ShaderOp.Negate:
					{
						reader.Negate(out int resultId, out int typeId, out int valueId);

						var opType = types[typeId];

						if (opType != types[valueId])
						{
							throw new InvalidOperationException($"Negate operand must have the same type as result");
						}

						SetValue(resultId, opType, emitter => EmitValue(emitter, valueId).Negate());

						break;
					}
				case ShaderOp.Equals:
					{
						reader.Equals(out int resultId, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						SetValue(resultId, ShaderType.Bool, emitter => EmitValues(emitter, a, b).CompareEqual());

						break;
					}
				case ShaderOp.GreaterThan:
					{
						reader.GreaterThan(out int resultId, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						SetValue(resultId, ShaderType.Bool, emitter => EmitValues(emitter, a, b).CompareGreaterThan());

						break;
					}
				case ShaderOp.LessThan:
					{
						reader.LessThan(out int resultId, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						SetValue(resultId, ShaderType.Bool, emitter => EmitValues(emitter, a, b).CompareLessThan());

						break;
					}
				case ShaderOp.Conditional:
					{
						reader.Conditional(out int resultId, out int type, out int condition, out int trueValue, out int falseValue);

						if (types[condition] != ShaderType.Bool)
						{
							throw new InvalidOperationException("Conditional condition must be bool");
						}

						if (types[trueValue] != types[falseValue])
						{
							throw new InvalidOperationException("Conditional true and false values must have the same type");
						}

						SetValue(resultId, types[trueValue], emitter =>
						{
							emitter = EmitValue(emitter, condition)
										.DefineLabel(out var trueLabel)
										.DefineLabel(out var endLabel)
										.BranchIfTrue(trueLabel);

							emitter = EmitValues(emitter, falseValue)
										.Branch(endLabel)
										.MarkLabel(trueLabel);

							emitter = EmitValues(emitter, trueValue)
										.MarkLabel(endLabel);

							return emitter;
						});

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

						var baseType = types[baseId].ElementType!;

						var resultType = types[typeId];

						if (baseType.IsRuntimeArray() || baseType.IsVector())
						{
							SetValue(resultId, resultType, emitter => EmitValues(emitter, [baseId, ids[0]])
																			.LoadConstant(resultType.ElementType!.Size)
																			.Multiply()
																			.LoadConstant(resultType.ElementType!.Size)
																			.Call(Slice.Value));
						}
						else if (baseType.IsStruct())
						{
							if (!constantValues.TryGetValue(ids[0], out Field indexConstant))
							{
								throw new InvalidOperationException($"Access chain index {ids[0]} must be a constant");
							}

							int offset = baseType.Members!.Take(indexConstant.Int32).Sum(x => x.Size);

							SetValue(resultId, resultType, emitter => EmitValue(emitter, baseId)
																			.LoadConstant(offset)
																			.LoadConstant(resultType.ElementType!.Size)
																			.Call(Slice.Value));

						}
						else
						{
							throw new NotImplementedException($"Unimplemented access chain base type {baseType}.");
						}

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

						if (opType.ElementType == ShaderType.Float(4))
						{
							SetValue(resultId, opType, emitter => EmitValues(emitter, ids).Call(Vec<float>(count)));
						}
						else if (opType.ElementType == ShaderType.Int(4))
						{
							SetValue(resultId, opType, emitter => EmitValues(emitter, ids).Call(Vec<int>(count)));
						}
						else
						{
							throw new NotImplementedException($"Unimplemented composite construct type {opType}.");
						}

						break;
					}
				case ShaderOp.VectorShuffle:
					{
						reader.VectorShuffle(out int count);

						var components = new int[count];

						reader.VectorShuffle(out int resultId, out int typeId, out int vector1Id, out int vector2Id, components.AsSpan(), out _);

						var opType = types[typeId];
						var vector1Type = types[vector1Id];
						var vector2Type = types[vector2Id];

						var elementType = opType.IsVector()
												? opType.ElementType!
												: opType;

						SetValue(resultId, opType, emitter =>
						{
							for (int index = 0; index < count; index++)
							{
								int component = components[index];
								int vectorId = vector1Id;
								int vectorSize = vector1Type.ElementCount;
								if (component >= vector1Type.ElementCount)
								{
									component -= vector1Type.ElementCount;
									vectorId = vector2Id;
									vectorSize = vector2Type.ElementCount;
								}

								emitter = EmitValue(emitter, vectorId)
											.LoadConstant(component)
											.Call(CompVec(GetType(elementType), vectorSize));
							}

							if (count > 1)
							{
								emitter = emitter.Call(Vec(GetType(elementType), count));
							}

							return emitter;
						});

						break;
					}
				case ShaderOp.Gather:
					{
						reader.Gather(out int resultId, out int typeId, out int imageId, out int coordinateId);

						var imageType = types[imageId].ElementType!;

						var pixelType = imageType.ElementType!;
						int pixelSize = pixelType.Size;

						var gatherMethod = typeof(JitterMethods).GetMethod(nameof(JitterMethods.Gather_Int_2))!;

						SetValue(resultId, pixelType, emitter => EmitValues(emitter, imageId, coordinateId)
																	.LoadLocal(imageDimensions[imageId])
																	.LoadConstant(pixelSize)
																	.Call(gatherMethod)
																	.Call(ReadByType(pixelType)));

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

	public override void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, Span<byte> input, Span<byte> output)
	{
		var bufferCollection = new SpanCollection();

		for (int index = 0; index < bufferAttachments.Length; index++)
		{
			bufferCollection.Add(bufferAttachments[index].Span);
		}

		var imageCollection = new SpanCollection();
		var imageDimensionCollection = new SpanCollection();

		Span<byte> dimensionSpan = new byte[12 * imageAttachments.Length];

		for (int index = 0; index < imageAttachments.Length; index++)
		{
			var attachment = imageAttachments[index];

			imageCollection.Add(attachment.Data.Span);

			var imageDimensionSpan = dimensionSpan[(index * 12)..][..12];

			JitterMethods.Write(imageDimensionSpan, new JitterMethods.Vector_3<int>(attachment.Size.Width, attachment.Size.Height, attachment.Size.Depth));

			imageDimensionCollection.Add(imageDimensionSpan);
		}

		this.execute(input, output, bufferCollection, imageCollection, imageDimensionCollection);
	}

	public static ShaderJitter Create(byte[] shaderData)
	{
		var (inputMappings, outputMappings) = ShaderAnalyser.GetMappings(shaderData);

		return new(shaderData, inputMappings, outputMappings);
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
				case ShaderOp.TypeStruct:
					{
						reader.TypeStruct(out int id, out int memberCount);

						var memberTypeIds = new int[memberCount];

						reader = reader.TypeStruct(out _, memberTypeIds, out _);

						var typeLookup = types;

						types[id] = ShaderType.StructOf([.. memberTypeIds.Select(x => typeLookup[x])]);

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