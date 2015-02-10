using System;
using Microsoft.SPOT;

namespace InternetControllerTest {
	class Converters {
		public static unsafe float byteToFloat(byte[] byte_array) {
			uint ret = (uint) (byte_array[0] << 0 | byte_array[1] << 8 | byte_array[2] << 16 | byte_array[3] << 24);
			float r = *((float*) &ret);	// This should work?
			return r;
		}

		public static unsafe byte[] floatToByte(float value) {
			if(sizeof(uint) != 4) throw new Exception("uint is not a 4-byte variable on this system!");

			uint asInt = *((uint*) &value);
			byte[] byte_array = new byte[sizeof(uint)];

			byte_array[0] = (byte) (asInt & 0xFF);
			byte_array[1] = (byte) ((asInt >> 8) & 0xFF);
			byte_array[2] = (byte) ((asInt >> 16) & 0xFF);
			byte_array[3] = (byte) ((asInt >> 24) & 0xFF);

			return byte_array;
		}
	}
}
