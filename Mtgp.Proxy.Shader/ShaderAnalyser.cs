using Mtgp.Shader;

namespace Mtgp.Proxy.Shader;

internal static class ShaderAnalyser
{
	public static (ShaderIoMappings Input, ShaderIoMappings Output) GetMappings(Memory<byte> compiledShader)
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

		var input = new ShaderIoMappingsBuilder();
		var output = new ShaderIoMappingsBuilder();

		shaderReader = shaderReader.EntryPoint(out _);

		var variables = new List<int>();
		var locations = new Dictionary<int, uint>();
		var builtins = new Dictionary<int, Builtin>();
		var storageClasses = new Dictionary<int, ShaderStorageClass>();
		var variableTypes = new Dictionary<int, int>();

		var types = new Dictionary<int, ShaderType>();

		while (!shaderReader.EndOfStream && shaderReader.Next != ShaderOp.None)
		{
			var op = shaderReader.Next;

			switch (op)
			{
				case ShaderOp.Decorate:
					shaderReader.Decorate(out int target, out var decoration);

					if (decoration == ShaderDecoration.Location)
					{
						shaderReader.DecorateLocation(out _, out uint location);

						locations[target] = location;
					}
					else if (decoration == ShaderDecoration.Builtin)
					{
						shaderReader.DecorateBuiltin(out _, out Builtin builtin);

						builtins[target] = builtin;
					}
					break;
				case ShaderOp.TypeInt:
					{
						shaderReader.TypeInt(out int result, out int width);
						types[result] = ShaderType.Int(width);
					}
					break;
				case ShaderOp.TypeFloat:
					{
						shaderReader.TypeFloat(out int result, out int width);
						types[result] = ShaderType.Float(width);
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
				case ShaderOp.TypeRuntimeArray:
					{
						shaderReader.TypeRuntimeArray(out int result, out int elementType);
						var type = types[elementType];
						types[result] = ShaderType.RuntimeArrayOf(type);
					}
					break;
				case ShaderOp.TypePointer:
					{
						shaderReader.TypePointer(out int result, out var storageClass, out int typeId);
						var type = types[typeId];
						types[result] = ShaderType.PointerOf(type, storageClass);
					}
					break;
				case ShaderOp.TypeStruct:
					{
						shaderReader.TypeStruct(out int result, out int memberCount);
						var members = new int[memberCount];

						shaderReader.TypeStruct(out _, members, out _);

						types[result] = ShaderType.StructOf([.. members.Select(x => types[x])]);
					}
					break;
				case ShaderOp.Variable:
					{
						shaderReader.Variable(out int result, out var storageClass, out var type);

						variables.Add(result);
						variableTypes[result] = type;
						storageClasses[result] = storageClass;
					}
					break;
			}

			shaderReader = shaderReader.Skip();
		}

		foreach (var variable in variables)
		{
			if (variableTypes.TryGetValue(variable, out var typeId)
				&& storageClasses.TryGetValue(variable, out var storageClass))
			{
				var type = types[typeId].ElementType!;

				var mappings = storageClass switch
				{
					ShaderStorageClass.Input => input,
					ShaderStorageClass.Output => output,
					_ => null
				};

				if (mappings is not null)
				{
					if (locations.TryGetValue(variable, out var location))
					{
						mappings.AddLocation(type, (int)location);
					}
					else if (builtins.TryGetValue(variable, out var builtin))
					{
						mappings.AddBuiltin(type, builtin);
					}
				}
			}
		}

		return (input.Build(), output.Build());
	}
}
