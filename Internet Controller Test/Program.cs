using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using NETMF.OpenSource.XBee;
using NETMF.OpenSource.XBee.Api;
using NETMF.OpenSource.XBee.Api.Zigbee;

namespace InternetControllerTest {

	public delegate void IoDataHandler(IoSampleResponse ioPacket);

	public class Program {
		// Setup ports
		private static OutputPort onboardLED = new OutputPort(Pins.ONBOARD_LED, false);	// Turn off the onboard led
		private static OutputPort controlLED = new OutputPort(Pins.GPIO_PIN_D7, false);	// Initially this is off
		private static AnalogInput tmp36 = new AnalogInput(AnalogChannels.ANALOG_PIN_A0);	// Analog pin to read temperature

		private const string DB_ADDRESS = "192.168.2.53";	// The address to the server with the MySQL server

		private enum XBeePortData { Temperature, Luminosity, Pressure, Humidity, LuminosityLux, HeatingOn, ThermoOn, Power }

		private static XBeeApi xBee;

		public static void Main() {
			// Create the Zigbee IO Sample Listener for automated data packets
			IoSampleListener dataListener = new IoSampleListener();
			dataListener.IoDataReceived += dataListener_IoDataReceived;

			// Setup the xBee
			Debug.Print("Initializing XBee...");
			xBee = new XBeeApi("COM1", 9600);
			xBee.EnableDataReceivedEvent();
			xBee.EnableAddressLookup();
			xBee.EnableModemStatusEvent();

			// Add event handling for custom-built packets transmitted over Zigbee
			xBee.AddPacketListener(dataListener);
			xBee.DataReceived += xBee_DataReceived;

			// Connect to the XBee
			xBee.Open();
			Debug.Print("XBee Connected...");

			// Start the listening thread, and add the event handlers
			Listener server = new Listener(5267);
			server.thermoStatusChanged += server_thermoStatusChanged;
			server.programOverrideRequested += server_programOverride;
			server.thermoRuleChanged += server_thermoRuleChanged;
			server.dataRequested += server_dataRequested;

			// Infinite loop
			double airTempSum = 0.0;
			while(true) {
				// Get the sensor reading - perform an average over 20 readings
				airTempSum = 0.0;
				for(int i = 0; i < 20; i++) {
					// Get the air reading
					double voltage = 3.3*tmp36.Read();
					airTempSum += 100.0*voltage - 50.0;

					Thread.Sleep(100);
				}
				double airTemperature = airTempSum/20.0;

				// Update the database - Air temperature
				string airUpdate = "GET /db_test_upload.php?radio_id=40aeba93&temperature=" + airTemperature.ToString("F2") + "&power=3.3\r\n";
				DBSendData(airUpdate);

				// Sleep for the rest of the 2 minutes
				Thread.Sleep(118000);
			}
		}

		private static void DBSendData(string sendURL) {
			// Send data over the ethernet
			using(Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				client.Connect(new IPEndPoint(IPAddress.Parse(DB_ADDRESS), 80));	// Connect to server
				using(NetworkStream netStream = new NetworkStream(client)) {
					byte[] buffer = System.Text.Encoding.UTF8.GetBytes(sendURL);
					netStream.Write(buffer, 0, buffer.Length);
				}
			}

			// Print string to debug console
			Debug.Print(sendURL);
		}

		private static byte[] FormatApiMode(byte[] input) {
			// Determine the size of the array
			const byte marker = 0x7d;
			int count = 0;
			foreach(byte b in input) if(b != marker) ++count;

			// Check if any markers need to be removed
			if(count == input.Length) return input;
			else {
				// Iterate through each item
				byte[] copy_array = new byte[count];
				int n = 0;
				foreach(byte b in input)
					if(b != marker) copy_array[n++] = b;

				return copy_array;
			}
		}

		private static unsafe float byteToFloat(byte[] byte_array) {
			uint ret = (uint) (byte_array[0] << 0 | byte_array[1] << 8 | byte_array[2] << 16 | byte_array[3] << 24);
			float r = *((float*) &ret);	// This should work?
			return r;
		}

		private static unsafe byte[] floatToByte(float value) {
			if(sizeof(uint) != 4) throw new Exception("uint is not a 4-byte variable on this system!");

			uint asInt = *((uint*) &value);
			byte[] byte_array = new byte[sizeof(uint)];

			byte_array[0] = (byte) (asInt & 0xFF);
			byte_array[1] = (byte) ((asInt >> 8) & 0xFF);
			byte_array[2] = (byte) ((asInt >> 16) & 0xFF);
			byte_array[3] = (byte) ((asInt >> 24) & 0xFF);

			return byte_array;
		}

