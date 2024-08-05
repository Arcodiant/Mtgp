﻿using System.Text;

namespace Mtgp.Shader;

public static class ShaderDisassembler
{
	public static string Disassemble(Span<byte> shaderCode)
	{
		var assembly = new StringBuilder();

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
				case ShaderOp.Return:
					{
						assembly.AppendLine();
					}
					break;
				default:
					assembly.AppendLine(" - Unknown Opcode");
					break;
			}

			shaderReader = shaderReader.Skip();
		}

		return assembly.ToString();
	}
}
