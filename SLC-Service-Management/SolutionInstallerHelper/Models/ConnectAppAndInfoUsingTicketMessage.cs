namespace SolutionInstallerHelper.Models
{
	using System;

	using Newtonsoft.Json;

	/// <summary>
	/// Represents a message used to connect an application and its information using a connection ticket.
	/// </summary>
	public class ConnectAppAndInfoUsingTicketMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectAppAndInfoUsingTicketMessage"/> class with the specified connection ticket.
		/// </summary>
		/// <param name="ticket">The connection ticket for establishing the connection.</param>
		public ConnectAppAndInfoUsingTicketMessage(string ticket)
		{
			ConnectionTicket = ticket;
			ClientAppName = "ServiceManagementInstaller";
			ClientAppVersion = "1.0";
			ClientComputerName = Environment.MachineName;
		}

		/// <summary>
		/// Gets or sets the connection ticket used for authentication.
		/// </summary>
		[JsonProperty("connectionTicket")]
		public string ConnectionTicket { get; set; }

		/// <summary>
		/// Gets or sets the name of the client application.
		/// </summary>
		[JsonProperty("clientAppName")]
		public string ClientAppName { get; set; }

		/// <summary>
		/// Gets or sets the version of the client application.
		/// </summary>
		[JsonProperty("clientAppVersion")]
		public string ClientAppVersion { get; set; }

		/// <summary>
		/// Gets or sets the name of the client computer.
		/// </summary>
		[JsonProperty("clientComputerName")]
		public string ClientComputerName { get; set; }
	}
}