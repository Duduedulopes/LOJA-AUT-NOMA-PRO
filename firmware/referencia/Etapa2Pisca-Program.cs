using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace Etapa2Pisca
{
    /// <summary>
    /// Etapa 2 — validação da montagem.
    /// Faz piscar o LED da protoboard (anodo em GPIO17 / TX2) junto com o LED
    /// azul embutido na placa (GPIO2), meio segundo cada.
    /// </summary>
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

// ---------------------------------------------------------------------------
// DIAGNÓSTICO
//
// Os dois piscam juntos ....... montagem da Etapa 2 correta, pode seguir.
//
// Só o azul da placa pisca .... o problema está na protoboard. Verifique, nesta
//                               ordem: (1) o anodo do LED (perna comprida) está
//                               no GPIO17; (2) o catodo passa pelo resistor até
//                               o trilho GND; (3) o LED não está invertido.
//
// Só o da protoboard pisca .... normal em algumas placas DevKit V1 que não têm
//                               o LED em GPIO2. Não é erro.
//
// Nenhum pisca, sem saída ..... o código não subiu. Confirme no Device Explorer
//                               do Visual Studio se a placa aparece em COM6.
//
// Nenhum pisca, mas a janela
// de Saída mostra as mensagens  o firmware está rodando e o GPIO não responde.
//                               Confira se o GPIO17 não ficou preso em outra
//                               função — na DevKit V1 ele é livre.
// ---------------------------------------------------------------------------
