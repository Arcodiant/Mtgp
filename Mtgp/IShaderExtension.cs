namespace Mtgp;

public interface IShaderExtension
{
	int CreateShader(byte[] shader);
	int CreateRenderPass(Dictionary<int, int> attachments, (int Binding, int Width, int Height)[] attachmentDescriptors, int vertexShader, int fragmentShader, (int X, int Y) viewportSize);
	int CreateBuffer(int size);
	int CreatePipe();
	int CreateActionList();
	void AddRunPipelineAction(int actionList, int pipeline);
	void AddClearBufferAction(int actionList);
	void AddIndirectDrawAction(int actionList, int renderPass, int indirectCommandBuffer, int offset);
	void SetActionTrigger(int actionList, int pipe);
	(int FixedBuffer, int InstanceBuffer, int IndirectCommandBuffer, int Pipeline) CreateStringSplitPipeline((int Width, int Height) viewport, int linesPipe);
}
