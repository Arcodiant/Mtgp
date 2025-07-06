using System.Text;

namespace Mtgp.Shader;

public static class ShaderDisassembler
{
	public static string Disassemble(Span<byte> shaderCode)
	{
		var assembly = new StringBuilder();

		try
		{
			var shaderReader = new ShaderReader(shaderCode);

			while (!(shaderReader.EndOfStream || shaderReader.Next == ShaderOp.None))
			{
				assembly.Append(shaderReader.Next.ToString());

				switch (shaderReader.Next)
				{
					case ShaderOp.EntryPoint:
						{
							shaderReader.EntryPoint(out uint varCount);
							var variables = new int[varCount];
							shaderReader.EntryPoint(variables, out _);

							assembly.AppendLine($"({string.Join(", ", variables)})");
						}
						break;
					case ShaderOp.Decorate:
						{
							shaderReader.Decorate(out int target, out var decoration);

							switch (decoration)
							{
								case ShaderDecoration.Location:
									{
										shaderReader.DecorateLocation(out _, out uint location);
										assembly.AppendLine($"({target}, {decoration}, {location})");
									}
									break;
								case ShaderDecoration.Builtin:
									{
										shaderReader.DecorateBuiltin(out _, out var builtin);
										assembly.AppendLine($"({target}, {decoration}, {builtin})");
									}
									break;
								case ShaderDecoration.Binding:
									{
										shaderReader.DecorateBinding(out _, out var binding);
										assembly.AppendLine($"({target}, {decoration}, {binding})");
									}
									break;
								default:
									assembly.AppendLine($"({target}, {decoration}, ...?) - Unknown Decoration");
									break;
							}
						}
						break;
					case ShaderOp.TypePointer:
						{
							shaderReader.TypePointer(out int result, out var storageClass, out int type);

							assembly.AppendLine($"({result}, {storageClass}, {type})");
						}
						break;
					case ShaderOp.TypeBool:
						{
							shaderReader.TypeBool(out int result);

							assembly.AppendLine($"({result})");
						}
						break;
					case ShaderOp.TypeInt:
						{
							shaderReader.TypeInt(out int result, out int width);

							assembly.AppendLine($"({result}, {width})");
						}
						break;
					case ShaderOp.TypeFloat:
						{
							shaderReader.TypeFloat(out int result, out int width);

							assembly.AppendLine($"({result}, {width})");
						}
						break;
					case ShaderOp.TypeVector:
						{
							shaderReader.TypeVector(out int result, out int type, out int count);

							assembly.AppendLine($"({result}, {type}, {count})");
						}
						break;
					case ShaderOp.TypeImage:
						{
							shaderReader.TypeImage(out int result, out int type, out int dim);

							assembly.AppendLine($"({result}, {type}, {dim})");
						}
						break;
					case ShaderOp.TypeStruct:
						{
							shaderReader.TypeStruct(out int result, out int count);

                            var members = new int[count];

                            shaderReader.TypeStruct(out _, members, out _);

                            assembly.AppendLine($"({result}, {string.Join(", ", members)})");
                        }
						break;
					case ShaderOp.Variable:
						{
							shaderReader.Variable(out int result, out var storageClass, out int type);

							assembly.AppendLine($"({result}, {storageClass}, {type})");
						}
						break;
					case ShaderOp.Load:
						{
							shaderReader.Load(out int result, out int type, out int variable);

							assembly.AppendLine($"({result}, {type}, {variable})");
						}
						break;
					case ShaderOp.Store:
						{
							shaderReader.Store(out int pointer, out var value);

							assembly.AppendLine($"({pointer}, {value})");
						}
						break;
					case ShaderOp.Constant:
						{
							shaderReader.Constant(out int result, out int type, out int value);

							assembly.AppendLine($"({result}, {type}, {value})");
						}
						break;
					case ShaderOp.Gather:
						{
							shaderReader.Gather(out int result, out int type, out int texture, out int coordinate);

							assembly.AppendLine($"({result}, {type}, {texture}, {coordinate})");
						}
						break;
					case ShaderOp.Equals:
						{
							shaderReader.Equals(out int result, out int type, out int left, out int right);

							assembly.AppendLine($"({result}, {type}, {left}, {right})");
						}
						break;
					case ShaderOp.Add:
						{
							shaderReader.Add(out int result, out int type, out int left, out int right);

							assembly.AppendLine($"({result}, {type}, {left}, {right})");
						}
						break;
					case ShaderOp.Subtract:
						{
							shaderReader.Subtract(out int result, out int type, out int left, out int right);

							assembly.AppendLine($"({result}, {type}, {left}, {right})");
						}
						break;
					case ShaderOp.Conditional:
						{
							shaderReader.Conditional(out int result, out int type, out int condition, out int trueValue, out int falseValue);

							assembly.AppendLine($"({result}, {type}, {condition}, {trueValue}, {falseValue})");
						}
						break;
					case ShaderOp.CompositeConstruct:
						{
							shaderReader.CompositeConstruct(out int count);

							var constituents = new int[count];

							shaderReader.CompositeConstruct(out int result, out int type, constituents, out _);

							assembly.AppendLine($"({result}, {type}, {string.Join(", ", constituents)})");
						}
						break;
					case ShaderOp.AccessChain:
						{
							shaderReader.AccessChain(out int count);

							var indices = new int[count];

							shaderReader.AccessChain(out int result, out int type, out int baseId, indices, out _);

							assembly.AppendLine($"({result}, {type}, {baseId}, {string.Join(", ", indices)})");
						}
						break;
					case ShaderOp.Return:
						{
							assembly.AppendLine();
						}
						break;
					default:
						shaderReader.Skip(out uint wordCount);

						Span<byte> raw = new byte[(int)wordCount * 4];

						shaderReader.Skip(raw, out _);

						assembly.Append(" - Unknown Opcode [(");
						assembly.Append(shaderReader.Next);
						assembly.Append(", ");
						assembly.Append(wordCount);
						assembly.Append(')');

						for (int index = 1; index < wordCount; index++)
						{
							assembly.Append(", ");
							assembly.Append(BitConverter.ToUInt32(raw[(index * 4)..][..4]));
						}

						assembly.AppendLine("]");
						break;
				}

				shaderReader = shaderReader.Skip();
			}

			return assembly.ToString();
		}
		catch (Exception ex)
		{
			throw new DisassemblyException("Failed to disassemble shader", assembly.ToString(), ex);
		}
	}
}

public class DisassemblyException
	: Exception
{
	public DisassemblyException(string message, string assembly)
		: base(message)
	{
		this.Data.Add("Assembly", assembly);
	}

	public DisassemblyException(string message, string assembly, Exception ex)
		: base(message, ex)
	{
		this.Data.Add("Assembly", assembly);
	}
}
