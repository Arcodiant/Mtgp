namespace Mtgp.Server.Shader;

public abstract record ResourceHandle(int Id);

public interface IResourceHandle
{
	static abstract string ResourceType { get; }
}