using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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
    /// Endpoints usados, ambos anônimos:
    ///   GET  /api/sessions/current-open
    ///   POST /api/sessions/{id}/items/by-rfid   body: { "rfidTag": "..." }
    ///
    /// O RC522 é acessado pelo driver próprio em Rc522.cs, e não pelo pacote
    /// nanoFramework.Iot.Device.Mfrc522 — ver o comentário no topo daquele arquivo.
    ///
    /// Diagnóstico pelo LED azul da placa (GPIO2). Tabela no fim deste arquivo.
    /// </summary>
    public class Program
    {
        // Credenciais em Segredos.cs, que está no .gitignore.
        // Se este projeto não compilar por falta da classe Segredos, copie o
        // Segredos.exemplo.cs para Segredos.cs e preencha com os seus valores.
        private const string Ssid = Segredos.Ssid;
        private const string Senha = Segredos.SenhaWifi;
        private const string BaseUrl = Segredos.BaseUrl;

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
            bool conectou;

            try
            {
                var cs = new CancellationTokenSource(60000);

                conectou = WifiNetworkHelper.ConnectDhcp(Ssid, Senha, requiresDateTime: false, token: cs.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Excecao no Wi-Fi: " + ex.Message);
                conectou = false;
            }

            if (!conectou)
            {
                Debug.WriteLine("Wi-Fi FALHOU: " + WifiNetworkHelper.Status);
                Falhar(2);
            }

            Debug.WriteLine("Wi-Fi conectado.");
            Piscar(2, 600);
            Thread.Sleep(600);

            _http = new HttpClient();
            Debug.WriteLine("HttpClient criado. BaseUrl: " + BaseUrl);

            // ---------- 2. Sessão aberta ----------
            string sessaoId = null;
            int tentativas = 0;

            while (sessaoId == null)
            {
                tentativas++;
                Debug.WriteLine("Buscando sessao aberta... tentativa " + tentativas);
                sessaoId = BuscarSessaoAberta();

                if (sessaoId == null)
                {
                    Debug.WriteLine("Nenhuma sessao aberta. Tentando de novo em 5s...");
                    Piscar(4, 100);
                    Thread.Sleep(5000);
                }
            }

            Debug.WriteLine("Sessao encontrada: " + sessaoId + " apos " + tentativas + " tentativas");
            Piscar(3, 600);
            Thread.Sleep(600);

            // ---------- 3. Leitor ----------
            Configuration.SetPinFunction(PinSck, DeviceFunction.SPI1_CLOCK);
            Configuration.SetPinFunction(PinMosi, DeviceFunction.SPI1_MOSI);
            Configuration.SetPinFunction(PinMiso, DeviceFunction.SPI1_MISO);

            var connection = new SpiConnectionSettings(1, PinSs);
            connection.ClockFrequency = 5_000_000;

            var leitor = new Rc522(SpiDevice.Create(connection), gpio, PinReset);

            Debug.WriteLine("RC522 versao: 0x" + Hex(leitor.Versao));
            Debug.WriteLine("Pronto. Passe os produtos pelo leitor.");

            string ultimaTag = null;
            int vaziasSeguidas = 0;

            while (true)
            {
                byte[] uid = leitor.LerUid();

                if (uid == null)
                {
                    // Depois de algumas leituras vazias seguidas, considera que o cartão
                    // saiu do campo e libera a mesma tag para ser lida de novo.
                    vaziasSeguidas++;

                    if (vaziasSeguidas > 6)
                    {
                        ultimaTag = null;
                    }

                    Thread.Sleep(150);
                    continue;
                }

                vaziasSeguidas = 0;

                string tag = Uid(uid);

                if (tag == ultimaTag)
                {
                    Thread.Sleep(300);
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
                string url = BaseUrl + "/api/sessions/current-open";
                Debug.WriteLine("Tentando conectar em: " + url);
                
                using (var resposta = _http.Get(url))
                {
                    Debug.WriteLine("Status Code: " + resposta.StatusCode);
                    
                    if (resposta.StatusCode != HttpStatusCode.OK)
                    {
                        Debug.WriteLine("current-open devolveu " + resposta.StatusCode);
                        Piscar(2, 100); // 2 piscadas = erro de status
                        return null;
                    }

                    string conteudo = resposta.Content.ReadAsString();
                    Debug.WriteLine("Tamanho do conteudo: " + conteudo.Length);
                    
                    if (conteudo.Length == 0)
                    {
                        Debug.WriteLine("Conteudo VAZIO recebido da API!");
                        Piscar(4, 100); // 4 piscadas = conteúdo vazio
                        return null;
                    }
                    
                    Debug.WriteLine("Conteudo recebido: " + (conteudo.Length > 50 ? conteudo.Substring(0, 50) + "..." : conteudo));
                    
                    string id = ExtrairId(conteudo);
                    Debug.WriteLine("ID extraido: " + (id ?? "null"));
                    
                    return id;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no current-open: " + ex.Message);
                Piscar(3, 100); // 3 piscadas = exceção
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
                        _led.Write(PinValue.High);
                        Thread.Sleep(1500);
                        _led.Write(PinValue.Low);
                    }
                    else if (resposta.StatusCode == HttpStatusCode.NotFound)
                    {
                        Debug.WriteLine(resposta.Content.ReadAsString());
                        Piscar(3, 100);
                    }
                    else
                    {
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
        /// Pega o "id" do JSON sem desserializar.
        /// Procura pela primeira ocorrência de um GUID (36 chars: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        /// depois da chave "id" — robusto contra espaços, tabs e quebras de linha.
        /// </summary>
        private static string ExtrairId(string json)
        {
            if (json == null || json.Length == 0)
            {
                Debug.WriteLine("ExtrairId: json vazio");
                Piscar(4, 100); // 4 piscadas = JSON vazio
                return null;
            }

            Debug.WriteLine("ExtrairId json[0..100]: " + (json.Length > 100 ? json.Substring(0, 100) : json));

            // Busca "id" com qualquer espaçamento em volta dos dois pontos
            int posId = json.IndexOf("\"id\"");
            if (posId < 0)
            {
                Debug.WriteLine("ExtrairId: 'id' nao encontrado");
                Piscar(5, 100); // 5 piscadas = 'id' não encontrado
                return null;
            }

            Debug.WriteLine("ExtrairId: posId=" + posId);

            // Avança após "id" e busca a primeira aspa que abre o valor
            int posAspa = json.IndexOf('"', posId + 4);
            if (posAspa < 0)
            {
                Debug.WriteLine("ExtrairId: aspa de abertura nao encontrada");
                Piscar(6, 100); // 6 piscadas = aspa não encontrada
                return null;
            }

            Debug.WriteLine("ExtrairId: posAspa=" + posAspa);

            int inicio = posAspa + 1;
            Debug.WriteLine("ExtrairId: inicio=" + inicio);

            if (json.Length < inicio + 36)
            {
                Debug.WriteLine("ExtrairId: json curto demais, len=" + json.Length + " inicio=" + inicio);
                Piscar(7, 100); // 7 piscadas = JSON muito curto
                return null;
            }

            string guid = json.Substring(inicio, 36);
            Debug.WriteLine("ExtrairId: guid extraido=" + guid);
            
            // Valida formato GUID básico (8-4-4-4-12 hífens)
            if (guid[8] != '-' || guid[13] != '-' || guid[18] != '-' || guid[23] != '-')
            {
                Debug.WriteLine("ExtrairId: formato GUID invalido: " + guid);
                Piscar(8, 100); // 8 piscadas = formato inválido
                return null;
            }

            Debug.WriteLine("ExtrairId: GUID valido!");
            return guid;
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
        /// ⚠️ ESTE É O FORMATO QUE PRECISA ESTAR NO CAMPO "Tag RFID" DO ADMINAPP.
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
// PACOTES NuGet — apenas estes quatro:
//   nanoFramework.System.Device.Gpio
//   nanoFramework.System.Device.Spi
//   nanoFramework.Hardware.Esp32
//   nanoFramework.System.Device.Wifi
//   nanoFramework.System.Net.Http
//
// ⚠️ NÃO instalar o nanoFramework.Iot.Device.Mfrc522. Ele conflita com o Wifi na
// versão do Runtime.Events e faz a placa reiniciar em loop. O driver próprio em
// Rc522.cs existe exatamente para evitá-lo.
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
//   QUATRO em ciclo ............... não há sessão "Aberta", ou a API não respondeu
//   3 rápidas repetindo sem parar . a placa está reiniciando em loop — quase sempre
//                                   conflito de versão de assembly entre pacotes
//
// A CADA PRODUTO PASSADO
//   aceso fixo 1,5 s .............. item adicionado, estoque baixado
//   3 rápidas ..................... 404: tag não vinculada a nenhum produto
//   5 rápidas ..................... outro erro (sessão fechada, estoque zerado)
// ---------------------------------------------------------------------------
