using Mtgp.Shader;

namespace Mtgp.Messages.Resources;

public record VertexInputInfo(VertexInputInfo.VertexBufferBinding[] VertexBufferBindings, VertexInputInfo.VertexAttribute[] VertexAttributes)
{
	public record VertexBufferBinding(int Binding, int Stride, InputRate InputRate);
	public record VertexAttribute(int Location, int Binding, ShaderType Type, int Offset);
}