		static void xBee_DataReceived(XBeeApi receiver, byte[] data, XBeeAddress sender) {
			// Format the data packet to remove 0x7d instances (assuming API mode is 2)
			byte[] packet = Program.FormatApiMode(data);

			// Create the http request string
			string dataUpdate = "GET /db_test_upload.php?radio_id=";

			// Get the sensor
			for(int i = 4; i < sender.Address.Length; i++) dataUpdate += sender.Address[i].ToString("x").ToLower();

			// Iterate through the data
			int data_length = packet.Length - 18;	// This is needed if the routers on in AT mode - they send the whole packet
//			int data_length = packet.Length;		// This is needed if the routers are in API mode - they send only the data packet
			if(data_length % 5 != 0) return;	// Something funny happened
			else {
				int num_sensors = data_length/5;
				int byte_pos = 17;	// The starting point in the data to read the sensor data in AT mode
//				int byte_pos = 0;	// The staarting point in the data to read the sensor data in API mode
				for(int cur_sensor = 0; cur_sensor < num_sensors; cur_sensor++) {
					// Determine the type of reading
					bool isPressure = false;
					if(packet[byte_pos] == 0x01) dataUpdate += "&temperature=";
					else if(packet[byte_pos] == 0x02) dataUpdate += "&luminosity=";
					else if(packet[byte_pos] == 0x04) {
						dataUpdate += "&pressure=";
						isPressure = true;
					} else if(packet[byte_pos] == 0x08) dataUpdate += "&humidity=";
					else if(packet[byte_pos] == 0x10) dataUpdate += "&power=";
					else if(packet[byte_pos] == 0x20) dataUpdate += "&luminosity_lux=";
					else if(packet[byte_pos] == 0x40) dataUpdate += "&heating_on=";
					else if(packet[byte_pos] == 0x80) dataUpdate += "&thermo_on=";
					else return;	// Something funny happened
					++byte_pos;

					// Convert the data
					byte[] fdata = { packet[byte_pos+0], packet[byte_pos+1], packet[byte_pos+2], packet[byte_pos+3] };
					float fvalue = byteToFloat(fdata);
					if(isPressure) {
						// Convert station pressure to altimiter pressure
						double Pmb = 0.01*fvalue;
						double hm = 167.64;
						fvalue = (float) (System.Math.Pow(1 + 8.422881e-5*(hm/System.Math.Pow(Pmb - 0.3, 0.190284)), 1/0.190284)*(Pmb - 0.3));
					}
					dataUpdate += fvalue.ToString("F2");
					byte_pos += 4;
				}

				// Send data
				dataUpdate += "\r\n";
				DBSendData(dataUpdate);
			}
		}

		static void dataListener_IoDataReceived(IoSampleResponse ioPacket) {
			// Print the data
			Debug.Print(ioPacket.ToString());

			// Create the http request to add the data to the database
			string dataUpdate = "GET /db_test_upload.php?radio_id=";

			// Get the sensor sending the data
			dataUpdate += ioPacket.SourceSerial.ToString().Substring(8, 8).ToLower();

			// Temperature reading
			if(ioPacket.IsAnalogEnabled(IoSampleResponse.Pin.A0)) {
				double voltage = 1.2*ioPacket.GetAnalog(IoSampleResponse.Pin.A0)/1023.0;
				double temperature = 100.0*(voltage - 0.5);
				dataUpdate += "&temperature=" + temperature.ToString("F2");
			}

			// Luminosity reading
			if(ioPacket.IsAnalogEnabled(IoSampleResponse.Pin.A1)) {
				double voltage = 1.2*ioPacket.GetAnalog(IoSampleResponse.Pin.A1)/1023.0;
				dataUpdate += "&luminosity=" + voltage.ToString("F2");
			}

			// Power reading
			if(ioPacket.IsAnalogEnabled(IoSampleResponse.Pin.SupplyVoltage)) {
				// Until I have a battery pack in the system, this will remain at 3.3V
				double voltage = 1.2*ioPacket.GetSupplyVoltage()/1023.0;
				dataUpdate += "&power=" + voltage.ToString("F2");
			} else dataUpdate += "&power=3.3";
			dataUpdate += "\r\n";

			// Send the data
			DBSendData(dataUpdate);
		}

		static void server_thermoStatusChanged(Socket client, RequestArgs request) {
			// Cast the request args
			ThermStatusArgs txCmd = (request is ThermStatusArgs) ? request as ThermStatusArgs : null;

			// Set the LED accordingly
			controlLED.Write(txCmd.TurnOn);
		}

		static void server_programOverride(Socket client, RequestArgs request) {
			throw new NotImplementedException();
		}

		static void server_thermoRuleChanged(Socket client, RequestArgs request) {
			throw new NotImplementedException();
		}

		static void server_dataRequested(Socket client, RequestArgs request) {
			throw new NotImplementedException();
		}
	}

	public class IoSampleListener : IPacketListener {
		// This class was taken from https://xbee.codeplex.com/discussions/440465 and modified for use

		public event IoDataHandler IoDataReceived;

		#region IPacketListener Implementation
		public bool Finished { get { return false; } }

		public void ProcessPacket(XBeeResponse packet) {
			if((packet is IoSampleResponse) && (IoDataReceived != null)) IoDataReceived(packet as IoSampleResponse);
		}

		public XBeeResponse[] GetPackets(int timeout) {
			throw new System.NotSupportedException();
		}
		#endregion IPacketListener Implementation
	}
}
