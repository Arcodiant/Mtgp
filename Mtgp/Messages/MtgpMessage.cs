using System.Text.Json.Serialization;

namespace Mtgp.Messages;

public record MtgpMessage(int Id, MtgpMessageType Type);

public record MtgpRequest(int Id, [property:JsonIgnore]string Command)
	: MtgpMessage(Id, MtgpMessageType.Request);

public record MtgpResponse(int Id, string Result)
	: MtgpMessage(Id, MtgpMessageType.Response);

public enum MtgpMessageType
{
	Request,
	Response
}
