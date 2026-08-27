using System;
using System.Device.I2c;
using System.Threading;

namespace Etapa8Saida
{
    /// <summary>
    /// Driver manual para o display OLED SSD1306 128x64 via I2C.
    ///
    /// Por que driver manual e não o pacote nanoFramework.Iot.Device.Ssd13xx:
    /// o pacote de binding puxa nanoFramework.Runtime.Events 1.11.37, que é
    /// incompatível com System.Device.Wifi 1.5.150 (exige 1.11.39). Os dois
    /// não coexistem — o sintoma é a placa reiniciando em loop. Este driver
    /// depende apenas de System.Device.I2c, que já está no projeto.
    ///
    /// Pinagem usada (pinos padrão do ESP32 estão ocupados pelo RC522):
    ///   SDA → GPIO4    SCL → GPIO5
    ///
    /// Remapeamento obrigatório antes de instanciar:
    ///   Configuration.SetPinFunction(4, DeviceFunction.I2C1_DATA);
    ///   Configuration.SetPinFunction(5, DeviceFunction.I2C1_CLOCK);
    /// </summary>
    public sealed class Oled : IDisposable
    {
        // --- constantes do SSD1306 ---
        private const byte EnderecoI2c = 0x3C;
        private const int Largura = 128;
        private const int Altura = 64;
        private const int Paginas = Altura / 8; // 8 páginas de 8 bits

        private const byte CmdStream = 0x00; // co=0, D/C#=0  → comando
        private const byte DadoStream = 0x40; // co=0, D/C#=1  → dados

        private readonly I2cDevice _i2c;

        // Frame buffer: 128 colunas × 8 páginas
        private readonly byte[] _buf = new byte[Largura * Paginas];

        // ------------------------------------------------------------------ //
        //  Construção e inicialização                                          //
        // ------------------------------------------------------------------ //

        public Oled(int busId = 1)
        {
            var settings = new I2cConnectionSettings(busId, EnderecoI2c);
            _i2c = I2cDevice.Create(settings);
            Inicializar();
        }

        private void Inicializar()
        {
            // Sequência de inicialização padrão SSD1306 128x64
            byte[] seq = new byte[]
            {
                0xAE, // display off
                0xD5, 0x80, // clock div / osc freq
                0xA8, 0x3F, // mux ratio = 63 (64 linhas)
                0xD3, 0x00, // display offset = 0
                0x40,       // start line = 0
                0x8D, 0x14, // charge pump ON
                0x20, 0x00, // memory mode = horizontal
                0xA1,       // seg remap (coluna 127 → SEG0)
                0xC8,       // COM scan descending
                0xDA, 0x12, // COM pins config
                0x81, 0xCF, // contraste
                0xD9, 0xF1, // pre-charge
                0xDB, 0x40, // vcomh deselect
                0xA4,       // display from RAM
                0xA6,       // normal (não invertido)
                0x2E,       // scroll off
                0xAF,       // display ON
            };

            foreach (byte b in seq)
                EnviarComando(b);

            Limpar();
            Atualizar();
        }

        // ------------------------------------------------------------------ //
        //  API pública                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Apaga o frame buffer (não envia ao display — chame Atualizar).</summary>
        public void Limpar()
        {
            for (int i = 0; i < _buf.Length; i++)
                _buf[i] = 0;
        }

        /// <summary>Envia o frame buffer inteiro ao display.</summary>
        public void Atualizar()
        {
            // Define janela de endereçamento: colunas 0-127, páginas 0-7
            EnviarComando(0x21); EnviarComando(0); EnviarComando(127);
            EnviarComando(0x22); EnviarComando(0); EnviarComando(7);

            // Envia os dados em blocos de 16 bytes (I2C do nanoFramework tem limite)
            // Cada transferência começa com o byte de controle DadoStream (0x40)
            byte[] bloco = new byte[17]; // 1 byte controle + 16 bytes dados
            bloco[0] = DadoStream;

            for (int i = 0; i < _buf.Length; i += 16)
            {
                int len = (_buf.Length - i) < 16 ? (_buf.Length - i) : 16;
                for (int j = 0; j < len; j++)
                    bloco[j + 1] = _buf[i + j];

                // Se o último bloco for menor que 16, zera o resto
                for (int j = len; j < 16; j++)
                    bloco[j + 1] = 0;

                _i2c.Write(new SpanByte(bloco, 0, len + 1));
            }
        }

        /// <summary>
        /// Escreve texto no frame buffer na posição (coluna, página).
        /// Página 0 = topo, página 7 = base. Cada página tem 8 pixels de altura.
        /// A coluna vai de 0 a 127.
        /// Chame Atualizar() depois para enviar ao display.
        /// </summary>
        public void EscreverTexto(int coluna, int pagina, string texto)
        {
            if (texto == null || texto.Length == 0) return;
            if (pagina < 0 || pagina >= Paginas) return;

            int x = coluna;

            foreach (char c in texto)
            {
                if (x + BasicFont.LarguraChar > Largura) break;

                int idx = c - 32;
                if (idx < 0 || idx >= BasicFont.Dados.Length)
                    idx = 0; // caractere desconhecido → espaço

                byte[] glifo = BasicFont.Dados[idx];

                for (int col = 0; col < BasicFont.LarguraChar; col++)
                {
                    _buf[pagina * Largura + x + col] = glifo[col];
                }

                // coluna de espaço entre caracteres
                if (x + BasicFont.LarguraChar < Largura)
                    _buf[pagina * Largura + x + BasicFont.LarguraChar] = 0;

                x += BasicFont.LarguraTotal;
            }
        }

        /// <summary>
        /// Limpa o display, escreve duas linhas de texto e atualiza.
        /// Atalho para o caso mais comum: linha1 no topo, linha2 abaixo.
        /// </summary>
        public void Mostrar(string linha1, string linha2 = null)
        {
            Limpar();
            EscreverTexto(0, 0, linha1 ?? "");
            if (linha2 != null)
                EscreverTexto(0, 2, linha2);
            Atualizar();
        }

        /// <summary>Apaga tudo e atualiza o display.</summary>
        public void MostrarVazio()
        {
            Limpar();
            Atualizar();
        }

        // ------------------------------------------------------------------ //
        //  Helpers internos                                                    //
        // ------------------------------------------------------------------ //

        private void EnviarComando(byte cmd)
        {
            _i2c.Write(new SpanByte(new byte[] { CmdStream, cmd }));
        }

        public void Dispose()
        {
            _i2c?.Dispose();
        }
    }
}
