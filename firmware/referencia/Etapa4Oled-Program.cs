using System;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using System.Threading;
using Iot.Device.Ssd13xx;
using nanoFramework.Hardware.Esp32;

namespace Etapa4Oled
{
    /// <summary>
    /// Etapa 4 — validação do display OLED SSD1306 no I2C.
    ///
    /// Não usa texto de propósito: a classe BasicFont não vem no pacote NuGet,
    /// só no projeto de exemplo da biblioteca. Para validar a montagem basta
    /// acender e apagar a tela, que já prova que o I2C está falando com o display.
    ///
    /// Diagnóstico pelo LED azul da placa (GPIO2), porque o depurador não anexa.
    /// </summary>
    public class Program
    {
        // Pinagem — ver diagrama/montagem-por-etapas.pdf
        // Atenção: 4 e 5, não 21 e 22 dos exemplos da internet.
        // O 21 e o 22 foram para o RC522.
        private const int PinSda = 4;   // D4  — SDA do OLED
        private const int PinScl = 5;   // D5  — SCL do OLED
        private const int PinLed = 2;   // LED azul da placa

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
                // Obrigatório no ESP32: o I2C1 tem pinos padrão diferentes dos nossos.
                // Precisa vir ANTES de criar o I2cDevice.
                Configuration.SetPinFunction(PinSda, DeviceFunction.I2C1_DATA);
                Configuration.SetPinFunction(PinScl, DeviceFunction.I2C1_CLOCK);

                var settings = new I2cConnectionSettings(1, Ssd1306.DefaultI2cAddress);
                var i2c = I2cDevice.Create(settings);

                var tela = new Ssd1306(i2c, Ssd13xx.DisplayResolution.OLED128x64);

                tela.ClearScreen();
                tela.Display();

                Debug.WriteLine("OLED inicializado no endereco 0x3C.");

                // SINAL 2 — duas piscadas lentas: o display respondeu no I2C
                Piscar(2, 600);
                Thread.Sleep(1000);

                // Regime normal: pisca a tela inteira, um segundo acesa, um apagada.
                while (true)
                {
                    tela.DrawFilledRectangle(0, 0, 128, 64, true);
                    tela.Display();
                    Thread.Sleep(1000);

                    tela.ClearScreen();
                    tela.Display();
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Excecao: " + ex.Message);

                // SINAL 3 — exceção: pisca em pares, para sempre.
                while (true)
                {
                    Piscar(2, 100);
                    Thread.Sleep(900);
                }
            }
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
    }
}

// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul da placa (GPIO2)
//
// 3 rápidas, pausa, 2 LENTAS ..... OLED respondeu no I2C. A tela deve começar
//                                  a piscar inteira, 1 s acesa e 1 s apagada.
//
// 3 rápidas, depois PARES de
// piscadas sem parar ............. exceção. O display não respondeu no
//                                  endereço 0x3C. Nesta ordem:
//                                  1. VCC do OLED no trilho +, GND no trilho −
//                                  2. SDA no D4 (coluna 5) e SCL no D5 (coluna 8)
//                                     — trocar esses dois é o erro mais comum
//                                  3. alguns módulos usam 0x3D em vez de 0x3C:
//                                     trocar DefaultI2cAddress por
//                                     SecondaryI2cAddress e testar
//
// LED sinaliza OK mas a tela
// fica escura .................... I2C funcionando e display não. Provavelmente
//                                  a resolução está errada: trocar OLED128x64
//                                  por OLED128x32 e testar.
//
// nada, LED morto ................ o programa não executa. Reconectar o USB.
//
// NOTA: se `DrawFilledRectangle` não compilar, apague as duas linhas dele e as
// duas do ClearScreen dentro do while. O SINAL 2 já valida o I2C sozinho —
// o pisca-pisca da tela é confirmação visual extra, não é essencial.
// ---------------------------------------------------------------------------
