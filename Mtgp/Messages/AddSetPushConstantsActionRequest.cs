namespace Mtgp.Messages;

public record AddSetPushConstantsActionRequest(int Id, int ActionList, byte[] Data)
    : MtgpRequest(Id), IMtgpRequestType
{

    public static string Command => "core.shader.addSetPushConstantsAction";
}
