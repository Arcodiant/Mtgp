using Mtgp.Server.Shader;

namespace Mtgp.Server;

internal static class ResourceBuilderExtensions
{
	public static ResourceBuilder BufferView(this ResourceBuilder builder, out Task<BufferViewHandle> task, (BufferHandle Buffer, int Offset) bufferLocation, int size, string? name = null)
		=> builder.BufferView(out task, bufferLocation.Buffer.Id, bufferLocation.Offset, size, name);
}
