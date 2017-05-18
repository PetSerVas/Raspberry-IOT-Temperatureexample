using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageClient
{
	class Program
	{
		static string connectionString = "HostName=TemperaturHub.azure-devices.net;" + 
			"SharedAccessKeyName=iothubowner;" + 
			"SharedAccessKey=wFyWdx6shn9PYd1g8zkB0RCgpTNlDfVITXE2yoUf4hM=";
		static string iotHubD2cEndpoint = "messages/events";
		static EventHubClient eventHubClient;
		//static string deviceKey;
		//static DeviceClient deviceClient;
		//static string iotHubUri = "TemperaturHub.azure-devices.net";

		static void Main(string[] args)
		{
			Console.WriteLine("Receive messages. Ctrl-C to exit.\n");

			eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, iotHubD2cEndpoint);
			var d2cPartitions = eventHubClient.GetRuntimeInformation().PartitionIds;

			CancellationTokenSource cts = new CancellationTokenSource();

			Console.CancelKeyPress += (s, e) =>
			{
				e.Cancel = true;
				cts.Cancel();
				Console.WriteLine("Exiting...");
				System.Threading.Thread.Sleep(3000);
				return;
			};

			var tasks = new List<Task>();
			foreach (string partition in d2cPartitions)
			{
				tasks.Add(ReceiveMessagesFromHubAsync(partition, cts.Token));
			}

			Task.WaitAll(tasks.ToArray());

			Console.ReadLine();

		}

		private static async Task ReceiveMessagesFromHubAsync(string partition, CancellationToken ct)
		{
			var eventHubReceiver = eventHubClient.GetDefaultConsumerGroup().CreateReceiver(partition, DateTime.UtcNow);

			while (true)
			{
				if (ct.IsCancellationRequested) break;
				EventData eventData = await eventHubReceiver.ReceiveAsync();
				if (eventData == null) continue;

				string data = Encoding.UTF8.GetString(eventData.GetBytes());
				Console.WriteLine("Message received. Partition: {0} Data: '{1}'", partition, data);
			}
		}
	}
}
