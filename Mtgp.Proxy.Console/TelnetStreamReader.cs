using Serilog;
using System.Text;

namespace Mtgp;

public class TelnetStreamReader(Stream stream)
{
	private readonly Stream stream = stream;
	private ReceiveState receiveState = ReceiveState.Character;
	private TelnetCommand receivedCommand;
	private TelnetOption sbOption;

	private readonly byte[] buffer = new byte[1024];
	private int offset = 0;
	private int count = 0;

	private readonly List<byte> data = [];

	private enum ReceiveState
	{
		Character,
		Escaped,
		Negotiation,
		SbInitial,
		SbData,
		SbEscaped
	}

	public async Task<TelnetEvent> ReadNextAsync()
	{
		while (true)
		{
			if (offset >= count)
			{
				if (this.receiveState == ReceiveState.Character && this.data.Count > 0)
				{
					var dataBuffer = this.data.ToArray();

					this.data.Clear();

					return new TelnetStringEvent(Encoding.UTF8.GetString(dataBuffer));
				}

				count = await this.stream.ReadAsync(buffer);

				if (count == 0)
				{
					return new TelnetCloseEvent();
				}

				offset = 0;
			}

			byte datum = this.buffer[offset];

			Log.Verbose("Handle byte {Datum}", datum);

			offset++;

			switch (this.receiveState)
			{
				case ReceiveState.Character:
					if ((TelnetCommand)datum == TelnetCommand.IAC)
					{
						this.receiveState = ReceiveState.Escaped;
					}
					else
					{
						this.data.Add(datum);
					}
					break;
				case ReceiveState.Escaped:
					TelnetCommand command = (TelnetCommand)datum;

					switch (command)
					{
						case TelnetCommand.DO:
						case TelnetCommand.DONT:
						case TelnetCommand.WILL:
						case TelnetCommand.WONT:
							this.receiveState = ReceiveState.Negotiation;
							this.receivedCommand = command;
							break;
						case TelnetCommand.IAC:
							this.receiveState = ReceiveState.Character;
							this.data.Add(0xff);
							break;
						case TelnetCommand.SB:
							this.receiveState = ReceiveState.SbInitial;
							break;
						default:
							this.receiveState = ReceiveState.Character;
							break;

					}
					break;
				case ReceiveState.Negotiation:
					this.receiveState = ReceiveState.Character;

					return new TelnetCommandEvent(this.receivedCommand)
					{
						Option = (TelnetOption)datum
					};
				case ReceiveState.SbInitial:
					this.receiveState = ReceiveState.SbData;
					this.sbOption = (TelnetOption)datum;
					break;
				case ReceiveState.SbData:
					if ((TelnetCommand)datum == TelnetCommand.IAC)
					{
						this.receiveState = ReceiveState.SbEscaped;
					}
					else
					{
						this.data.Add(datum);
					}
					break;
				case ReceiveState.SbEscaped:
					if ((TelnetCommand)datum == TelnetCommand.IAC)
					{
						this.data.Add((byte)0xff);
						this.receiveState = ReceiveState.SbData;
					}
					else
					{
						this.receiveState = ReceiveState.Character;
						var dataBuffer = this.data.ToArray();

						this.data.Clear();

						return new TelnetCommandEvent(TelnetCommand.SB)
						{
							Option = this.sbOption,
							Data = dataBuffer
						};
					}
					break;
			}
		}
	}
}

public abstract class TelnetEvent
{
}

public class TelnetCloseEvent
	: TelnetEvent
{
}

public class TelnetCommandEvent(TelnetCommand command)
	: TelnetEvent
{
	public TelnetCommand Command { get; } = command;
	public TelnetOption? Option { get; init; }
	public byte[]? Data { get; init; }
}

public class TelnetStringEvent(string value)
		: TelnetEvent
{
	public string Value { get; } = value;
}
