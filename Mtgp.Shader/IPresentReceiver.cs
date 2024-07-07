namespace Mtgp.Shader
{
    public interface IPresentReceiver
    {
        void Clear(AnsiColour foreground, AnsiColour background);
        void Present(ReadOnlySpan<RuneDelta> value);
    }
}