using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;
using nanoFramework.Networking;

namespace Etapa6Wifi
{
    /// <summary>
    /// Etapa 6 — conectar a ESP32 na rede Wi-Fi.
    ///
    /// Só isso. Não fala com a API ainda — o próximo programa faz isso.
    /// Diagnóstico pelo LED azul da placa (GPIO2).
    /// </summary>
    public class Program
    {
        // Credenciais em Segredos.cs, que está no .gitignore.
        // Se não compilar por falta da classe Segredos, copie o Segredos.exemplo.cs
        // para Segredos.cs e preencha.
        //
        // Lembrete: a ESP32 só enxerga redes de 2,4 GHz. Se o roteador usa o mesmo
        // nome para 2,4 e 5 GHz, pode ser preciso separar os nomes na configuração
        // dele, ou criar uma rede de convidados em 2,4 GHz para os dispositivos IoT.
        private const string Ssid = Segredos.Ssid;
        private const string Senha = Segredos.SenhaWifi;

        private const int PinLed = 2;

        private static GpioPin _led;

        public static void Main()
        {
            var gpio = new GpioController();

            _led = gpio.OpenPin(PinLed, PinMode.Output);
            _led.Write(PinValue.Low);

            // SINAL 1 — três piscadas rápidas: o programa está rodando
            Piscar(3, 120);
            Thread.Sleep(1000);

            Debug.WriteLine("=== Etapa 6 — Wi-Fi ===");
            Debug.WriteLine("Conectando em: " + Ssid);

            // 60 segundos para a conexão acontecer.
            // requiresDateTime: false — não exige sincronizar a hora por SNTP.
            // Com true, uma rede sem internet faria a conexão "falhar" mesmo
            // tendo IP, e isso confunde o diagnóstico. A hora só vai importar
            // quando entrarmos em HTTPS.
            var cs = new CancellationTokenSource(60000);

            bool conectou = WifiNetworkHelper.ConnectDhcp(
                Ssid,
                Senha,
                requiresDateTime: false,
                token: cs.Token);

            if (!conectou)
            {
                Debug.WriteLine("FALHOU. Status: " + WifiNetworkHelper.Status);

                if (WifiNetworkHelper.HelperException != null)
                {
                    Debug.WriteLine("Excecao: " + WifiNetworkHelper.HelperException.Message);
                }

                // SINAL 3 — falha: pares de piscadas, para sempre
                while (true)
                {
                    Piscar(2, 100);
                    Thread.Sleep(900);
                }
            }

            Debug.WriteLine("CONECTADO.");

            // SINAL 2 — conectado: duas piscadas lentas e o LED fica ACESO FIXO.
            // Aceso fixo é o sinal de sucesso — inconfundível.
            Piscar(2, 600);
            Thread.Sleep(500);

            _led.Write(PinValue.High);

            Thread.Sleep(Timeout.Infinite);
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
// 3 rápidas, 2 lentas, e o LED
// fica ACESO FIXO ................ conectado na rede com IP. Etapa 6 validada.
//                                  Pode levar até 15 segundos.
//
// 3 rápidas, depois PARES de
// piscadas sem parar ............. não conectou. Na ordem de suspeita:
//
//                                  1. A REDE É 5 GHz. A ESP32 é 2,4 GHz e não
//                                     enxerga redes de 5 GHz. Se o roteador usa
//                                     o mesmo nome para as duas faixas, ele pode
//                                     estar entregando a de 5 GHz. Solução:
//                                     separar os nomes, ou criar uma rede de
//                                     convidados em 2,4 GHz.
//                                  2. SSID ou senha digitados errado — inclusive
//                                     maiúsculas e minúsculas, que contam.
//                                  3. Caractere especial ou acento no nome da rede.
//                                  4. Rede com portal de login (cativo) — não
//                                     funciona, a placa não tem navegador.
//                                  5. Filtro de MAC no roteador. O MAC desta
//                                     placa é 70:4B:CA:6D:E0:08.
//
// nada, LED morto ................ o programa não executa. Reconectar o USB.
//
// COMO CONFERIR POR FORA: entre na administração do roteador e procure a lista de
// dispositivos conectados. A ESP32 aparece com o MAC 70:4B:CA:6D:E0:08.
// Não precisamos do IP dela — quem inicia a conversa é sempre a placa.
// ---------------------------------------------------------------------------