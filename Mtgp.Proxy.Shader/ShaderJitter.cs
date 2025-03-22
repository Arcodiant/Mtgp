using Mtgp.Shader;
using Sigil;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mtgp.Proxy.Shader
{
	public class ShaderJitter
		: IShaderExecutor
	{
		private readonly static Lazy<MethodInfo> BitConverter__TryWriteBytes_Int32 = new(() => typeof(BitConverter).GetMethod(nameof(BitConverter.TryWriteBytes), [typeof(Span<byte>), typeof(int)])!);

		private readonly static Lazy<MethodInfo> Span_Byte__Slice_Int32 = new(() => typeof(Span<byte>).GetMethod(nameof(Span<byte>.Slice), [typeof(int)])!);

		private readonly static Lazy<MethodInfo> Unsafe__As_Int_Byte = new(() => typeof(Unsafe).GetMethods().Single(x => x.Name == nameof(Unsafe.As) && x.GetGenericArguments().Length == 2).MakeGenericMethod([typeof(int), typeof(byte)]));

		private readonly static Lazy<MethodInfo> MemoryMarshal__CreateSpan = new(() => typeof(MemoryMarshal).GetMethod(nameof(MemoryMarshal.CreateSpan))!.MakeGenericMethod([typeof(byte)]));

		private const int outputIndex = 0;
		private const int outputBuiltinsIndex = 1;
		private delegate void ExecuteDelegate(SpanCollection output, ref ShaderInterpreter.Builtins outputBuiltins);

		private readonly ExecuteDelegate execute;

		public ShaderJitter(Memory<byte> compiledShader, Dictionary<int, int> outputLocationMappings)
		{
			var methodEmitter = Emit<ExecuteDelegate>.NewDynamicMethod();

			var reader = new ShaderReader(compiledShader.Span);

			reader = reader.EntryPointSection(out var entryPointVariables);

			reader = reader.DecorationSection(out var builtins, out var locations);

			reader = reader.TypeSection(out var types, out var constants, out var variables);

			var values = new Dictionary<int, List<Action<Emit<ExecuteDelegate>>>>();

			Emit<ExecuteDelegate> LoadValue(int id)
			{
				if (values.TryGetValue(id, out var actions))
				{
					actions.ForEach(action => action(methodEmitter));

					return methodEmitter;
				}
				else
				{
					throw new InvalidOperationException($"Value {id} not found.");
				}
			}

			foreach (var (id, constant) in constants)
			{
				if (constant.Type == ShaderType.Int(4))
				{
					values[id] = [emitter => emitter.LoadConstant(constant.Value.Int32)];
				}
				else if (constant.Type == ShaderType.Float(4))
				{
					values[id] = [emitter => emitter.LoadConstant(constant.Value.Float)];
				}
				else
				{
					throw new NotImplementedException($"Unimplemented constant type {constant.Type}.");
				}
			}

			foreach (var (id, variable) in variables)
			{
				var local = methodEmitter.DeclareLocal(typeof(Span<byte>), $"output_variable_{id}");

				if (locations.TryGetValue(id, out uint locationValue))
				{
					methodEmitter.LoadArgumentAddress(outputIndex)
									.LoadConstant(outputLocationMappings[(int)locationValue])
									.Call(Span_Byte__Slice_Int32.Value);
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

				values[id] = variable.StorageClass switch
				{
					ShaderStorageClass.Output => [emitter => emitter.LoadLocal(local)],
					_ => throw new NotImplementedException($"Unimplemented storage class {variable.StorageClass}."),
				};
			}

			while (!reader.EndOfStream && reader.Next != ShaderOp.None && reader.Next != ShaderOp.Return)
			{
				switch (reader.Next)
				{
					case ShaderOp.Store:
						reader.Store(out int targetId, out int valueId);
						LoadValue(targetId);
						LoadValue(valueId);
						methodEmitter.Call(BitConverter__TryWriteBytes_Int32.Value);
						methodEmitter.Pop();
						break;
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
			this.execute(output, ref outputBuiltins);
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

		public static ShaderReader DecorationSection(this ShaderReader reader, out Dictionary<int, Builtin> builtins, out Dictionary<int, uint> locations)
		{
			builtins = [];
			locations = [];

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
}