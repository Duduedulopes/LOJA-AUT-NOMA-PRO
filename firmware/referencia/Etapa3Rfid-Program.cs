using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Threading;
using Iot.Device.Card;
using Iot.Device.Mfrc522;
using nanoFramework.Hardware.Esp32;

namespace Etapa3Rfid
{
    /// <summary>
    /// Etapa 3 — validação do leitor RFID RC522.
    ///
    /// Todo o diagnóstico sai pelo LED azul da placa (GPIO2), porque o depurador
    /// do Visual Studio não anexa nesta máquina e o Debug.WriteLine não chega a
    /// lugar nenhum. Ver a tabela de sinais no fim do arquivo.
    /// </summary>
    public class Program
    {
        // Pinagem — ver diagrama/montagem-por-etapas.pdf
        private const int PinSck = 18;    // D18
        private const int PinMosi = 23;   // D23
        private const int PinMiso = 19;   // D19
        private const int PinSs = 21;     // D21 — SDA/SS do RC522
        private const int PinReset = 22;  // D22 — RST do RC522
        private const int PinLed = 2;     // LED azul soldado na placa

        private static GpioPin _led;

        public static void Main()
        {
            var gpio = new GpioController();

            _led = gpio.OpenPin(PinLed, PinMode.Output);
            _led.Write(PinValue.Low);

            // SINAL 1 — três piscadas rápidas: o programa está rodando
            Piscar(3, 120);
            Thread.Sleep(1200);

            try
            {
                // Obrigatório no ESP32: o SPI1 tem pinos padrão diferentes dos nossos.
                // Precisa vir ANTES de criar o SpiDevice.
                Configuration.SetPinFunction(PinSck, DeviceFunction.SPI1_CLOCK);
                Configuration.SetPinFunction(PinMosi, DeviceFunction.SPI1_MOSI);
                Configuration.SetPinFunction(PinMiso, DeviceFunction.SPI1_MISO);

                var connection = new SpiConnectionSettings(1, PinSs);
                connection.ClockFrequency = 5_000_000;

                var spi = SpiDevice.Create(connection);
                var leitor = new MfRc522(spi, PinReset, gpio, false);

                var versao = leitor.Version;

                Debug.WriteLine("Versao do chip: " + versao.Major + "." + versao.Minor);

                // SINAL 2 — piscadas LENTAS contando a versão do chip.
                // 1 ou 2 piscadas = RC522 respondendo. Nenhuma piscada = versão 0.
                Piscar(versao.Major, 600);
                Thread.Sleep(1500);

                if (versao.Major == 0)
                {
                    // SINAL 3 — versão 0: fica piscando rápido para sempre.
                    // Pode ser fio de SPI trocado, ou clone barato que funciona.
                    // Encoste um cartão: se o LED ficar aceso fixo, é clone e está tudo bem.
                    while (true)
                    {
                        if (Ler(leitor))
                        {
                            break;
                        }

                        Piscar(1, 80);
                    }
                }

                // SINAL 4 — regime normal: LED apagado esperando, aceso 2 s a cada cartão.
                while (true)
                {
                    if (Ler(leitor))
                    {
                        _led.Write(PinValue.High);
                        Thread.Sleep(2000);
                        _led.Write(PinValue.Low);
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Excecao: " + ex.Message);

                // SINAL 5 — exceção: pisca em pares, para sempre.
                while (true)
                {
                    Piscar(2, 100);
                    Thread.Sleep(900);
                }
            }
        }

        private static bool Ler(MfRc522 leitor)
        {
            Data106kbpsTypeA cartao;

            bool achou = leitor.ListenToCardIso14443TypeA(out cartao, TimeSpan.FromSeconds(1));

            if (achou)
            {
                Debug.WriteLine("Cartao — UID: " + Uid(cartao.NfcId));
            }

            return achou;
        }

        private static void Piscar(int vezes, int ms)
        {
            for (int i = 0; i < vezes; i++)
            {
                _led.Write(PinValue.High);
                Thread.Sleep(ms);
                _led.Write(PinValue.Low);
                Thread.Sleep(ms);
            }
        }

        private static string Hex(byte valor)
        {
            const string digitos = "0123456789ABCDEF";

            return new string(new[] { digitos[valor >> 4], digitos[valor & 0x0F] });
        }

        private static string Uid(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "(vazio)";
            }

            string texto = Hex(bytes[0]);

            for (int i = 1; i < bytes.Length; i++)
            {
                texto += "-" + Hex(bytes[i]);
            }

            return texto;
        }
    }
}

// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul da placa (GPIO2)
//
// A sequência começa sempre com 3 piscadas rápidas. O que vem depois é o
// resultado do teste.
//
// 3 rápidas, pausa, 2 LENTAS ..... versão 2.0 — RC522 perfeito. Etapa 3 validada.
// 3 rápidas, pausa, 1 LENTA ...... versão 1.0 — RC522 perfeito também.
// 3 rápidas, pausa, nada ......... versão 0.0 — segue piscando rápido sem parar.
//                                  Encoste um cartão: se acender fixo, é clone
//                                  barato e funciona. Se não, há fio de SPI trocado.
// 3 rápidas, depois PARES de
// piscadas sem parar ............. exceção. O SPI não pôde ser criado.
//                                  Confira o SetPinFunction e os pinos 18/23/19.
// nada, LED morto ................ o programa não executa. Reconectar o USB —
//                                  com o depurador falhando, o CLR fica parado
//                                  esperando ele.
//
// Depois do teste, em regime normal: LED apagado esperando cartão, aceso 2 s
// a cada leitura.
//
// Se vier versão 0.0, a ordem de suspeita nos fios é:
//   1. SDA/SS e RST invertidos (D21 e D22 são vizinhos — erro mais comum)
//   2. MISO fora do D19
//   3. SCK fora do D18 ou MOSI fora do D23
// ---------------------------------------------------------------------------
