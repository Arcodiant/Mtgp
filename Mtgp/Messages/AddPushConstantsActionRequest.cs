namespace Mtgp.Messages;

public record AddPushConstantsActionRequest(int Id, int ActionList, byte[] Data)
    : MtgpRequest(Id), IMtgpRequestType
{
    public static string Command => "core.shader.addPushConstantsAction";
}
