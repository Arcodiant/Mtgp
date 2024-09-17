namespace Mtgp.Messages;

public record FrameBufferInfo(int Character, int Foreground, int Background)
{
	public static implicit operator FrameBufferInfo((int Character, int Foreground, int Background) tuple)
		=> new(tuple.Character, tuple.Foreground, tuple.Background);
}
