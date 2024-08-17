namespace Mtgp.Shader;

public record Scale(float X, float Y, float Z)
{
	public static implicit operator (float X, float Y, float Z)(Scale scale) => (scale.X, scale.Y, scale.Z);
	public static implicit operator Scale((float X, float Y, float Z) scale) => new(scale.X, scale.Y, scale.Z);
}
