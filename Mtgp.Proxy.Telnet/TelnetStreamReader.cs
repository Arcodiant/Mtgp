using System.Text;

namespace Mtgp.Proxy.Telnet;

public class TelnetStreamReader
{
	private ReceiveState receiveState = ReceiveState.Character;
	private AnsiCodeState ansiCodeState = AnsiCodeState.Character;
	private TelnetCommand receivedCommand;
	private TelnetOption sbOption;

	private enum ReceiveState
	{
		Character,
		Escaped,
		Negotiation,
		SbInitial,
		SbData,
		SbEscaped
	}

	private enum AnsiCodeState
	{
		Character,
		Escaped,
		Csi
	}

	public void GetEvents(ReadOnlySpan<byte> data, Queue<TelnetEvent> events)
	{
		var buffer = new List<byte>();
		int offset = 0;

		while (offset < data.Length)
		{
			byte datum = data[offset];

			offset++;

			switch (this.receiveState)
			{
				case ReceiveState.Character:
					if ((TelnetCommand)datum == TelnetCommand.IAC)
					{
						this.receiveState = ReceiveState.Escaped;

						if (buffer.Count > 0)
						{
							events.Enqueue(new TelnetStringEvent([.. buffer]));
							buffer.Clear();
						}
					}
					else
					{
						switch(this.ansiCodeState)
						{
							case AnsiCodeState.Character:
								if (datum == 0x1b)
								{
									this.ansiCodeState = AnsiCodeState.Escaped;
								}
								else if (datum == 0x9b)
								{
									this.ansiCodeState = AnsiCodeState.Csi;
								}
								else
								{
									buffer.Add(datum);
								}
								break;
							case AnsiCodeState.Escaped:
								if (datum == '[')
								{
									this.ansiCodeState = AnsiCodeState.Csi;
								}
								else
								{
									this.ansiCodeState = AnsiCodeState.Character;
									buffer.Add(0x1b);

									if (datum != 0x1b)
									{
										buffer.Add(datum);
									}
								}
								break;
							case AnsiCodeState.Csi:
								if(char.IsLetter((char)datum))
								{
									this.ansiCodeState = AnsiCodeState.Character;
									events.Enqueue(new TelnetCsiEvent([.. buffer], (char)datum));
									buffer.Clear();
								}
								else
								{
									buffer.Add(datum);
								}
								break;
						}
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
							buffer.Add(0xff);
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

					events.Enqueue(new TelnetCommandEvent(this.receivedCommand, (TelnetOption)datum));
					break;
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
						buffer.Add(datum);
					}
					break;
				case ReceiveState.SbEscaped:
					if ((TelnetCommand)datum == TelnetCommand.IAC)
					{
						buffer.Add(0xff);
						this.receiveState = ReceiveState.SbData;
					}
					else
					{
						this.receiveState = ReceiveState.Character;
						events.Enqueue(new TelnetSubNegotiationEvent(this.sbOption, [.. buffer]));
						buffer.Clear();
					}
					break;
			}
		}

		if (buffer.Count > 0)
		{
			events.Enqueue(new TelnetStringEvent(Encoding.UTF8.GetString([.. buffer])));
			buffer.Clear();
		}
	}
}

public abstract record TelnetEvent;

public record TelnetCommandEvent(TelnetCommand Command, TelnetOption Option)
	: TelnetEvent;

public record TelnetSubNegotiationEvent(TelnetOption Option, byte[] Data)
	: TelnetEvent;

public record TelnetStringEvent(string Value)
	: TelnetEvent
{
	public TelnetStringEvent(ReadOnlySpan<byte> data)
		: this(Encoding.UTF8.GetString(data))
	{
	}
}

public record TelnetCloseEvent
	: TelnetEvent;

public record TelnetCsiEvent(string Value, char Suffix)
	: TelnetEvent
{
	public TelnetCsiEvent(ReadOnlySpan<byte> data, char suffix)
		: this(Encoding.UTF8.GetString(data), suffix)
	{
	}
}
