using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using nanoFramework.Networking;

namespace Etapa7Api
{
    /// <summary>
    /// Etapa 7 — a ESP32 conversando com a AutonomousStore.WebApi.
    ///
    /// Conecta no Wi-Fi e faz um GET em /api/categories, que é anônimo.
    /// O objetivo é só provar que os dois se falam pela rede.
    /// Diagnóstico pelo LED azul da placa (GPIO2).
    /// </summary>
    public class Program
    {
        // ⚠️ PREENCHER — rede de 2,4 GHz
        private const string Ssid = "COLOQUE_O_NOME_DA_SUA_REDE";
        private const string Senha = "COLOQUE_A_SENHA";

        // ⚠️ PREENCHER com o IP do PC na rede local.
        // Descobrir com:  ipconfig | Select-String "IPv4"
        // Sempre HTTP e porta 5071 — o perfil http-rede (ESP32) do launchSettings.
        // NUNCA https: o certificado de desenvolvimento é autoassinado e a placa recusa.
        private const string Url = "http://192.168.0.XXX:5071/api/categories";

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

            // ---------- Wi-Fi ----------
            Debug.WriteLine("Conectando em: " + Ssid);

            var cs = new CancellationTokenSource(60000);

            bool conectou = WifiNetworkHelper.ConnectDhcp(
                Ssid,
                Senha,
                requiresDateTime: false,
                token: cs.Token);

            if (!conectou)
            {
                Debug.WriteLine("Wi-Fi FALHOU. Status: " + WifiNetworkHelper.Status);
                Falhar(2);   // pares de piscadas
            }

            Debug.WriteLine("Wi-Fi conectado.");

            // SINAL 2 — duas piscadas lentas: Wi-Fi pronto
            Piscar(2, 600);
            Thread.Sleep(800);

            // ---------- API ----------
            Debug.WriteLine("GET " + Url);

            try
            {
                using (var client = new HttpClient())
                {
                    using (var resposta = client.Get(Url))
                    {
                        Debug.WriteLine("Status: " + resposta.StatusCode);

                        if (resposta.StatusCode != HttpStatusCode.OK)
                        {
                            Falhar(3);   // trios de piscadas
                        }

                        string corpo = resposta.Content.ReadAsString();

                        Debug.WriteLine("Resposta: " + corpo);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Excecao no GET: " + ex.Message);
                Falhar(3);   // trios de piscadas
            }

            // SINAL 3 — SUCESSO: LED aceso fixo, para sempre.
            Debug.WriteLine("SUCESSO — a placa falou com a API.");

            _led.Write(PinValue.High);

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>Pisca em grupos de N para sempre. Nunca retorna.</summary>
        private static void Falhar(int grupo)
        {
            while (true)
            {
                Piscar(grupo, 100);
                Thread.Sleep(1200);
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
// ANTES DE RODAR — três coisas no PC
//
// 1. Descobrir o IP da máquina e colocar na constante Url acima:
//       ipconfig | Select-String "IPv4"
//
// 2. Liberar a porta no firewall (PowerShell como ADMINISTRADOR, uma vez só):
//       New-NetFirewallRule -DisplayName "AutonomousStore API 5071" `
//         -Direction Inbound -Protocol TCP -LocalPort 5071 -Action Allow -Profile Private
//
// 3. Rodar a API com o perfil **http-rede (ESP32)**, no dropdown ao lado do play.
//    Os perfis http e https escutam só em localhost e a placa não alcança.
//
// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul da placa (GPIO2)
//
// 3 rápidas, 2 lentas, ACESO FIXO ... a placa falou com a API e recebeu 200.
//                                     Etapa 7 validada.
//
// PARES de piscadas em ciclo ........ Wi-Fi não conectou. Ver a tabela da Etapa 6.
//
// TRIOS de piscadas em ciclo ........ Wi-Fi ok, API não respondeu. Na ordem:
//                                     1. A API não está rodando, ou está rodando
//                                        no perfil errado (http/https, só localhost)
//                                     2. IP errado na constante Url — o IP do PC
//                                        muda quando ele reconecta na rede
//                                     3. Firewall bloqueando a porta 5071. Se o
//                                        Windows classificou sua Wi-Fi como
//                                        "pública", a regra com -Profile Private
//                                        não pega
//                                     4. PC e placa em redes diferentes — comum
//                                        quando o roteador tem 2,4 e 5 GHz
//                                        separados e o PC está no de 5 GHz.
//                                        Não impede se estiverem na mesma LAN,
//                                        mas impede se houver isolamento de
//                                        cliente ("AP isolation") ligado
//                                     5. Usou https por engano
//
// TESTE INTERMEDIÁRIO que economiza tempo: abra no navegador do CELULAR, conectado
// na mesma Wi-Fi, o endereço http://<IP-DO-PC>:5071/api/categories
// Se o celular vê e a placa não, o problema é a placa. Se o celular também não vê,
// é firewall, perfil da API ou IP errado — e não vale mexer no firmware.
// ---------------------------------------------------------------------------
