using System;
using Microsoft.SPOT;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;

namespace InternetControllerTest {

	public delegate void RequestReceivedHandler(Socket client, RequestArgs request);

	public class Listener : IDisposable {
		// Local constants
		const int maxRequestSize = 1024;

		// Members
		readonly int portNumber;
		private Socket listeningSocket = null;
		private IPEndPoint _client;

		// Events
		public event RequestReceivedHandler thermoStatusChanged;
		public event RequestReceivedHandler programOverrideRequested;
		public event RequestReceivedHandler thermoRuleChanged;
		public event RequestReceivedHandler dataRequested;

		// Constructor
		public Listener(int PortNumber) {
			// Setup the socket and initialize it for listening
			portNumber = PortNumber;
			listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			listeningSocket.Bind(new IPEndPoint(IPAddress.Any, portNumber));
			listeningSocket.Listen(10);

			// Listen for connections in another thread
			new Thread(StartListening).Start();
		}

		// Destructor
		~Listener() {
			Dispose();
		}

		// Parmeters
		public IPEndPoint ClientIP {
			get { return _client; }
		}

		// Listening thread
		public void StartListening() {
			// Infinite loop looking for connections
			while(true) {
				using(Socket clientSocket = listeningSocket.Accept()) {
					// Get the client IP
					_client = clientSocket.RemoteEndPoint as IPEndPoint;
					Debug.Print("Received request from " + _client.ToString());

					// Determine the size of the transmission
					int availableBytes = clientSocket.Available;
					int bytesReceived = (availableBytes > maxRequestSize ? maxRequestSize : availableBytes);
					Debug.Print(DateTime.Now.ToString() + " " + availableBytes.ToString() + " request bytes available; " + bytesReceived + " bytes to try and receive.");

					// Process the request
					if(bytesReceived > 2) {
						byte[] buffer = new byte[bytesReceived];
						int readByteCount = clientSocket.Receive(buffer, bytesReceived, SocketFlags.None);
						Debug.Print("Read " + readByteCount + " bytes from the client socket.");

						// Get the first two characters, and check the codes
						string code = new string(Encoding.UTF8.GetChars(buffer, 0, 2));
						char[] cmd = Encoding.UTF8.GetChars(buffer);
						if(code == "TS") {	// Thermo status command
							if(thermoStatusChanged != null) thermoStatusChanged(clientSocket, new ThermStatusArgs(cmd));
						} else if(code == "PO") {	// Program override command
							if(programOverrideRequested != null) programOverrideRequested(clientSocket, new ProgramOverrideArgs(cmd));
						} else if(code == "TR") {	// Thermo rule command
							if(thermoRuleChanged != null) thermoRuleChanged(clientSocket, new RuleChangeArgs(cmd));
						} else if(code == "DR") {	// Data request
							if(dataRequested != null) dataRequested(clientSocket, new DataRequestArgs(cmd));
						}
					} else Debug.Print("Number of identified bytes is less than needed for this hardware.");
				}
				Thread.Sleep(10);	// Provide some delay to help prevent lock-ups
			}
		}

		#region IDisposable Members
		/// <summary>
		/// Closes the listening socket
		/// </summary>
		public void Dispose() {
			if(listeningSocket != null) listeningSocket.Close();

		}
		#endregion
	}
}
