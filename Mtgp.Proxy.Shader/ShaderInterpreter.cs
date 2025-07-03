using Mtgp.Shader;
using System;
using System.Runtime.InteropServices;

namespace Mtgp.Proxy.Shader;

[StructLayout(LayoutKind.Explicit)]
public readonly struct Field
{
	[FieldOffset(0)]
	public readonly int Int32;
	[FieldOffset(0)]
	public readonly bool Bool;
	[FieldOffset(0)]
	public readonly float Float;

	public Field(int value) => this.Int32 = value;
	public Field(bool value) => this.Bool = value;
	public Field(float value) => this.Float = value;

	public static implicit operator Field(int value) => new(value);
	public static implicit operator Field(bool value) => new(value);
	public static implicit operator Field(float value) => new(value);

	public static explicit operator int(Field value) => value.Int32;
	public static explicit operator bool(Field value) => value.Bool;
	public static explicit operator float(Field value) => value.Float;
}

public record ShaderAttribute(ShaderType Type, int Location, int Offset);

public class ShaderInterpreter
		: ShaderExecutor
{
	private readonly Memory<byte> compiledShader;
	public override ShaderIoMappings InputMappings { get; }
	public override ShaderIoMappings OutputMappings { get; }

	public static ShaderInterpreter Create(Memory<byte> compiledShader)
	{
		var (inputMappings, outputMappings) = ShaderAnalyser.GetMappings(compiledShader);

		return new(compiledShader, inputMappings, outputMappings);
	}

	public ShaderInterpreter(Memory<byte> compiledShader, ShaderIoMappings inputMappings, ShaderIoMappings outputMappings)
	{
		this.compiledShader = compiledShader;
		this.InputMappings = inputMappings;
		this.OutputMappings = outputMappings;
	}

	private class VariableInfo
	{
		public uint? Location { get; set; }
		public uint? Binding { get; set; }
		public Builtin? Builtin { get; set; }
	}

	public struct Builtins(int vertexIndex = 0, int instanceIndex = 0, int positionX = 0, int positionY = 0, int timer = 0, int workgroupId = 0)
	{
		public int VertexIndex = vertexIndex;
		public int InstanceIndex = instanceIndex;
		public int PositionX = positionX;
		public int PositionY = positionY;
		public int Timer = timer;
		public int WorkgroupId = workgroupId;
	};

	public readonly struct UniformPointer(byte binding, uint pointer)
	{
		private UniformPointer(uint value)
			: this(0, 0)
		{
			this.value = value;
		}

		private readonly uint value = (pointer & 0x00FFFFFF) + ((uint)binding << 24);

		public byte Binding
		{
			get => (byte)(value >> 24);
		}

		public uint Pointer
		{
			get => value & 0x00FFFFFF;
		}

		public uint ToUInt32() => value;

		public static UniformPointer FromUInt32(uint value) => new(value);
	}

	public override void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, Span<byte> input, Span<byte> output)
	{
		bool isRunning = true;

		var shaderReader = new ShaderReader(compiledShader.Span);

		var variableDecorations = new Dictionary<int, VariableInfo>();

		void SetVariableInfo(int target, Action<VariableInfo> set)
		{
			if (!variableDecorations.TryGetValue(target, out var decorations))
			{
				decorations = new();
				variableDecorations[target] = decorations;
			}

			set(decorations);
		}

		var results = new Dictionary<int, int>();
		var types = new Dictionary<int, ShaderType>();

		int workingSetSize = 4096;
		Span<byte> workingSet = stackalloc byte[workingSetSize];

		int workingSetPointer = 0;

		int GetWorkingSetPointer(int size)
		{
			int pointer = workingSetPointer;
			workingSetPointer += size;

			if (workingSetPointer > workingSetSize)
			{
				throw new InvalidOperationException("Working set overflow");
			}

			return pointer;
		}

		Span<byte> GetSpan(int id, Span<byte> workingSet, bool deref = false)
		{
			int valuePointer = results[id];
			int valueSize = deref ? types[id].ElementType!.Size : types[id].Size;

			return workingSet[valuePointer..][..valueSize];
		}

		Span<byte> GetTarget(int id, int type, Span<byte> workingSet)
		{
			int pointer = GetWorkingSetPointer(types[type].Size);
			results[id] = pointer;

			return workingSet[pointer..][..types[type].Size];
		}

		while (isRunning)
		{
			var op = shaderReader.Next;
			int result;
			int target;

			switch (op)
			{
				case ShaderOp.Decorate:
					shaderReader.Decorate(out _, out var decoration);

					switch (decoration)
					{
						case ShaderDecoration.Location:
							shaderReader = shaderReader.DecorateLocation(out target, out uint location);

							SetVariableInfo(target, x => x.Location = location);
							break;
						case ShaderDecoration.Binding:
							shaderReader = shaderReader.DecorateBinding(out target, out uint binding);

							SetVariableInfo(target, x => x.Binding = binding);
							break;
						case ShaderDecoration.Builtin:
							shaderReader = shaderReader.DecorateBuiltin(out target, out var builtin);

							SetVariableInfo(target, x => x.Builtin = builtin);
							break;
						default:
							throw new InvalidOperationException($"Unknown decoration {decoration}");
					}
					break;
				case ShaderOp.TypeInt:
					{
						shaderReader = shaderReader.TypeInt(out result, out int width);
						types[result] = ShaderType.Int(width);
					}
					break;
				case ShaderOp.TypeFloat:
					{
						shaderReader = shaderReader.TypeFloat(out result, out int width);
						types[result] = ShaderType.Float(width);
					}
					break;
				case ShaderOp.TypeBool:
					shaderReader = shaderReader.TypeBool(out result);
					types[result] = ShaderType.Bool;
					break;
				case ShaderOp.TypePointer:
					{
						shaderReader = shaderReader.TypePointer(out result, out var storageClass, out int pointerType);
						types[result] = ShaderType.PointerOf(types[pointerType], storageClass);
						break;
					}
				case ShaderOp.TypeVector:
					{
						shaderReader = shaderReader.TypeVector(out result, out int componentType, out int componentCount);

						types[result] = ShaderType.VectorOf(types[componentType], componentCount);
						break;
					}
				case ShaderOp.TypeStruct:
					{
						shaderReader.TypeStruct(out result, out int memberCount);
						Span<int> members = new int[memberCount];

						shaderReader = shaderReader.TypeStruct(out result, members, out _);
						var memberTypes = new ShaderType[memberCount];
						for (int i = 0; i < memberCount; i++)
						{
							memberTypes[i] = types[members[i]];
						}
						types[result] = ShaderType.StructOf(memberTypes);
						break;
					}
				case ShaderOp.TypeRuntimeArray:
					{
						shaderReader = shaderReader.TypeRuntimeArray(out result, out int elementType);

						types[result] = ShaderType.RuntimeArrayOf(types[elementType]);
						break;
					}
				case ShaderOp.TypeImage:
					{
						shaderReader = shaderReader.TypeImage(out result, out int imageType, out int dim);

						types[result] = ShaderType.ImageOf(types[imageType], dim);
						break;
					}
				case ShaderOp.Variable:
					{
						shaderReader = shaderReader.Variable(out result, out var variableStorageClass, out int type);

						var variableType = types[type];

						if (!variableType.IsPointer())
						{
							throw new InvalidOperationException("Variable type must be a pointer");
						}

						if (variableType.StorageClass != variableStorageClass)
						{
							throw new InvalidOperationException("Variable storage class must match type storage class");
						}

						types[result] = variableType;

						int pointer = 0;

						switch (variableType.StorageClass)
						{
							case ShaderStorageClass.Input:
								if (variableDecorations[result].Location is not null)
								{
									pointer = this.InputMappings.Locations[(int)variableDecorations[result].Location!.Value];
								}
								else if (variableDecorations[result].Builtin is not null)
								{
									pointer = this.InputMappings.Builtins[variableDecorations[result].Builtin!.Value];
								}
								else
								{
									throw new InvalidOperationException("Input variable has no target decoration");
								}
								break;
							case ShaderStorageClass.Output:
								if (variableDecorations[result].Location is not null)
								{
									pointer = this.OutputMappings.Locations[(int)variableDecorations[result].Location!.Value];
								}
								else if (variableDecorations[result].Builtin is not null)
								{
									pointer = this.OutputMappings.Builtins[variableDecorations[result].Builtin!.Value];
								}
								else
								{
									throw new InvalidOperationException("Output variable has no target decoration");
								}
								break;
							case ShaderStorageClass.UniformConstant:
							case ShaderStorageClass.Uniform:
								if (variableDecorations[result].Binding is not null)
								{
									var uniformPointer = new UniformPointer((byte)variableDecorations[result].Binding!, 0);

									pointer = (int)uniformPointer.ToUInt32();
								}
								else
								{
									throw new InvalidOperationException("Uniform variable has no binding decoration");
								}
								break;
							case ShaderStorageClass.Function:
								pointer = GetWorkingSetPointer(variableType.ElementType!.Size);
								break;
							case ShaderStorageClass.Image:
								if (variableDecorations[result].Binding is not null)
								{
									pointer = (int)variableDecorations[result].Binding!;
								}
								else
								{
									throw new InvalidOperationException("Image variable has no target decoration");
								}
								break;
							default:
								throw new InvalidOperationException($"Invalid storage class {variableStorageClass}");
						}

						var targetSpan = GetTarget(result, type, workingSet);

						new BitWriter(targetSpan).Write(pointer);
					}
					break;
				case ShaderOp.Constant:
					{
						shaderReader = shaderReader.Constant(out result, out int type, out int value);

						var constantType = types[type];

						if (constantType.IsInt() || constantType.IsFloat())
						{
							new BitWriter(GetTarget(result, type, workingSet)).Write(value);
						}
						else
						{
							throw new InvalidOperationException($"Unsupported constant type {constantType}");
						}

						types[result] = constantType;
						break;
					}
				case ShaderOp.Load:
					{
						shaderReader = shaderReader.Load(out result, out int resultType, out int pointer);

						var type = types[resultType];
						var pointerType = types[pointer];

						if (pointerType.ElementType != type)
						{
							throw new InvalidOperationException("Load result type must match variable element type");
						}

						var valueType = pointerType.ElementType;

						var targetSpan = GetTarget(result, resultType, workingSet);

						switch (pointerType.StorageClass)
						{
							case ShaderStorageClass.Input:
								{
									int pointerValue = BitConverter.ToInt32(GetSpan(pointer, workingSet));

									var inputSpan = input[pointerValue..][..type.Size];

									inputSpan.CopyTo(targetSpan);
									break;
								}
							case ShaderStorageClass.Uniform:
								{
									int pointerValue = BitConverter.ToInt32(GetSpan(pointer, workingSet));
									var uniformPointer = UniformPointer.FromUInt32((uint)pointerValue);

									var uniformSpan = bufferAttachments[uniformPointer.Binding].Span[(int)uniformPointer.Pointer..][..valueType.Size];

									uniformSpan.CopyTo(targetSpan);
									break;
								}
							case ShaderStorageClass.Function:
								{
									int pointerValue = BitConverter.ToInt32(GetSpan(pointer, workingSet));

									var inputSpan = workingSet[pointerValue..][..valueType.Size!];

									inputSpan.CopyTo(targetSpan);
									break;
								}
							default:
								throw new InvalidOperationException($"Invalid storage class {pointerType.StorageClass}");
						}

						types[result] = type;

						break;
					}
				case ShaderOp.Store:
					{
						shaderReader = shaderReader.Store(out int pointer, out int value);

						var pointerType = types[pointer];
						var valueType = pointerType.ElementType;

						if (valueType != types[value])
						{
							throw new InvalidOperationException("Store value type must match pointer element type");
						}

						var valueToStore = GetSpan(value, workingSet);
						int pointerValue = BitConverter.ToInt32(GetSpan(pointer, workingSet));

						switch (pointerType.StorageClass)
						{
							case ShaderStorageClass.Output:
								var outputSpan = output[pointerValue..][..valueType.Size];

								valueToStore.CopyTo(outputSpan);
								break;
							case ShaderStorageClass.Uniform:
								var uniformPointer = UniformPointer.FromUInt32((uint)pointerValue);

								var uniformSpan = bufferAttachments[uniformPointer.Binding].Span[(int)uniformPointer.Pointer..][..valueType.Size];

								valueToStore.CopyTo(uniformSpan);
								break;
							case ShaderStorageClass.Function:
								var targetSpan = workingSet[pointerValue..][..valueType.Size!];

								valueToStore.CopyTo(targetSpan);
								break;
							default:
								throw new InvalidOperationException($"Invalid storage class {pointerType.StorageClass}");
						}
						break;
					}
				case ShaderOp.Add:
					{
						shaderReader = shaderReader.Add(out result, out int type, out int a, out int b);

						if (types[type] != types[a] || types[type] != types[b])
						{
							throw new InvalidOperationException("Add operands must have the same type");
						}

						ApplyOperator(types[type], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a + b, (a, b) => a + b);
						types[result] = types[type];
						break;
					}
				case ShaderOp.Mod:
					{
						shaderReader = shaderReader.Mod(out result, out int type, out int a, out int b);

						if (types[type] != types[a] || types[type] != types[b])
						{
							throw new InvalidOperationException("Mod operands must have the same type");
						}

						ApplyOperator(types[type], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a % b, (a, b) => a % b);

						types[result] = types[type];
						break;
					}
				case ShaderOp.Gather:
					{
						shaderReader = shaderReader.Gather(out result, out int type, out int texture, out int coord);

						int binding = BitConverter.ToInt32(GetSpan(texture, workingSet));

						var textureImage = imageAttachments[binding];
						var textureData = textureImage.Data.Span;

						new BitReader(GetSpan(coord, workingSet))
							.Read(out int x)
							.Read(out int y);

						int textureIndex = x + y * textureImage.Size.Width;
						int valueSize = types[type].Size;

						textureData[(textureIndex * valueSize)..][..valueSize].CopyTo(GetTarget(result, type, workingSet));
						types[result] = types[type];
						break;
					}
				case ShaderOp.Subtract:
					{
						shaderReader = shaderReader.Subtract(out result, out int type, out int a, out int b);

						if (types[type] != types[a] || types[type] != types[b])
						{
							throw new InvalidOperationException("Subtract operands must have the same type");
						}

						ApplyOperator(types[type], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a - b, (a, b) => a - b);

						types[result] = types[type];
						break;
					}
				case ShaderOp.Multiply:
					{
						shaderReader = shaderReader.Multiply(out result, out int type, out int a, out int b);

						if (types[type] != types[a] || types[type] != types[b])
						{
							throw new InvalidOperationException("Multiply operands must have the same type");
						}

						ApplyOperator(types[type], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a * b, (a, b) => a * b);

						types[result] = types[type];
						break;
					}
				case ShaderOp.Divide:
					{
						shaderReader = shaderReader.Divide(out result, out int type, out int a, out int b);

						if (types[type] != types[a] || types[type] != types[b])
						{
							throw new InvalidOperationException("Divide operands must have the same type");
						}

						ApplyOperator(types[type], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a / b, (a, b) => a / b);

						types[result] = types[type];
						break;
					}
				case ShaderOp.Equals:
					{
						shaderReader = shaderReader.Equals(out result, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						ApplyOperator(types[a], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a - b, (a, b) => a - b);

						types[result] = types[type];
						break;
					}
				case ShaderOp.GreaterThan:
					{
						shaderReader = shaderReader.GreaterThan(out result, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						ApplyOperator(types[a], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a > b ? 0 : 1, (a, b) => a > b ? 0 : 1);

						types[result] = types[type];
						break;
					}
				case ShaderOp.LessThan:
					{
						shaderReader = shaderReader.LessThan(out result, out int type, out int a, out int b);

						if (types[a] != types[b])
						{
							throw new InvalidOperationException($"Equals operands must have the same type");
						}

						if (types[type] != ShaderType.Bool)
						{
							throw new InvalidOperationException($"Equals result must be bool");
						}

						ApplyOperator(types[a], GetSpan(a, workingSet), GetSpan(b, workingSet), GetTarget(result, type, workingSet), (a, b) => a < b ? 0 : 1, (a, b) => a < b ? 0 : 1);

						types[result] = types[type];
						break;
					}
				case ShaderOp.Conditional:
					{
						shaderReader = shaderReader.Conditional(out result, out int type, out int condition, out int trueValue, out int falseValue);

						if (types[condition] != ShaderType.Bool)
						{
							throw new InvalidOperationException("Conditional condition must be bool");
						}

						if (types[trueValue] != types[falseValue])
						{
							throw new InvalidOperationException("Conditional true and false values must have the same type");
						}

						new BitReader(GetSpan(condition, workingSet)).Read(out int conditionValue);

						bool isTrue = conditionValue == 0;

						int valueId = isTrue ? trueValue : falseValue;

						GetSpan(valueId, workingSet).CopyTo(GetTarget(result, type, workingSet));

						types[result] = types[type];
						break;
					}
				case ShaderOp.CompositeConstruct:
					{
						shaderReader.CompositeConstruct(out int count);

						Span<int> components = new int[count];

						shaderReader = shaderReader.CompositeConstruct(out result, out int type, components, out _);

						if (!types[type].IsVector())
						{
							throw new InvalidOperationException("Composite construct result type must be a vector");
						}

						if (count != types[type].ElementCount)
						{
							throw new InvalidOperationException("Composite construct component count must match element count");
						}

						var elementType = types[type].ElementType;

						var targetSpan = GetTarget(result, type, workingSet);

						for (int i = 0; i < count; i++)
						{
							if (types[components[i]] == elementType)
							{
								var subSpan = targetSpan[(i * elementType.Size)..][..elementType.Size];

								GetSpan(components[i], workingSet).CopyTo(subSpan);
							}
							else
							{
								throw new Exception("Composite construct component type must match element type");
							}
						}

						types[result] = types[type];
						break;
					}
				case ShaderOp.IntToFloat:
					{
						shaderReader = shaderReader.IntToFloat(out result, out int type, out int value);

						if (!types[type].IsFloat())
						{
							throw new InvalidOperationException("Int to float result type must be float");
						}

						if (!types[value].IsInt())
						{
							throw new InvalidOperationException("Int to float value type must be int");
						}

						var targetSpan = GetTarget(result, type, workingSet);
						new BitWriter(targetSpan).Write((float)BitConverter.ToInt32(GetSpan(value, workingSet)));
						types[result] = types[type];
						break;
					}
				case ShaderOp.FloatToInt:
					{
						shaderReader = shaderReader.FloatToInt(out result, out int type, out int value);

						if (!types[type].IsInt())
						{
							throw new InvalidOperationException("Float to int result type must be int");
						}

						if (!types[value].IsFloat())
						{
							throw new InvalidOperationException("Float to int value type must be float");
						}

						var targetSpan = GetTarget(result, type, workingSet);
						new BitWriter(targetSpan).Write((int)BitConverter.ToSingle(GetSpan(value, workingSet)));
						types[result] = types[type];
						break;
					}
				case ShaderOp.Abs:
					{
						shaderReader = shaderReader.Abs(out result, out int type, out int value);

						ApplyOperator(types[type], GetSpan(value, workingSet), GetTarget(result, type, workingSet), Math.Abs, Math.Abs);
						types[result] = types[type];
						break;
					}
				case ShaderOp.Negate:
					{
						shaderReader = shaderReader.Negate(out result, out int type, out int value);

						ApplyOperator(types[type], GetSpan(value, workingSet), GetTarget(result, type, workingSet), x => -x, x => -x);
						types[result] = types[type];
						break;
					}
				case ShaderOp.Return:
					isRunning = false;
					break;
				case ShaderOp.EntryPoint:
					shaderReader = shaderReader.Skip();
					break;
				case ShaderOp.AccessChain:
					{
						shaderReader.AccessChain(out int count);

						Span<int> indices = new int[count];

						if (count > 1)
						{
							throw new NotImplementedException("Access chain with more than one index is not implemented");
						}

						shaderReader = shaderReader.AccessChain(out result, out int type, out int basePointer, indices, out _);

						var basePointerType = types[basePointer];

						if (!basePointerType.IsPointer())
						{
							throw new InvalidOperationException("Access chain base must be a pointer");
						}

						if (basePointerType.StorageClass is ShaderStorageClass.Image)
						{
							throw new InvalidOperationException($"Invalid storage class {basePointerType.StorageClass} for access chain base pointer");
						}

						int offsetIntoBase;

						int index = BitConverter.ToInt32(GetSpan(indices[0], workingSet));

						var subjectType = basePointerType.ElementType!;

						if (subjectType.IsStruct())
						{
							if (index < 0 || index >= subjectType.Members!.Length)
							{
								throw new InvalidOperationException($"Access chain index {index} is out of bounds for struct type {subjectType}");
							}
							var memberType = subjectType.Members[index];
							if (types[type] != ShaderType.PointerOf(memberType, basePointerType.StorageClass.Value))
							{
								throw new InvalidOperationException("Access chain result type must be a pointer to the member type of the base pointer");
							}
							offsetIntoBase = subjectType.Members.Take(index).Sum(m => m.Size);
						}
						else if (subjectType.IsRuntimeArray() || subjectType.IsVector())
						{
							var elementType = subjectType.ElementType!;

							if (types[type] != ShaderType.PointerOf(elementType, basePointerType.StorageClass.Value))
							{
								throw new InvalidOperationException("Access chain result type must be a pointer to the element type of the base pointer");
							}

							offsetIntoBase = index * elementType.Size;
						}
						else
						{
							throw new InvalidOperationException($"Access chain base pointer type {subjectType} is not supported for access chain");
						}

						int basePointerValue = BitConverter.ToInt32(GetSpan(basePointer, workingSet));

						var targetSpan = GetTarget(result, type, workingSet);

						if (basePointerType.StorageClass is ShaderStorageClass.UniformConstant || basePointerType.StorageClass is ShaderStorageClass.Uniform)
						{
							var uniformPointer = UniformPointer.FromUInt32((uint)basePointerValue);

							new BitWriter(targetSpan).Write((int)new UniformPointer(uniformPointer.Binding, (uint)(uniformPointer.Pointer + offsetIntoBase)).ToUInt32());
						}
						else
						{
							int elementPointer = basePointerValue + offsetIntoBase;

							new BitWriter(targetSpan).Write(elementPointer);
						}

						types[result] = types[type];
					}
					break;
				case ShaderOp.VectorShuffle:
					{
						shaderReader.VectorShuffle(out int count);

						Span<int> components = new int[count];

						if (count > 4)
						{
							throw new NotImplementedException("Vector shuffle with more than 4 components is not implemented");
						}

						shaderReader = shaderReader.VectorShuffle(out result, out int type, out int vector1, out int vector2, components, out _);

						var resultType = types[type];

						int elementCount = resultType.IsVector() ? resultType.ElementCount : 1;

						if (count != elementCount)
						{
							throw new InvalidOperationException("Vector shuffle component count must match element count");
						}

						if (!types[vector1].IsVector() || !types[vector2].IsVector())
						{
							throw new InvalidOperationException("Vector shuffle operands must be vectors");
						}

						if (types[vector1].ElementType != types[vector2].ElementType)
						{
							throw new InvalidOperationException("Vector shuffle operands must have the same element type");
						}

						var targetSpan = GetTarget(result, type, workingSet);

						var vector1Span = GetSpan(vector1, workingSet);
						var vector2Span = GetSpan(vector2, workingSet);

						for (int index = 0; index < count; index++)
						{
							int component = components[index];

							var vectorSpan = vector1Span;

							if (component >= types[vector1].ElementCount)
							{
								component -= types[vector1].ElementCount;
								vectorSpan = vector2Span;
							}

							vectorSpan[(component * resultType.Size)..][..resultType.Size].CopyTo(targetSpan[(index * resultType.Size)..][..resultType.Size]);
						}

						types[result] = types[type];
						break;
					}
				default:
					throw new InvalidOperationException($"Unknown opcode {op}");
			}
		}
	}

	private static void ApplyOperator(ShaderType type, Span<byte> value, Span<byte> target, Func<float, float> floatOp, Func<int, int> intOp)
	{
		if (type.IsFloat())
		{
			new BitWriter(target).Write(floatOp(BitConverter.ToSingle(value)));
		}
		else if (type.IsInt())
		{
			new BitWriter(target).Write(intOp(BitConverter.ToInt32(value)));
		}
		else
		{
			throw new InvalidOperationException($"Unsupported type {type}");
		}
	}

	private static void ApplyOperator(ShaderType type, Span<byte> a, Span<byte> b, Span<byte> target, Func<float, float, float> floatOp, Func<int, int, int> intOp)
	{
		if (type.IsFloat())
		{
			new BitWriter(target).Write(floatOp(BitConverter.ToSingle(a), BitConverter.ToSingle(b)));
		}
		else if (type.IsInt())
		{
			new BitWriter(target).Write(intOp(BitConverter.ToInt32(a), BitConverter.ToInt32(b)));
		}
		else
		{
			throw new InvalidOperationException($"Unsupported type {type}");
		}
	}
}
