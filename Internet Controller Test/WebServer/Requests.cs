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
		private double _length;

		public ProgramOverrideArgs(char[] Data) : base(Data) {
			// Check the args
			if((_args[1].ToUpper() == "ON") && (_args.Length == 4)) {
				_turnOn = true;
				_setting = double.Parse(_args[2]);
				_length = double.Parse(_args[3]);
			} else if((_args[1].ToUpper() == "OFF") && (_args.Length == 2)) {
				_turnOn = false;
				_setting = 0.0;
				_length = -1.0;
			} else throw new ArgumentException("Vacation Status Command '" + _command + "' Not Currently Handled.");
		}

		public bool TurnOn {
			get { return _turnOn; }
		}

		public double Temperature {
			get { return _setting; }
		}
	}

	public class RuleChangeArgs : RequestArgs {
		public RuleChangeArgs(char[] Data) : base(Data) { }
	}

	public class DataRequestArgs : RequestArgs {
		public DataRequestArgs(char[] Data) : base(Data) { }
	}
}
