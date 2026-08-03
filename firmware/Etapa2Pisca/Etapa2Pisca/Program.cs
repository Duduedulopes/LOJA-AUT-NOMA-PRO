using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace Etapa2Pisca
{
    public class Program
    {
        // Pinagem do projeto — ver PROXIMOS-PASSOS.md
        private const int LedProtoboard = 17;  // TX2, através do resistor
        private const int LedPlaca = 2;        // LED azul soldado na DevKit V1

        public static void Main()
        {
            var gpio = new GpioController();

            var ledProto = gpio.OpenPin(LedProtoboard, PinMode.Output);
            var ledPlaca = gpio.OpenPin(LedPlaca, PinMode.Output);

            Debug.WriteLine("Etapa 2 iniciada. Os dois LEDs devem piscar juntos.");

            var estado = PinValue.Low;

            while (true)
            {
                estado = estado == PinValue.High ? PinValue.Low : PinValue.High;

                ledProto.Write(estado);
                ledPlaca.Write(estado);

                Debug.WriteLine(estado == PinValue.High ? "LED ligado" : "LED apagado");

                Thread.Sleep(500);
            }
        }
    }
}