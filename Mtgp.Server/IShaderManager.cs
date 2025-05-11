using Mtgp.Server.Shader;
using Mtgp.Shader;

namespace Mtgp.Server
{
	public interface IShaderManager
	{
		Task<ImageHandle> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format);
		Task<ShaderHandle> CreateShaderAsync(string code);
	}
}