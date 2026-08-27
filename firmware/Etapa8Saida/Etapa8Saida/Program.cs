using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Device.I2c;
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
    /// Etapa 8 — o leitor de saída do MVP com feedback no OLED SSD1306.
    ///
    /// Fluxo: passa o produto pelo RC522, a tag vai para a API, a API resolve qual
    /// produto é, adiciona na sessão aberta, baixa o estoque, e o OLED mostra o
    /// nome e o preço do produto.
    ///
    /// Endpoints usados, ambos anônimos:
    ///   GET  /api/sessions/current-open
    ///   POST /api/sessions/{id}/items/by-rfid   body: { "rfidTag": "..." }
    ///
    /// O RC522 é acessado pelo driver próprio em Rc522.cs, e não pelo pacote
    /// nanoFramework.Iot.Device.Mfrc522 — ver o comentário no topo daquele arquivo.
    /// O OLED é acessado pelo driver próprio em Oled.cs, e não pelo pacote
    /// nanoFramework.Iot.Device.Ssd13xx — mesmo motivo (conflito com Wifi).
    ///
    /// Diagnóstico pelo LED azul da placa (GPIO2). Tabela no fim deste arquivo.
    /// </summary>
    public class Program
    {
        // Credenciais em Segredos.cs, que está no .gitignore.
        private const string Ssid    = Segredos.Ssid;
        private const string Senha   = Segredos.SenhaWifi;
        private const string BaseUrl = Segredos.BaseUrl;

        // Pinagem do RC522 (SPI1)
        private const int PinSck   = 18;
        private const int PinMosi  = 23;
        private const int PinMiso  = 19;
        private const int PinSs    = 21;
        private const int PinReset = 22;

        // Pinagem do OLED (I2C1) — GPIO padrão 21/22 estão ocupados pelo RC522
        private const int PinSda = 4;
        private const int PinScl = 5;

        private const int PinLed = 2;

        private static GpioPin  _led;
        private static HttpClient _http;
        private static Oled     _oled;

        public static void Main()
        {
            var gpio = new GpioController();
            _led = gpio.OpenPin(PinLed, PinMode.Output);
            _led.Write(PinValue.Low);

            // ---------- 0. OLED ----------
            // Remapeia I2C1 para os pinos livres ANTES de instanciar o barramento
            Configuration.SetPinFunction(PinSda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(PinScl, DeviceFunction.I2C1_CLOCK);

            try
            {
                _oled = new Oled(busId: 1);
                _oled.Mostrar("Iniciando...", "");
            }
            catch (Exception ex)
            {
                // OLED opcional — se falhar, o programa continua só com o LED
                Debug.WriteLine("OLED falhou: " + ex.Message);
                _oled = null;
            }

            Piscar(3, 120);
            Thread.Sleep(1000);

            // ---------- 1. Wi-Fi ----------
            bool conectou;
            MostrarOled("Conectando", "WiFi...");

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
                MostrarOled("WiFi FALHOU", WifiNetworkHelper.Status.ToString());
                Falhar(2);
            }

            Debug.WriteLine("Wi-Fi conectado.");
            Piscar(2, 600);
            Thread.Sleep(600);

            _http = new HttpClient();

            // ---------- 2. Sessão aberta ----------
            string sessaoId = null;
            int tentativas = 0;

            while (sessaoId == null)
            {
                tentativas++;
                Debug.WriteLine("Buscando sessao aberta... tentativa " + tentativas);
                MostrarOled("Aguardando", "sessao aberta...");
                sessaoId = BuscarSessaoAberta();

                if (sessaoId == null)
                {
                    Debug.WriteLine("Nenhuma sessao aberta. Tentando de novo em 5s...");
                    Piscar(4, 100);
                    Thread.Sleep(5000);
                }
            }

            Debug.WriteLine("Sessao: " + sessaoId);
            Piscar(3, 600);
            Thread.Sleep(600);
            MostrarOled("Pronto!", "Passe o produto");

            // ---------- 3. Remapeia SPI e inicializa RC522 ----------
            // Feito DEPOIS do OLED para não interferir com o I2C durante a init
            Configuration.SetPinFunction(PinSck,  DeviceFunction.SPI1_CLOCK);
            Configuration.SetPinFunction(PinMosi, DeviceFunction.SPI1_MOSI);
            Configuration.SetPinFunction(PinMiso, DeviceFunction.SPI1_MISO);

            var connection = new SpiConnectionSettings(1, PinSs);
            connection.ClockFrequency = 5_000_000;

            var leitor = new Rc522(SpiDevice.Create(connection), gpio, PinReset);
            Debug.WriteLine("RC522 versao: 0x" + Hex(leitor.Versao));
            Debug.WriteLine("Pronto. Passe os produtos pelo leitor.");

            string ultimaTag     = null;
            int    vaziasSeguidas = 0;

            while (true)
            {
                byte[] uid = leitor.LerUid();

                if (uid == null)
                {
                    vaziasSeguidas++;
                    if (vaziasSeguidas > 6)
                        ultimaTag = null;

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
                MostrarOled("Lendo...", tag);

                EnviarItem(sessaoId, tag);
            }
        }

        // ------------------------------------------------------------------ //
        //  HTTP                                                                //
        // ------------------------------------------------------------------ //

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

                    string conteudo = resposta.Content.ReadAsString();
                    return ExtrairId(conteudo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no current-open: " + ex.Message);
                return null;
            }
        }

        private static void EnviarItem(string sessaoId, string tag)
        {
            string url  = BaseUrl + "/api/sessions/" + sessaoId + "/items/by-rfid";
            string json = "{\"rfidTag\":\"" + tag + "\"}";

            try
            {
                var conteudo = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resposta = _http.Post(url, conteudo))
                {
                    Debug.WriteLine("POST -> " + resposta.StatusCode);

                    if (resposta.StatusCode == HttpStatusCode.OK)
                    {
                        // Extrai nome e preço da resposta para exibir no OLED
                        string body  = resposta.Content.ReadAsString();
                        string nome  = ExtrairUltimoProduto(body, "\"nomeProduto\":");
                        string preco = ExtrairUltimoProduto(body, "\"precoUnitario\":");

                        string linha2 = (preco != null) ? "R$ " + preco : "OK";
                        MostrarOled(nome ?? "Adicionado!", linha2);

                        _led.Write(PinValue.High);
                        Thread.Sleep(1500);
                        _led.Write(PinValue.Low);

                        // Volta para a mensagem de espera depois de 3 segundos
                        Thread.Sleep(1500);
                        MostrarOled("Pronto!", "Passe o produto");
                    }
                    else if (resposta.StatusCode == HttpStatusCode.NotFound)
                    {
                        string erro = resposta.Content.ReadAsString();
                        Debug.WriteLine(erro);
                        MostrarOled("Tag nao", "cadastrada!");
                        Piscar(3, 100);
                        Thread.Sleep(2000);
                        MostrarOled("Pronto!", "Passe o produto");
                    }
                    else
                    {
                        string erro = resposta.Content.ReadAsString();
                        Debug.WriteLine(erro);
                        MostrarOled("Erro!", resposta.StatusCode.ToString());
                        Piscar(5, 100);
                        Thread.Sleep(2000);
                        MostrarOled("Pronto!", "Passe o produto");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no POST: " + ex.Message);
                MostrarOled("Erro rede", "Tente novamente");
                Piscar(5, 100);
                Thread.Sleep(2000);
                MostrarOled("Pronto!", "Passe o produto");
            }
        }

        // ------------------------------------------------------------------ //
        //  Parsing JSON simples (sem desserialização)                          //
        // ------------------------------------------------------------------ //

        private static string ExtrairId(string json)
        {
            if (json == null || json.Length == 0) return null;

            int posId = json.IndexOf("\"id\"");
            if (posId < 0) return null;

            int posAspa = json.IndexOf('"', posId + 4);
            if (posAspa < 0) return null;

            int inicio = posAspa + 1;
            if (json.Length < inicio + 36) return null;

            string guid = json.Substring(inicio, 36);

            if (guid[8] != '-' || guid[13] != '-' || guid[18] != '-' || guid[23] != '-')
                return null;

            return guid;
        }

        /// <summary>
        /// Extrai o valor string do ÚLTIMO item da lista que contenha a chave indicada.
        /// Usado para pegar o nome e o preço do produto recém-adicionado na sessão.
        /// Exemplo de fragmento: "nomeProduto":"Agua 500ml"
        /// </summary>
        private static string ExtrairUltimoProduto(string json, string chave)
        {
            if (json == null || chave == null) return null;

            int pos = json.LastIndexOf(chave);
            if (pos < 0) return null;

            pos += chave.Length;

            // pula espaços e dois pontos até achar a aspa ou o dígito
            while (pos < json.Length && (json[pos] == ' ' || json[pos] == ':'))
                pos++;

            if (pos >= json.Length) return null;

            // valor string (entre aspas)
            if (json[pos] == '"')
            {
                pos++; // pula a aspa de abertura
                int fim = json.IndexOf('"', pos);
                if (fim < 0) return null;
                string valor = json.Substring(pos, fim - pos);
                // Trunca para caber no OLED (128px / 6px por char ≈ 21 chars)
                return valor.Length > 20 ? valor.Substring(0, 20) : valor;
            }

            // valor numérico (sem aspas — precoUnitario é decimal)
            int fimNum = pos;
            while (fimNum < json.Length && json[fimNum] != ',' && json[fimNum] != '}')
                fimNum++;

            return json.Substring(pos, fimNum - pos).Trim();
        }

        // ------------------------------------------------------------------ //
        //  OLED helper                                                         //
        // ------------------------------------------------------------------ //

        private static void MostrarOled(string linha1, string linha2 = null)
        {
            if (_oled == null) return;

            try
            {
                _oled.Mostrar(linha1, linha2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OLED erro: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ //
        //  LED helpers                                                         //
        // ------------------------------------------------------------------ //

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

        // ------------------------------------------------------------------ //
        //  Formatação de tag RFID                                              //
        // ------------------------------------------------------------------ //

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
            if (bytes == null || bytes.Length == 0) return "";

            string texto = Hex(bytes[0]);
            for (int i = 1; i < bytes.Length; i++)
                texto += "-" + Hex(bytes[i]);

            return texto;
        }
    }
}

// ---------------------------------------------------------------------------
// PACOTES NuGet necessários (além dos já existentes):
//   + nanoFramework.System.Device.I2c  ← novo, para o OLED
//
// ⚠️ NÃO instalar:
//   nanoFramework.Iot.Device.Mfrc522   ← conflito com Wifi
//   nanoFramework.Iot.Device.Ssd13xx   ← conflito com Wifi (mesmo motivo)
//
// ---------------------------------------------------------------------------
// TABELA DE SINAIS — LED azul (GPIO2)
//
// PARTIDA
//   3 rápidas ..................... programa rodando
//   + 2 lentas .................... Wi-Fi conectado
//   + 3 lentas .................... sessão aberta encontrada, leitor pronto
//
//   2 em ciclo .................... Wi-Fi não conectou
//   4 em ciclo .................... sem sessão "Aberta" (aguardando no loop)
//
// A CADA PRODUTO PASSADO
//   aceso fixo 1,5 s .............. item adicionado — OLED mostra nome e preço
//   3 rápidas ..................... 404: tag não vinculada a nenhum produto
//   5 rápidas ..................... outro erro (sessão fechada, estoque zerado)
//
// OLED — sequência de mensagens
//   "Iniciando..."          → programa começou
//   "Conectando / WiFi..."  → tentando Wi-Fi
//   "Aguardando / sessao"   → buscando sessão aberta
//   "Pronto! / Passe o produto" → leitor ativo
//   "<nome produto> / R$ X" → produto adicionado com sucesso
//   "Tag nao / cadastrada!" → tag desconhecida pela API
//   "Erro! / <status>"      → outro erro HTTP
// ---------------------------------------------------------------------------
