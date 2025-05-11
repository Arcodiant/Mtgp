using Mtgp.Server.Shader;

namespace Mtgp.Server;

public class BufferManager(MtgpClient client, int defaultBufferSize = 4096)
	: IBufferManager
{
	private class BufferState(BufferHandle handle, int size, int nextOffset = 0)
	{
		public BufferHandle Handle { get; } = handle;
		public int Size { get; } = size;
		public int NextOffset { get; set; } = nextOffset;
		public int Remaining => this.Size - this.NextOffset;
	}

	private readonly List<BufferState> buffers = [];

	public async Task<(BufferHandle Buffer, int Offset)> Allocate(int size)
	{
		for (int i = 0; i < this.buffers.Count; i++)
		{
			var buffer = this.buffers[i];

			if (buffer.Remaining >= size)
			{
				int offset = buffer.NextOffset;

				buffer.NextOffset += size;

				return (buffer.Handle, offset);
			}
		}

		int newBufferSize = size > defaultBufferSize
								? (int)Math.Ceiling(size / (double)defaultBufferSize) * defaultBufferSize
								: defaultBufferSize;

		await client.GetResourceBuilder()
						.Buffer(out var bufferTask, newBufferSize)
						.BuildAsync();

		var bufferId = await bufferTask;

		this.buffers.Add(new BufferState(bufferId, newBufferSize, size));

		return (bufferId, 0);
	}
}
