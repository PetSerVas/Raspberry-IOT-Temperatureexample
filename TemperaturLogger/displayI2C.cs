using System;
using System.Threading.Tasks;
using Windows.Devices.I2c;

namespace DisplayI2C
{
	class displayI2C
	{

		private const byte LCD_WRITE = 0x07;

		// Pinbelegung des I2C Chips auf dem LCD Display (IC PCF8574)
		private const byte _D4 = 4;
		private const byte _D5 = 5;
		private const byte _D6 = 6;
		private const byte _D7 = 7;
		private const byte _En = 2;
		private const byte _Rw = 1;
		private const byte _Rs = 0;
		private const byte _Bl = 3;
		private byte _backLight = 0x01;
		private I2cDevice LCDDevice;
		private byte[] _LineAddress = new byte[] { 0x00, 0x40 };



		public displayI2C(I2cDevice Device) 
		{
			this.LCDDevice = Device;
		}
               
		// Initialization
		public void init(	bool turnOnDisplay = true, 
						bool turnOnCursor = false, 
						bool blinkCursor = false, 
						bool cursorDirection = true, 
						bool textShift = false)
		{
			Task.Delay(100).Wait();
			pulseEnable(Convert.ToByte((1 << _D5) | (1 << _D4)));
			Task.Delay(5).Wait();
			pulseEnable(Convert.ToByte((1 << _D5) | (1 << _D4)));
			Task.Delay(5).Wait();
			pulseEnable(Convert.ToByte((1 << _D5) | (1 << _D4)));

			//  Init 4-bit mode
			pulseEnable(Convert.ToByte((1 << _D5)));

			/* Init 4-bit mode + 2 line */
			pulseEnable(Convert.ToByte((1 << _D5)));
			pulseEnable(Convert.ToByte((1 << _D7)));

			/* Turn on display, cursor */
			pulseEnable(0);
			pulseEnable(Convert.ToByte((1 << _D7) | 
				(Convert.ToByte(turnOnDisplay) << _D6) | 
				(Convert.ToByte(turnOnCursor) << _D5) | 
				(Convert.ToByte(blinkCursor) << _D4)));

			clrscr();

			pulseEnable(0);
			pulseEnable(Convert.ToByte((1 << _D6) | 
				(Convert.ToByte(cursorDirection) << _D5) | 
				(Convert.ToByte(textShift) << _D4)));
		}

		// backlight ON
		public void turnOnBacklight()
		{
			_backLight = 0x01;
			sendCommand(0x00);
		}

		// Turn the backlight OFF.
		public void turnOffBacklight()
		{
			_backLight = 0x00;
			sendCommand(0x01);
		}

		// print string on display
		public void prints(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				printc(text[i]);
			}
		}

		// Print single character on display
		public void printc(char letter)
		{
			try
			{
				write(Convert.ToByte(letter), 1);
			}
			catch (Exception e)
			{
			}
		}

		// go to second line
		public void gotoSecondLine()
		{
			sendCommand(0xc0);
		}

		/**
		* goto X and Y 
		**/
		public void gotoxy(byte x, byte y)
		{
			sendCommand( Convert.ToByte(
				x | 
				_LineAddress[y] | 
				( 1 << LCD_WRITE) ) 
				);
		}

		// Send data to display
		public void sendData(byte data)
		{
			write(data, 1);
		}

		// Send command to display
		public void sendCommand(byte data)
		{
			write(data, 0);
		}

		// Clear display and set cursor to home position (Line 1, Col 1)
		public void clrscr()
		{
			pulseEnable(0);
			pulseEnable(Convert.ToByte((1 << _D4)));
			Task.Delay(5).Wait();
		}

		// Send pure data to display
		public void write(byte data, byte Rs)
		{
			pulseEnable(Convert.ToByte((data & 0xf0) | (Rs << _Rs)));
			pulseEnable(Convert.ToByte((data & 0x0f) << 4 | (Rs << _Rs)));
			//Task.Delay(5).Wait(); //In case of problem with displaying wrong characters uncomment this part
		}

		// Create falling edge of "enable" pin to 
		// set display to data or instruction receipt  
		private void pulseEnable(byte data)
		{
			if (LCDDevice == null)
				return;

			this.LCDDevice.Write(new byte[] {
				Convert.ToByte(data | (1 << _En) | 
				(_backLight << _Bl)) }); // Enable HIGH bit
			this.LCDDevice.Write(new byte[] {
				Convert.ToByte(data | 
				(_backLight << _Bl)) }); // Enable LOW bit
			//Task.Delay(2).Wait(); //In case of problem with displaying wrong characters uncomment this part
		}

		// Save custom symbol to CGRAM
		public void createSymbol(byte[] data, byte address)
		{
			sendCommand( Convert.ToByte(0x40 | (address << 3)));
			for(int i = 0; i < data.Length; i++)
			{
				sendData(data[i]);
			}
			clrscr();
		}

		// Print custom symbol
		public void printSymbol(byte address)
		{
			sendData(address);
		}
    }
}