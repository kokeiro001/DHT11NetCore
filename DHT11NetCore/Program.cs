using System;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;

namespace DHT11NetCore
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var pin = Pi.Gpio.GetGpioPinByBcmPinNumber(20);
            var dht11 = new DHT11(pin);

            while (true)
            {
                var result = await dht11.Read();

                switch (result.Error)
                {
                    case DHT11ReadErrorState.None:
                        Console.WriteLine($"Temperature={result.Temperature}, Humidity={result.Humidity}");
                        break;
                    case DHT11ReadErrorState.MissingData:
                        Console.WriteLine($"Error MissingData");
                        break;
                    case DHT11ReadErrorState.CRC:
                        Console.WriteLine($"Error CRC");
                        break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
