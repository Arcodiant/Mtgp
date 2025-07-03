namespace Mtgp.Proxy.Telnet;

public enum TelnetCommand
    : byte
{
    /// <summary>
    /// SubNegotiation Ends
    /// </summary>
    SE = 240,
    /// <summary>
    /// No-Op
    /// </summary>
    NOP = 241,
    /// <summary>
    /// Data Mark
    /// </summary>
    DM = 242,
    /// <summary>
    /// Break
    /// </summary>
    BRK = 243,
    /// <summary>
    /// Interrupt Process
    /// </summary>
    IP = 244,
    /// <summary>
    /// Abort Output
    /// </summary>
    AO = 245,
    /// <summary>
    /// Are You There
    /// </summary>
    AYT = 246,
    /// <summary>
    /// Erase Character
    /// </summary>
    EC = 247,
    /// <summary>
    /// Erase Line
    /// </summary>
    EL = 248,
    /// <summary>
    /// Go Ahead
    /// </summary>
    GA = 249,
    /// <summary>
    /// SubNegotiation
    /// </summary>
    SB = 250,
    /// <summary>
    /// Negotiation Will
    /// </summary>
    WILL = 251,
    /// <summary>
    /// Negotiation Wont
    /// </summary>
    WONT = 252,
    /// <summary>
    /// Negotiation Do
    /// </summary>
    DO = 253,
    /// <summary>
    /// Negotiation Dont
    /// </summary>
    DONT = 254,
    /// <summary>
    /// Interpret As Command
    /// </summary>
    IAC = 255
}

public static class TelnetCommandExtensions
{
	public static bool IsNegotiation(this TelnetCommand command)
        => command switch
	        {
		        TelnetCommand.WILL
                    or TelnetCommand.WONT
                    or TelnetCommand.DO
                    or TelnetCommand.DONT => true,
		        _ => false,
	        };

	public static bool IsImperative(this TelnetCommand command)
        => command switch
	        {
		        TelnetCommand.DO or TelnetCommand.DONT => true,
		        _ => false,
	        };

    public static bool IsInformative(this TelnetCommand command)
		=> command switch
		{
			TelnetCommand.WILL or TelnetCommand.WONT => true,
			_ => false,
		};

	public static bool IsPositive(this TelnetCommand command)
        => command switch
	        {
		        TelnetCommand.WILL or TelnetCommand.DO => true,
		        _ => false,
	        };

	public static TelnetCommand AsPositive(this TelnetCommand command)
        => command.IsPositive()
            ? command
            : command.Negate();

	public static TelnetCommand AsNegative(this TelnetCommand command)
        => !command.IsPositive()
            ? command
            : command.Negate();

	public static TelnetCommand Reciprocal(this TelnetCommand command)
        => command switch
	        {
		        TelnetCommand.WILL => TelnetCommand.DO,
		        TelnetCommand.WONT => TelnetCommand.DONT,
		        TelnetCommand.DO => TelnetCommand.WILL,
		        TelnetCommand.DONT => TelnetCommand.WONT,
		        _ => command,
	        };

	public static TelnetCommand Negate(this TelnetCommand command)
        => command switch
	        {
		        TelnetCommand.WILL => TelnetCommand.WONT,
		        TelnetCommand.WONT => TelnetCommand.WILL,
		        TelnetCommand.DO => TelnetCommand.DONT,
		        TelnetCommand.DONT => TelnetCommand.DO,
		        _ => command,
	        };
}
