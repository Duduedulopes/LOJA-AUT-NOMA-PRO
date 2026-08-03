using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace Etapa5Pir
{
    /// <summary>
    /// Etapa 5 — validação do sensor de presença PIR HC-SR501.
    ///
    /// O mais simples de todos: só GPIO, sem barramento e sem biblioteca.
    /// O LED azul da placa acompanha o sensor — aceso enquanto houver movimento.
    /// </summary>
    public class Program
    {
        // Pinagem — ver diagrama/montagem-por-etapas.pdf
        private const int PinPir = 16;  // RX2 (GPIO16) — OUT do PIR
        private const int PinLed = 2;   // LED azul da placa

        public static void Main()
        {
            var gpio = new GpioController();

            var led = gpio.OpenPin(PinLed, PinMode.Output);
            led.Write(PinValue.Low);

            // SINAL 1 — três piscadas rápidas: o programa está rodando
            for (int i = 0; i < 3; i++)
            {
                led.Write(PinValue.High);
                Thread.Sleep(120);
                led.Write(PinValue.Low);
                Thread.Sleep(120);
            }

            // O HC-SR501 precisa de até um minuto para estabilizar depois de energizar.
            // Nesse período ele dispara sozinho — não é defeito.
            Thread.Sleep(2000);

            var pir = gpio.OpenPin(PinPir, PinMode.Input);

            Debug.WriteLine("=== Etapa 5 — PIR ===");
            Debug.WriteLine("Aguarde ate 1 minuto de estabilizacao antes de confiar na leitura.");

            var anterior = PinValue.Low;

            while (true)
            {
                var agora = pir.Read();

                if (agora != anterior)
                {
                    anterior = agora;

                    led.Write(agora);

                    Debug.WriteLine(agora == PinValue.High ? "MOVIMENTO" : "parado");
                }

                Thread.Sleep(50);
            }
        }
    }
}

// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul da placa (GPIO2)
//
// 3 piscadas e depois o LED
// segue a sua mão .............. PIR funcionando. Etapa 5 validada, montagem
//                                completa.
//
// LED nunca acende ............. o PIR não dispara. A causa mais provável é
//                                tensão: o HC-SR501 foi projetado para 4,5 V
//                                ou mais e aqui está em 3,3 V.
//                                CORREÇÃO: tire o VCC dele do trilho + e ligue
//                                no pino VIN do ESP32, que entrega os 5 V do USB.
//                                A saída OUT continua segura para a placa.
//
// LED aceso sempre ............. ou o sensor ainda está estabilizando (espere
//                                um minuto inteiro), ou os potenciômetros de
//                                sensibilidade e tempo estão no máximo. São os
//                                dois parafusinhos laranja no módulo — gire
//                                ambos para o mínimo e teste de novo.
//
// LED pisca sozinho sem
// ninguém por perto ............ normal no primeiro minuto. Se continuar depois
//                                disso, reduza a sensibilidade no potenciômetro.
//
// 3 piscadas e nada mais ....... o programa rodou mas o pino não muda. Confira
//                                o OUT na coluna 6, fileira J — RX2 (GPIO16).
// ---------------------------------------------------------------------------
