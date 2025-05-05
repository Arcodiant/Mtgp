using FluentAssertions;
using System.Text;

namespace Mtgp.Proxy.Telnet.Tests
{
	[TestClass]
	public sealed class TelnetStreamReaderTests
	{
		[TestMethod]
		public void ShouldReadString()
		{
			var stream = new MemoryStream();

			var writer = new StreamWriter(stream);

			writer.Write("Hello, World!");

			writer.Flush();

			stream.Position = 0;

			var reader = new TelnetStreamReader();

			var result = reader.GetEvents(stream.ToArray());

			result.Should().BeEquivalentTo([new TelnetStringEvent("Hello, World!")]);
		}

		[TestMethod]
		public void ShouldReadEscapedCommand()
		{
			var stream = new MemoryStream();
			stream.Write([0xFF, (byte)TelnetCommand.DO, (byte)TelnetOption.Echo]);
			stream.Position = 0;
			var reader = new TelnetStreamReader();
			var result = reader.GetEvents(stream.ToArray());
			result.Should().BeEquivalentTo([new TelnetCommandEvent(TelnetCommand.DO, TelnetOption.Echo)]);
		}

		[TestMethod]
		public void ShouldReadSubNegotiation()
		{
			var stream = new MemoryStream();
			stream.Write([0xFF, (byte)TelnetCommand.SB, (byte)TelnetOption.NegotiateAboutWindowSize, 0x00, 0xFF, (byte)TelnetCommand.SE]);
			stream.Position = 0;
			var reader = new TelnetStreamReader();
			var result = reader.GetEvents(stream.ToArray());
			result.Should().BeEquivalentTo([new TelnetCommandEvent(TelnetCommand.SB, TelnetOption.NegotiateAboutWindowSize, [0x00])]);
		}

		[TestMethod]
		public void ShouldReadEscapedFF()
		{
			var stream = new MemoryStream();
			stream.Write([0xFF, 0xFF]);
			stream.Position = 0;
			var reader = new TelnetStreamReader();
			var result = reader.GetEvents(stream.ToArray());
			result.Should().BeEquivalentTo([new TelnetStringEvent(Encoding.UTF8.GetString([0xff]))]);
		}

		[TestMethod]
		public void ShouldReadMultipleEvents()
		{
			var stream = new MemoryStream();
			stream.Write([0x41, 0x42, 0x43, 0xFF, (byte)TelnetCommand.DO, (byte)TelnetOption.Echo, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x2C, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21]);
			stream.Position = 0;
			var reader = new TelnetStreamReader();
			var result = reader.GetEvents(stream.ToArray());
			result.Should().HaveCount(3);
			result.ElementAt(0).Should().Be(new TelnetStringEvent("ABC"));
			result.ElementAt(1).Should().Be(new TelnetCommandEvent(TelnetCommand.DO, TelnetOption.Echo));
			result.ElementAt(2).Should().Be(new TelnetStringEvent("Hello, World!"));
		}
	}
}
