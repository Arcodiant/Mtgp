namespace Mtgp.Messages;

public class AddDrawActionRequest(int id, int actionList, int renderPipeline, int[] imageAttachments, int[] bufferViewAttachments, AddDrawActionRequest.FrameBufferInfo framebuffer, int instanceCount, int vertexCount)
	: MtgpRequest(id, Command), IMtgpRequestWithResponse<AddDrawActionRequest, MtgpResponse>
{
	public AddDrawActionRequest()
		: this(0, 0, 0, [], [], new (0, 0, 0), 0, 0)
	{
	}

	public int ActionList { get; init; } = actionList;
	public int RenderPipeline { get; init; } = renderPipeline;
	public int[] ImageAttachments { get; init; } = imageAttachments;
	public int[] BufferViewAttachments { get; init; } = bufferViewAttachments;
	public FrameBufferInfo FrameBuffer { get; init; } = framebuffer;
	public int InstanceCount { get; init; } = instanceCount;
	public int VertexCount { get; init; } = vertexCount;

	static string IMtgpRequest.Command => Command;

	AddDrawActionRequest IMtgpRequest<AddDrawActionRequest, MtgpResponse>.Request => this;

	public MtgpResponse CreateResponse()
		=> new(this.Header.Id);

	public const string Command = "core.shader.addDrawAction";

	public record class FrameBufferInfo(int Character, int Foreground, int Background);
}
