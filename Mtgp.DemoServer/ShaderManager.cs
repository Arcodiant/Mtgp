using Mtgp.Shader;
using Mtgp.Shader.Tsl;

namespace Mtgp.DemoServer;

internal class ShaderManager(MtgpClient client)
{
	public async Task<int> CreateShaderFromFileAsync(string path)
		=> await CreateShaderAsync(await File.ReadAllTextAsync(path));

	public async Task<int> CreateShaderAsync(string code)
	{
		var shaderCompiler = new ShaderCompiler();

		var shaderData = shaderCompiler.Compile(code);

		await client.GetResourceBuilder()
					 .Shader(out var shaderTask, shaderData)
					 .BuildAsync();

		return await shaderTask;
	}

	public async Task<int> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format)
	{
		await client.GetResourceBuilder()
					 .Image(out var imageTask, size, format)
					 .BuildAsync();

		return await imageTask;
	}
}
