namespace SLC_SM_GQIDS_Get_Service_Items
{
	using System;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	internal sealed class ReservationWatcher : IDisposable
	{
		private readonly IConnection _connection;
		private readonly string setId = Guid.NewGuid().ToString();

		internal ReservationWatcher(IConnection connection)
		{
			_connection = connection ?? throw new GenIfException("Could not create a connection.");

			var subscriptionFilter = new SubscriptionFilter(typeof(ResourceManagerEventMessage));
			_connection.OnNewMessage += Connection_OnNewMessage;
			_connection.AddSubscription(setId, subscriptionFilter);
		}

		internal event EventHandler<ResourceManagerEventMessage> OnChanged;

		public void Dispose()
		{
			try
			{
				_connection?.Unsubscribe();
				_connection?.Dispose();
			}
			catch (Exception)
			{
				// Ignore
			}
		}

		private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
		{
			if (e.Message is ResourceManagerEventMessage change)
			{
				OnChanged?.Invoke(this, change);
			}
		}
	}
}