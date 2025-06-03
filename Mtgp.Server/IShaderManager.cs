using Mtgp.Server.Shader;

namespace Mtgp.Server
{
	public interface IShaderManager
	{
		Task<ShaderHandle> CreateShaderAsync(string code);
	}
}