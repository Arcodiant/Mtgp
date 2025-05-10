using Mtgp.Shader;
using System.Text;

namespace Mtgp.Server;

public static class MtgpClientExtensions
{
	public static async Task AddClearBufferAction(this MtgpClient client, int actionList, int image, char value)
		=> await client.AddClearBufferAction(actionList, image, Encoding.UTF32.GetBytes([value]));

	public static async Task AddClearBufferAction(this MtgpClient client, int actionList, int image, ColourField value)
	{
		switch(value.ColourFormat)
		{
			case ColourFormat.Ansi16:
				await client.AddClearBufferAction(actionList, image, [Ansi16Colour.ToByte(value.Ansi16Colour)]);
				break;
			case ColourFormat.Ansi256:
				await client.AddClearBufferAction(actionList, image, [value.Ansi256Colour.Value]);
				break;
			case ColourFormat.TrueColour:
				var data = new byte[12];

				new BitWriter(data)
					.Write(value.TrueColour.R)
					.Write(value.TrueColour.G)
					.Write(value.TrueColour.B);

				await client.AddClearBufferAction(actionList, image, data);
				break;
			default:
				throw new NotSupportedException($"Unsupported colour format: {value.ColourFormat}");
		}
	}
}
