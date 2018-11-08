using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unosquare.RaspberryIO.Gpio;

namespace DHT11NetCore
{
    enum DHT11ReadErrorState
    {
        None,
        MissingData,
        CRC,
    }

    class DHT11ReadResult
    {
        public DHT11ReadErrorState Error { get; set; }
        public int Temperature { get; set; }
        public int Humidity { get; set; }
    }

    class DHT11
    {
        private readonly GpioPin pin;

        public DHT11(GpioPin pin)
        {
            this.pin = pin;
        }

        public async Task<DHT11ReadResult> Read()
        {
            pin.PinMode = GpioPinDriveMode.Output;
            await pin.WriteAsync(GpioPinValue.High);
            await Task.Delay(TimeSpan.FromMilliseconds(25));

            await pin.WriteAsync(GpioPinValue.Low);
            await Task.Delay(TimeSpan.FromMilliseconds(11));

            pin.PinMode = GpioPinDriveMode.Input;
            pin.InputPullMode = GpioPinResistorPullMode.PullUp;

            var data = CollectInput();

            var pullUpLengths = ParseDataPullUpLengths(data);

            if (pullUpLengths.Length != 40)
            {
                return new DHT11ReadResult
                {
                    Error = DHT11ReadErrorState.MissingData,
                };
            }

            var bits = CalculateBits(pullUpLengths);

            var result = BitsToBytes(bits);

            var checksum = CalculateChecksum(result);
            if (result[4] != checksum)
            {
                return new DHT11ReadResult
                {
                    Error = DHT11ReadErrorState.CRC
                };
            }
            return new DHT11ReadResult
            {
                Temperature = result[2],
                Humidity = result[0],
                Error = DHT11ReadErrorState.None
            };
        }


        private GpioPinValue[] CollectInput()
        {
            var unchangedCount = 0;
            var maxUnchangedCount = 100;

            var last = default(GpioPinValue?);
            var data = new List<GpioPinValue>(maxUnchangedCount * 2);
            while (true)
            {
                var current = pin.ReadValue();
                data.Add(current);

                if (last != current)
                {
                    unchangedCount = 0;
                    last = current;
                }
                else
                {
                    unchangedCount++;
                    if (unchangedCount > maxUnchangedCount)
                    {
                        break;
                    }
                }
            }

            return data.ToArray();
        }

        private int[] ParseDataPullUpLengths(GpioPinValue[] data)
        {
            var state = SignalState.InitPullDown;

            var lengths = new List<int>();
            var currentLength = 0;

            for (int i = 0; i < data.Length; i++)
            {
                var current = data[i];
                currentLength++;
                switch (state)
                {
                    case SignalState.InitPullDown:
                        if (current == GpioPinValue.Low)
                        {
                            state = SignalState.InitPullUp;
                        }
                        break;
                    case SignalState.InitPullUp:
                        if (current == GpioPinValue.High)
                        {
                            state = SignalState.DataFirstPullDown;
                        }
                        break;
                    case SignalState.DataFirstPullDown:
                        if (current == GpioPinValue.Low)
                        {
                            currentLength = 0;
                            state = SignalState.DataPullUp;
                        }
                        break;
                    case SignalState.DataPullUp:
                        if (current == GpioPinValue.High)
                        {
                            currentLength = 0;
                            state = SignalState.DataPullDown;
                        }
                        break;
                    case SignalState.DataPullDown:
                        if (current == GpioPinValue.Low)
                        {
                            lengths.Add(currentLength);
                            state = SignalState.DataPullUp;
                        }
                        break;
                    default:
                        throw new InvalidOperationException(nameof(state));
                }
            }

            return lengths.ToArray();
        }

        private bool[] CalculateBits(int[] pullUpLengths)
        {
            var shortestPullUp = pullUpLengths.Min();
            var longestPullUp = pullUpLengths.Max();

            var halfway = shortestPullUp + (longestPullUp - shortestPullUp) / 2;
            var bits = new bool[pullUpLengths.Length];

            for (int i = 0; i < pullUpLengths.Length; i++)
            {
                bits[i] = pullUpLengths[i] > halfway;
            }

            return bits;
        }

        private byte[] BitsToBytes(bool[] bits)
        {
            if (bits.Length % 8 != 0)
            {
                throw new ArgumentException(nameof(bits));
            }

            if (BitConverter.IsLittleEndian)
            {
                bits = bits.Reverse().ToArray();
            }

            var bitArray = new BitArray(bits);
            var byteArray = new byte[bits.Length / 8];
            bitArray.CopyTo(byteArray, 0);

            if (BitConverter.IsLittleEndian)
            {
                byteArray = byteArray.Reverse().ToArray();
            }
            return byteArray;
        }

        private byte CalculateChecksum(byte[] theBytes)
        {
            return (byte)(theBytes[0] + theBytes[1] + theBytes[2] + theBytes[3] & 255);
        }

        private enum SignalState
        {
            InitPullDown,
            InitPullUp,
            DataFirstPullDown,
            DataPullUp,
            DataPullDown,
        }
    }
}
