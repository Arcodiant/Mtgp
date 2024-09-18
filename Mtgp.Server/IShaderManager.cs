using Mtgp.Shader;

namespace Mtgp.Server
{
	public interface IShaderManager
	{
		Task<int> CreateImageFromData(byte[] data, Extent3D size, ImageFormat format);
		Task<int> CreateShaderAsync(string code);
		Task<int> CreateShaderFromFileAsync(string path);
	}
}