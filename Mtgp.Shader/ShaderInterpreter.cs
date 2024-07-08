namespace Mtgp.Shader
{
	public class ShaderInterpreter(Memory<byte> compiledShader, int[] inputMappings, int[] outputMappings)
	{
		private readonly Memory<byte> compiledShader = compiledShader;
		private readonly int[] inputMappings = inputMappings;
		private readonly int[] outputMappings = outputMappings;

		private class VariableInfo
		{
			public uint? Location { get; set; }
			public uint? Binding { get; set; }
			public Builtin? Builtin { get; set; }
			public ShaderStorageClass? StorageClass { get; set; }
		}

		public struct Builtins(int vertexIndex = 0, int instanceIndex = 0, int positionX = 0, int positionY = 0)
		{
			public int VertexIndex = vertexIndex;
			public int InstanceIndex = instanceIndex;
			public int PositionX = positionX;
			public int PositionY = positionY;
		}

		public void Execute(Memory<byte>[] attachments, Builtins inputBuiltins, ReadOnlySpan<byte> input, ref Builtins outputBuiltins, Span<byte> output)
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

			var results = new Dictionary<int, int>();

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
					case ShaderOp.Variable:
						shaderReader = shaderReader.Variable(out result, out var storageClass);

						SetVariableInfo(result, x => x.StorageClass = storageClass);
						break;
					case ShaderOp.Constant:
						{
							shaderReader = shaderReader.Constant(out result, out int value);

							results[result] = value;
							break;
						}
					case ShaderOp.Load:
						{
							shaderReader = shaderReader.Load(out result, out int variable);

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
											_ => throw new InvalidOperationException($"Invalid builtin {variableInfo.Builtin}"),
										};
									}
									else if (variableInfo.Location is not null)
									{
										results[result] = BitConverter.ToInt32(input[this.inputMappings[(int)variableInfo.Location!]..]);
									}
									else
									{
										throw new InvalidOperationException("Input variable has no target decoration");
									}
									break;
								default:
									throw new InvalidOperationException($"Invalid storage class {variableInfo.StorageClass}");
							}

							break;
						}
					case ShaderOp.Store:
						{
							shaderReader = shaderReader.Store(out int variable, out int value);

							var variableInfo = variableDecorations[variable];

							int valueToStore = results[value];

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
							shaderReader = shaderReader.Add(out result, out int a, out int b);

							results[result] = results[a] + results[b];
							break;
						}
					case ShaderOp.Sample:
						{
							shaderReader = shaderReader.Sample(out result, out int texture, out int x, out int y);

							var variableData = variableDecorations[texture];

							var textureData = attachments[(int)variableData.Binding!].Span;

							int textureX = results[x];
							int textureY = results[y];

							int textureIndex = textureX + textureY * 80;

							results[result] = BitConverter.ToInt32(textureData[(textureIndex * 4)..]);
							break;
						}
					case ShaderOp.Subtract:
					case ShaderOp.Equals:
						{
							shaderReader = shaderReader.Binary(op, out result, out int a, out int b);

							results[result] = results[a] - results[b];
							break;
						}
					case ShaderOp.Conditional:
						{
							shaderReader = shaderReader.Conditional(out result, out int condition, out int trueValue, out int falseValue);

							results[result] = results[condition] == 0 ? results[trueValue] : results[falseValue];
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
