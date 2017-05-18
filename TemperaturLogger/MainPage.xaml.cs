using DisplayI2C;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Sensors.Dht;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TemperaturLogger
{
	public sealed partial class MainPage : Page
    {
		private DispatcherTimer timer;
		private GpioController gpio;
		private Sensors.Dht.Dht11 _dht;
		private GpioPin _pin;
		private Single Temperature;
		private Single Humidity;
		private DateTimeOffset Date;
		private DhtReading reading;

		private I2cDevice LCDDisplay;
		private displayI2C Display;

		static DeviceClient deviceClient;

		//static string iotHubUri = "Endpoint=sb://raspibus.servicebus.windows.net/;" + 
		//	"SharedAccessKeyName=RootManageSharedAccessKey;" + 
		//	"SharedAccessKey=L+Q17q+e+xjy9l3iSmbh4HpqTp2m4uZWRZvjqte5bDU=;" + 
		//	"EntityPath=messagequeue";
		//static string deviceKey = "SMX+phxezALzaaQ7l5mNI8HPpZ9MuuFs0PNMlB5GQsc="; // device key
		//static string deviceId = "RaspiPC";
		static string ConnectionString = "HostName=TemperaturHub.azure-devices.net;" + 
			"DeviceId=RaspiPC;" + 
			"SharedAccessKey=SMX+phxezALzaaQ7l5mNI8HPpZ9MuuFs0PNMlB5GQsc=";

		public MainPage()
		{
			InitializeComponent();
			GpioInit();
			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(5);
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		~MainPage()
		{
			_dht.Dispose();
			_pin.Dispose();
			Display.clrscr();
			LCDDisplay.Dispose();
		}
		private async void GpioInit()
		{
			gpio = GpioController.GetDefault();
			if (gpio == null)
			{
				return;
			}

			_pin = gpio.OpenPin(5);
			_dht = new Dht11(_pin, GpioPinDriveMode.Input);
			LCDDisplay = await InitializeDisplay();
			Display = new displayI2C(LCDDisplay);
			Display.init();
		}

		private async void Timer_Tick(object sender, object e)
		{
			await ReadData();
			ShowOnDisplay();
            await StoreData_SD();
			SendDeviceToCloudMessageAsync();
		}

        private async Task StoreData_SD()
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            //StorageFolder folder = ApplicationData.Current.RoamingFolder;
            StorageFile file = await folder.CreateFileAsync("Daten.txt");
            //var datei = await file.OpenAsync(FileAccessMode.ReadWrite);
            //StreamWriter writer = new StreamWriter(datei);
            List<string> Text = new List<string>();
            Text.Add("Temperature: " + Temperature + "°C");
            Text.Add("Luftfeuchtigkeit: " + Humidity + "%");
            Text.Add("Datum/Zeit: " + Date.ToString(string.Format("dd.MM.yyyy, HH:mm:ss")));
            await FileIO.AppendLinesAsync(file, Text);
        }

        private async Task ReadData()
		{
			reading = new DhtReading();

			try
			{
				reading = await _dht.GetReadingAsync();
			}
			catch( Exception e )
			{

			}
			if (reading.IsValid)
			{
				this.Temperature = Convert.ToSingle(reading.Temperature);
				this.Humidity = Convert.ToSingle(reading.Humidity);
				this.Date = DateTimeOffset.Now;

				F_Temperature.Text = Temperature.ToString();
				this.F_Humidity.Text = Humidity.ToString();
				this.F_Date.Text = Date.ToString(string.Format("dd.mm.yyyy, HH:MM:ss"));
			}
			else
			{
				throw new Exception("DHT Device did not answer");
			}
		}

		private async Task<I2cDevice> InitializeDisplay()
		{
			// search for I2C Devices if needed
			//List<byte> result = await FindDevicesAsync();
			try
			{
				string i2cDeviceSelector = I2cDevice.GetDeviceSelector();
				DeviceInformationCollection devices =
					await DeviceInformation.FindAllAsync(i2cDeviceSelector);
				DeviceInformation DI = devices[0];

				var LCD_settings = new I2cConnectionSettings(0x27);
				LCD_settings.BusSpeed = I2cBusSpeed.StandardMode;
				LCD_settings.SharingMode = I2cSharingMode.Shared;

				return await I2cDevice.FromIdAsync(DI.Id, LCD_settings);
			}
			catch( Exception e )
			{
				return null;
			}
		}

		private void ShowOnDisplay()
		{
			Display.clrscr();
			Task.Delay(3).Wait();
			string Temp = "Humidity: " + Humidity.ToString() + " %";
			Display.prints(Temp);

			Temp = "Temperatur: " + Temperature.ToString() + " Grad";
			Display.gotoSecondLine();
			Display.prints(Temp);
		}

		private async void SendDeviceToCloudMessageAsync()
		{
			string str = String.Format(
				"Temperatur um {0}: {1} °C, Luftfeuchtigkeit: {2} %",
				DateTime.Now.ToString(string.Format("dd.mm.yyyy, HH:MM:SS")),
				Temperature.ToString(),
				Humidity.ToString());

			var telemetryDataPoint = new
			{
				deviceId = "RaspiPC",
				Temperatur = Temperature,
				Humidity = Humidity,
				partition = 1
			};

			deviceClient = DeviceClient.CreateFromConnectionString(ConnectionString);
				//Create(iotHubUri, 
				//new DeviceAuthenticationWithRegistrySymmetricKey(
				//	deviceId, 
				//	deviceKey), 
				//Microsoft.Azure.Devices.Client.TransportType.Mqtt);

			try
			{
				var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
				var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(messageString));

				await deviceClient.SendEventAsync(message);
			}
			catch( Exception e )
			{

			}

		}

		public async Task<List<byte>> FindDevicesAsync()
		{
			List<byte> returnValue = new List<byte>();

			// Get a selector string that will return all I2C controllers on the system 
			string aqs = I2cDevice.GetDeviceSelector();

			// Find the I2C bus controller device with our selector string 
			var dis = await DeviceInformation.FindAllAsync(aqs).AsTask();

			if (dis.Count > 0)
			{
				const int minimumAddress = 8;
				const int maximumAddress = 77;
				for (byte address = minimumAddress; address <= maximumAddress; address++)
				{
					var settings = new I2cConnectionSettings(address);
					settings.BusSpeed = I2cBusSpeed.FastMode;
					settings.SharingMode = I2cSharingMode.Shared;

					// Create an I2cDevice with our selected bus controller and I2C settings 
					using (I2cDevice device = await I2cDevice.FromIdAsync(dis[0].Id, settings))
					{
						if (device != null)
						{
							try
							{
								byte[] writeBuffer = new byte[1] { 0 };
								device.Write(writeBuffer);
								// If no exception is thrown, there is 
								// a device at this address. 
								returnValue.Add(address);
							}
							catch
							{
								// If the address is invalid, an exception will be thrown. 
							}
						}
					}
				}
			}
			return returnValue;
		}
	}
}
