using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Iot.Device.Card;
using Iot.Device.Mfrc522;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Networking;

namespace Etapa8Saida
{
    /// <summary>
    /// Etapa 8 — o leitor de saída do MVP.
    ///
    /// Fluxo: passa o produto pelo RC522, a tag vai para a API, a API resolve qual
    /// produto é, adiciona na sessão aberta e baixa o estoque.
    ///
    /// Usa dois endpoints, ambos anônimos:
    ///   GET  /api/sessions/current-open
    ///   POST /api/sessions/{id}/items/by-rfid   body: { "rfidTag": "..." }
    ///
    /// Diagnóstico pelo LED azul da placa (GPIO2). Ver tabela no fim do arquivo.
    /// </summary>
    public class Program
    {
        // ⚠️ PREENCHER — rede de 2,4 GHz
        private const string Ssid = "COLOQUE_O_NOME_DA_SUA_REDE";
        private const string Senha = "COLOQUE_A_SENHA";

        // ⚠️ PREENCHER — IP do PC na rede. Muda quando o hotspot reconecta.
        //    ipconfig | Select-String "IPv4"
        private const string BaseUrl = "http://172.20.10.8:5071";

        // Pinagem do RC522 — ver a tabela no PROXIMOS-PASSOS.md
        private const int PinSck = 18;
        private const int PinMosi = 23;
        private const int PinMiso = 19;
        private const int PinSs = 21;
        private const int PinReset = 22;
        private const int PinLed = 2;

        private static GpioPin _led;
        private static HttpClient _http;

        public static void Main()
        {
            var gpio = new GpioController();

            _led = gpio.OpenPin(PinLed, PinMode.Output);
            _led.Write(PinValue.Low);

            Piscar(3, 120);
            Thread.Sleep(1000);

            // ---------- 1. Wi-Fi ----------
            var cs = new CancellationTokenSource(60000);

            if (!WifiNetworkHelper.ConnectDhcp(Ssid, Senha, requiresDateTime: false, token: cs.Token))
            {
                Debug.WriteLine("Wi-Fi FALHOU: " + WifiNetworkHelper.Status);
                Falhar(2);
            }

            Debug.WriteLine("Wi-Fi conectado.");
            Piscar(2, 600);
            Thread.Sleep(600);

            _http = new HttpClient();

            // ---------- 2. Sessão aberta ----------
            string sessaoId = BuscarSessaoAberta();

            if (sessaoId == null)
            {
                Debug.WriteLine("Nenhuma sessao aberta.");
                Falhar(4);
            }

            Debug.WriteLine("Sessao: " + sessaoId);
            Piscar(3, 600);
            Thread.Sleep(600);

            // ---------- 3. Leitor ----------
            Configuration.SetPinFunction(PinSck, DeviceFunction.SPI1_CLOCK);
            Configuration.SetPinFunction(PinMosi, DeviceFunction.SPI1_MOSI);
            Configuration.SetPinFunction(PinMiso, DeviceFunction.SPI1_MISO);

            var connection = new SpiConnectionSettings(1, PinSs);
            connection.ClockFrequency = 5_000_000;

            var leitor = new MfRc522(SpiDevice.Create(connection), PinReset, gpio, false);

            Debug.WriteLine("Pronto. Passe os produtos pelo leitor.");

            string ultimaTag = null;

            while (true)
            {
                Data106kbpsTypeA cartao;

                if (!leitor.ListenToCardIso14443TypeA(out cartao, TimeSpan.FromSeconds(1)))
                {
                    Thread.Sleep(150);
                    continue;
                }

                string tag = Uid(cartao.NfcId);

                // Evita registrar o mesmo produto várias vezes se ele ficar
                // parado em cima do leitor.
                if (tag == ultimaTag)
                {
                    Thread.Sleep(500);
                    continue;
                }

                ultimaTag = tag;

                Debug.WriteLine("Tag lida: " + tag);

                EnviarItem(sessaoId, tag);
            }
        }

