using System;
using Microsoft.SPOT;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.IO;

namespace InternetControllerTest {
/*	/// <summary>
	/// Holds information about a web request
	/// </summary>
	/// <remarks>
	/// Will expand as required, but stay simple until needed.
	/// </remarks>
	public class Request : IDisposable {
		private Socket _client;
		protected string _data;

		internal Request(Socket Client, char[] Data) {
			_client = Client;
			_data = new string(Data);
		}

		/// <summary>
		/// Client IP address
		/// </summary>
		public IPAddress Client {
			get {
				IPEndPoint ip = _client.RemoteEndPoint as IPEndPoint;
				if(ip != null) return ip.Address;
				return null;
			}
		}

		/// <summary>
		/// Send a response back to the client
		/// </summary>
		/// <param name="response"></param>
		public void SendResponse(string response) {
			throw new NotImplementedException("SendResponse not implemented in Response class.");
		}

		#region IDisposable Members

		public void Dispose() {
			if(_client != null) {
				_client.Close();
				_client = null;
			}
		}

		#endregion
	}*/

	public class RequestArgs {
		protected string _command;
		protected string[] _args;

		public RequestArgs(char[] Data) {
			_command = new string(Data);
			_args = _command.Split(':');
		}
	}

	public class ThermStatusArgs : RequestArgs {
		private bool _turnOn;

		public ThermStatusArgs(char[] Data) : base(Data) {
			// Check that there are two args
			if(_args.Length != 2) throw new ArgumentException("Thermostat Command requires 2 arguments");

			// Set the status
			if(_args[1].ToUpper() == "ON") _turnOn = true;
			else if(_args[1].ToUpper() == "OFF") _turnOn = false;
			else throw new ArgumentException("Thermostat Status Command '" + _command + "' Not Currently Handled.");
		}

		public bool TurnOn {
			get { return _turnOn; }
		}
	}

	public class ProgramOverrideArgs : RequestArgs {
		private bool _turnOn;
		private double _setting;

		public ProgramOverrideArgs(char[] Data) : base(Data) {
			// Check that there are 3 args
			if(_args.Length != 3) throw new ArgumentException("Program Override Command requires 3 arguments");

			// Check the args
			if(_args[1].ToUpper() == "ON") {
				_turnOn = true;
				_setting = double.Parse(_args[2]);
			} else if(_args[1].ToUpper() == "OFF") {
				_turnOn = false;
				_setting = 0.0;
			} else throw new ArgumentException("Program Override Command '" + _command + "' Not Currently Handled.");
		}

		public bool TurnOn {
			get { return _turnOn; }
		}

		public double Temperature {
			get { return _setting; }
		}
	}

	public class RuleChangeArgs : RequestArgs {
		public enum Operation { Get, Add, Delete, Move, Update }

		private Operation _operation;
		private byte _pos1;
		private byte _pos2;
		private TemperatureRule _rule;

		public RuleChangeArgs(char[] Data) : base(Data) {
			// The number of args depends on the requested operation
			string CMD = _args[1].ToUpper();
			if(CMD == "GET") {
				_operation = Operation.Get;
				_pos1 = _pos2 = 0;
				_rule = null;
			} else if(CMD == "ADD") {
			} else if(CMD == "DELETE") {
			} else if(CMD == "MOVE") {
			} else if(CMD == "UPDATE") {
			} else throw new ArgumentException("Rule Change Command '" + CMD + "' Not Currently Handled.");
		}

		public Operation ChangeRequested {
			get { return _operation; }
		}

		public byte FirstPosition {
			get { return _pos1; }
		}

		public byte SecondPosition {
			get { return _pos2; }
		}

		public TemperatureRule Rule {
			get { return _rule; }
		}
	}

	public class DataRequestArgs : RequestArgs {
		public DataRequestArgs(char[] Data) : base(Data) { }
	}

	public class TemperatureRule {
		private const int PACKET_LENGTH	= 9;
		private const int FLOAT_LENGTH	= 4;

		public enum DayType { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Weekdays, Weekends, Everyday }

		private DayType _days;
		private float _time;
		private float _temp;
	
		public TemperatureRule(DayType setRuleDay, float setRuleTime, float setRuleTemp) {
			_days = setRuleDay;
			_time = setRuleTime;
			_temp = setRuleTemp;
		}

		public DayType Days {
			get { return _days; }
		}

		public float Time {
			get { return _time; }
		}

		public float Temperature {
			get { return _temp; }
		}

		public TemperatureRule(byte[] packetData) {
			// Confirm the size of the packet
			if(packetData.Length != PACKET_LENGTH) throw new ArgumentException("TemperatureRule trying to instantiate based on incorrect byte packet length.");
			Debug.Assert(sizeof(float) == FLOAT_LENGTH);	// Debuggin check on the hardware

			// Get the float arrays
			byte[] time = new byte[FLOAT_LENGTH];
			byte[] temp = new byte[FLOAT_LENGTH];
			for(int i = 0; i < FLOAT_LENGTH; i++) {
				time[i] = packetData[i + 1];
				temp[i] = packetData[i + FLOAT_LENGTH + 1];
			}

			// Set the values
			_days = (DayType) packetData[0];
			_time = Converters.byteToFloat(time);
			_temp = Converters.byteToFloat(temp);
		}

		public byte[] toByteArray() {
			Debug.Assert(sizeof(float) == FLOAT_LENGTH);	// Debugging check on the hardware

			// Get the byte bits
			byte day = (byte) _days;
			byte[] time = Converters.floatToByte(_time);
			byte[] temp = Converters.floatToByte(_temp);

			// Create the packet
			byte[] packet = new byte[PACKET_LENGTH];
			packet[0] = day;
			for(int i = 0; i < FLOAT_LENGTH; i++) {
				packet[i + 1] = time[i];
				packet[i + FLOAT_LENGTH + 1] = temp[i];
			}

			return packet;
		}
	}
}
