namespace Mtgp.Messages;

public class CapabilitiesRequest(int id, string serverDescription, int[] serverVersion)
	: MtgpRequest(id, "core.capabilities")
{
    public CapabilitiesRequest()
        : this(default, "", [])
    {
    }

    public string ServerDescription { get; init; } = serverDescription;
	public int[] ServerVersion { get; init; } = serverVersion;
};
