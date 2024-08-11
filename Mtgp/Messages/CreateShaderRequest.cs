namespace Mtgp.Messages;

public class CreateShaderRequest(int id, byte[] shader)
	: MtgpRequest(id, Command), IMtgpRequest<CreateShaderRequest, CreateShaderResponse>
{
	public CreateShaderRequest()
		: this(0, [])
	{
	}

	public byte[] Shader { get; init; } = shader;

	static string IMtgpRequest.Command => Command;

	CreateShaderRequest IMtgpRequest<CreateShaderRequest, CreateShaderResponse>.Request => this;

	public CreateShaderResponse CreateResponse(int shaderId)
		=> new(this.Header.Id, shaderId);

	public const string Command = "core.shader.createShader";
}

public class CreateShaderResponse(int id, int shaderId)
	: MtgpResponse(id)
{
	public CreateShaderResponse()
		: this(0, 0)
	{
	}

	public int ShaderId { get; init; } = shaderId;
}