using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mtgp.Shader
{
	public class ShaderInterpreter
	{
		private readonly Memory<byte> compiledShader;
		private readonly int[] inputMappings;
		private readonly int[] outputMappings;

		[StructLayout(LayoutKind.Explicit)]
		private struct Field
		{
			[FieldOffset(0)]
			public int Int32;
			[FieldOffset(0)]
			public bool Bool;
			[FieldOffset(0)]
			public float Float;

			public static implicit operator Field(int value) => new() { Int32 = value };
			public static implicit operator Field(bool value) => new() { Bool = value };
			public static implicit operator Field(float value) => new() { Float = value };

			public static explicit operator int(Field value) => value.Int32;
			public static explicit operator bool(Field value) => value.Bool;
			public static explicit operator float(Field value) => value.Float;
		}

		public ShaderInterpreter(Memory<byte> compiledShader)
		{
			this.compiledShader = compiledShader;

			var (inputs, outputs) = GetAttributes(compiledShader);

			this.inputMappings = inputs.Select(x => x.Type.Size).RunningOffset().ToArray();
			this.outputMappings = outputs.Select(x => x.Type.Size).RunningOffset().ToArray();
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
			var variableTypes = new Dictionary<int, int>();

			var types = new Dictionary<int, ShaderType>();

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
					case ShaderOp.TypeInt:
						{
							shaderReader.TypeInt(out int result, out int width);
							types[result] = ShaderType.Int(width);
						}
						break;
					case ShaderOp.TypeBool:
						{
							shaderReader.TypeBool(out int result);
							types[result] = ShaderType.Bool;
						}
						break;
					case ShaderOp.TypeImage:
						{
							shaderReader.TypeImage(out int result, out int imageType, out int dim);
							var type = types[imageType];
							types[result] = ShaderType.ImageOf(type, dim);
						}
						break;
					case ShaderOp.TypeVector:
						{
							shaderReader.TypeVector(out int result, out int componentType, out int componentCount);
							var type = types[componentType];
							types[result] = ShaderType.VectorOf(type, componentCount);
						}
						break;
					case ShaderOp.TypePointer:
						{
							shaderReader.TypePointer(out int result, out var storageClass, out int typeId);
							var type = types[typeId];
							types[result] = ShaderType.PointerOf(type, storageClass);
						}
						break;
					case ShaderOp.Variable:
						{
							shaderReader.Variable(out int result, out var storageClass, out var type);

							if (variables.Contains(result))
							{
								variableTypes[result] = type;
								storageClasses[result] = storageClass;
							}
						}
						break;
				}

				shaderReader = shaderReader.Skip();
			}

			foreach (var variable in variables)
			{
				if (locations.TryGetValue(variable, out var location)
					&& variableTypes.TryGetValue(variable, out var type)
					&& storageClasses.TryGetValue(variable, out var storageClass))
				{
					var attribute = new ShaderAttribute(types[type], (int)location);

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

		private class VariableInfo
		{
			public uint? Location { get; set; }
			public uint? Binding { get; set; }
			public Builtin? Builtin { get; set; }
			public ShaderStorageClass? StorageClass { get; set; }
		}

		public struct Builtins(int vertexIndex = 0, int instanceIndex = 0, int positionX = 0, int positionY = 0, int timer = 0)
		{
			public int VertexIndex = vertexIndex;
			public int InstanceIndex = instanceIndex;
			public int PositionX = positionX;
			public int PositionY = positionY;
			public int Timer = timer;
		}

		public void Execute(ImageState[] imageAttachments, Memory<byte>[] bufferAttachments, Builtins inputBuiltins, ReadOnlySpan<byte> input, ref Builtins outputBuiltins, Span<byte> output)
		{
			bool isRunning = true;

			var shaderReader = new ShaderReader(this.compiledShader.Span);

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

			var results = new Dictionary<int, Field>();
			var results2 = new Dictionary<int, (Field X, Field Y)>();
			var types = new Dictionary<int, ShaderType>();

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
						shaderReader = shaderReader.TypeInt(out result, out int width);
						types[result] = ShaderType.Int(width);
						break;
					case ShaderOp.TypeBool:
						shaderReader = shaderReader.TypeBool(out result);
						types[result] = ShaderType.Bool;
						break;
					case ShaderOp.TypePointer:
						shaderReader = shaderReader.TypePointer(out result, out var storageClass, out int pointerType);
						types[result] = ShaderType.PointerOf(types[pointerType], storageClass);
						break;
					case ShaderOp.TypeVector:
						{
							shaderReader = shaderReader.TypeVector(out result, out int componentType, out int componentCount);

							types[result] = ShaderType.VectorOf(types[componentType], componentCount);
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

							SetVariableInfo(result, x => x.StorageClass = variableStorageClass);
						}
						break;
					case ShaderOp.Constant:
						{
							shaderReader = shaderReader.Constant(out result, out int type, out int value);

							results[result] = value;
							types[result] = types[type];
							break;
						}
					case ShaderOp.Load:
						{
							shaderReader = shaderReader.Load(out result, out int resultType, out int variable);

							var type = types[resultType];

							if (types[variable].ElementType != type)
							{
								throw new InvalidOperationException("Load result type must match variable element type");
							}

							var variableInfo = variableDecorations[variable];

							switch (variableInfo.StorageClass)
							{
								case ShaderStorageClass.Input:
									if (variableInfo.Builtin is not null)
									{
										results[result] = variableInfo.Builtin switch
										{
											Builtin.VertexIndex => inputBuiltins.VertexIndex,
											Builtin.InstanceIndex => inputBuiltins.InstanceIndex,
											Builtin.Timer => inputBuiltins.Timer,
											Builtin.PositionX => inputBuiltins.PositionX,
											Builtin.PositionY => inputBuiltins.PositionY,
											_ => throw new InvalidOperationException($"Invalid builtin {variableInfo.Builtin}"),
										};
									}
									else if (variableInfo.Location is not null)
									{
										if (type.Size == 4)
										{
											results[result] = MemoryMarshal.AsRef<Field>(input[this.inputMappings[(int)variableInfo.Location!]..]);
										}
										else if (type.Size == 8)
										{
											results2[result] = MemoryMarshal.AsRef<(Field, Field)>(input[this.inputMappings[(int)variableInfo.Location!]..]);
										}
										else
										{
											throw new InvalidOperationException("Invalid input size");
										}
									}
									else
									{
										throw new InvalidOperationException("Input variable has no target decoration");
									}
									break;
								default:
									throw new InvalidOperationException($"Invalid storage class {variableInfo.StorageClass}");
							}

							types[result] = type;

							break;
						}
					case ShaderOp.Store:
						{
							shaderReader = shaderReader.Store(out int variable, out int value);

							var variableInfo = variableDecorations[variable];

							int valueToStore = (int)results[value];

							if (types[variable].ElementType != types[value])
							{
								throw new InvalidOperationException("Store value type must match variable element type");
							}

							switch (variableInfo.StorageClass)
							{
								case ShaderStorageClass.Output:
									if (variableInfo.Builtin is not null)
									{
										switch (variableInfo.Builtin)
										{
											case Builtin.PositionX:
												outputBuiltins.PositionX = valueToStore;
												break;
											case Builtin.PositionY:
												outputBuiltins.PositionY = valueToStore;
												break;
											default:
												throw new InvalidOperationException($"Invalid builtin {variableInfo.Builtin}");
										}
									}
									else if (variableInfo.Location is not null)
									{
										BitConverter.TryWriteBytes(output[this.outputMappings[(int)variableInfo.Location]..], valueToStore);
									}
									else
									{
										throw new InvalidOperationException("Output variable has no target decoration");
									}
									break;
								default:
									throw new InvalidOperationException($"Invalid storage class {variableInfo.StorageClass}");
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

							results[result] = (int)results[a] + (int)results[b];
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

							results[result] = (int)results[a] % (int)results[b];
							types[result] = types[type];
							break;
						}
					case ShaderOp.Gather:
						{
							shaderReader = shaderReader.Gather(out result, out int type, out int texture, out int coord);

							var variableData = variableDecorations[texture];

							var textureImage = imageAttachments[(int)variableData.Binding!];
							var textureData = textureImage.Data.Span;

							var (x, y) = results2[coord];

							int textureIndex = (int)x + (int)y * textureImage.Size.Width;

							if (types[type] != ShaderType.Int(4))
							{
								throw new InvalidOperationException("Gather result type must be int32");
							}

							results[result] = BitConverter.ToInt32(textureData[(textureIndex * 4)..]);
							types[result] = types[type];
							break;
						}
					case ShaderOp.Subtract:
						{
							shaderReader = shaderReader.Subtract(out result, out int type, out int a, out int b);

							if (types[type] != types[a] || types[type] != types[b])
							{
								throw new InvalidOperationException($"Subtract operands must have the same type");
							}

							results[result] = (int)results[a] - (int)results[b];
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

							results[result] = (int)results[a] - (int)results[b];
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

							results[result] = (int)results[condition] == 0 ? results[trueValue] : results[falseValue];
							types[result] = types[type];
							break;
						}
					case ShaderOp.Return:
						isRunning = false;
						break;
					case ShaderOp.EntryPoint:
						shaderReader = shaderReader.Skip();
						break;
					default:
						throw new InvalidOperationException($"Unknown opcode {op}");
				}
			}
		}
	}
}
