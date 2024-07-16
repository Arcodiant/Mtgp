namespace Mtgp.Shader
{
    public interface IPresentReceiver
    {
        void Clear(AnsiColour foreground, AnsiColour background);
        void Draw(ReadOnlySpan<RuneDelta> value);

        void Present();
    }
}