        /// <summary>GET /api/sessions/current-open — devolve o Id ou null.</summary>
        private static string BuscarSessaoAberta()
        {
            try
            {
                using (var resposta = _http.Get(BaseUrl + "/api/sessions/current-open"))
                {
                    if (resposta.StatusCode != HttpStatusCode.OK)
                    {
                        Debug.WriteLine("current-open devolveu " + resposta.StatusCode);
                        return null;
                    }

                    return ExtrairId(resposta.Content.ReadAsString());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no current-open: " + ex.Message);
                return null;
            }
        }

        /// <summary>POST /api/sessions/{id}/items/by-rfid</summary>
        private static void EnviarItem(string sessaoId, string tag)
        {
            string url = BaseUrl + "/api/sessions/" + sessaoId + "/items/by-rfid";
            string json = "{\"rfidTag\":\"" + tag + "\"}";

            try
            {
                var conteudo = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resposta = _http.Post(url, conteudo))
                {
                    Debug.WriteLine("POST -> " + resposta.StatusCode);

                    if (resposta.StatusCode == HttpStatusCode.OK)
                    {
                        // SUCESSO — aceso fixo por 1,5 s
                        _led.Write(PinValue.High);
                        Thread.Sleep(1500);
                        _led.Write(PinValue.Low);
                    }
                    else if (resposta.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Tag não vinculada a produto nenhum — 3 piscadas rápidas
                        Debug.WriteLine(resposta.Content.ReadAsString());
                        Piscar(3, 100);
                    }
                    else
                    {
                        // Qualquer outro erro — 5 piscadas rápidas
                        Debug.WriteLine(resposta.Content.ReadAsString());
                        Piscar(5, 100);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no POST: " + ex.Message);
                Piscar(5, 100);
            }
        }

        /// <summary>
        /// Pega o "id" do JSON sem desserializar. O SessionResponse tem Id como
        /// primeiro campo, então ele sai como {"id":"guid",... — 36 caracteres.
        /// Feio, mas evita mais um pacote e mais uma classe de contrato na placa.
        /// </summary>
        private static string ExtrairId(string json)
        {
            const string marca = "\"id\":\"";

            int inicio = json.IndexOf(marca);

            if (inicio < 0)
            {
                return null;
            }

            inicio += marca.Length;

            if (json.Length < inicio + 36)
            {
                return null;
            }

            return json.Substring(inicio, 36);
        }

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

        private static string Hex(byte valor)
        {
            const string digitos = "0123456789ABCDEF";

            return new string(new[] { digitos[valor >> 4], digitos[valor & 0x0F] });
        }

        /// <summary>
        /// Formata o UID como AA-BB-CC-DD, maiúsculas, separado por hífen.
        /// ⚠️ ESTE É O FORMATO QUE PRECISA ESTAR CADASTRADO NO CAMPO "Tag RFID"
        /// DO ADMINAPP. Se lá estiver escrito de outro jeito, a API devolve 404.
        /// </summary>
        private static string Uid(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
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
// PACOTES NuGet
//   nanoFramework.Iot.Device.Mfrc522
//   nanoFramework.Hardware.Esp32
//   nanoFramework.System.Device.Wifi
//   nanoFramework.System.Net.Http
//
// NÃO instale System.Device.Gpio nem System.Device.Spi à mão — o Mfrc522 já os
// traz nas versões exatas contra as quais foi compilado. Instalar por fora gera
// aviso de "diretiva de runtime" e o nanoFramework resolve assembly por versão
// exata.
//
// ---------------------------------------------------------------------------
// ANTES DE RODAR
//
// 1. A API de pé no perfil **http-rede (ESP32)**
// 2. Existir uma sessão com status "Aberta" — criar pelo app do cliente ou pelo
//    Swagger (POST /api/sessions e depois POST /api/sessions/confirm-entry)
// 3. Ter pelo menos um produto com o campo "Tag RFID" preenchido no AdminApp,
//    no formato AA-BB-CC-DD (maiúsculas, com hífen)
//
// Para descobrir a tag de um cartão, rode o Etapa3Rfid2 e leia o UID.
//
// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul (GPIO2)
//
// PARTIDA
//   3 rápidas ..................... programa rodando
//   + 2 lentas .................... Wi-Fi conectado
//   + 3 lentas .................... sessão aberta encontrada, leitor pronto
//
//   PARES em ciclo ................ Wi-Fi não conectou
//   QUATRO em ciclo ............... não há sessão "Aberta", ou a API não respondeu.
//                                   Confira no Swagger: GET /api/sessions/current-open
//
// A CADA PRODUTO PASSADO
//   aceso fixo 1,5 s .............. item adicionado na sessão, estoque baixado
//   3 rápidas ..................... 404: a tag lida não está vinculada a produto
//                                   nenhum. Cadastre no AdminApp, coluna Tag RFID
//   5 rápidas ..................... outro erro. Causas prováveis: sessão não está
//                                   mais "Aberta" (já passou pelo checkout), ou
//                                   estoque zerado
//
// LIMITAÇÃO CONHECIDA: a sessão é buscada uma única vez, na partida. Se a sessão
// mudar, é preciso reiniciar a placa. Para o MVP serve; depois vale rebuscar a
// sessão a cada leitura, ou quando vier erro.
// ---------------------------------------------------------------------------
