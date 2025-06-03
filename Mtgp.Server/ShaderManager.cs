using Mtgp.Server.Shader;
using Mtgp.Shader.Tsl;

namespace Mtgp.Server;

public static class ShaderManagerExtensions
{
	public static async Task<ShaderHandle> CreateShaderFromFileAsync(this IShaderManager manager, string path)
		=> await manager.CreateShaderAsync(await File.ReadAllTextAsync(path));
}

public class ShaderManager(IMessageConnection connection)
	: IShaderManager
{
	public async Task<ShaderHandle> CreateShaderAsync(string code)
	{
		var shaderCompiler = new ShaderCompiler();

		var shaderData = shaderCompiler.Compile(code);

		await connection.GetResourceBuilder()
					 .Shader(out var shaderTask, shaderData)
					 .BuildAsync();

		return await shaderTask;
	}
}
