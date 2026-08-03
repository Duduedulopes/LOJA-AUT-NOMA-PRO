using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Threading;

namespace Etapa8Saida
{
    /// <summary>
    /// Driver mínimo do MFRC522, escrito à mão.
    ///
    /// POR QUE ISTO EXISTE: o pacote nanoFramework.Iot.Device.Mfrc522 (1.2.1016, a mais
    /// recente) foi compilado contra nanoFramework.Runtime.Events 1.11.37, enquanto o
    /// System.Device.Wifi 1.5.150 exige a 1.11.39. O nanoFramework resolve assembly por
    /// versão EXATA e não tem binding redirect, então os dois não coexistem — o sintoma
    /// é a placa reiniciando em loop assim que o código do Wi-Fi é usado.
    ///
    /// Rebaixar o Wifi também não resolve: a 1.5.141 pede o assembly nativo v100.0.6.5 e
    /// o firmware ESP32_REV3 gravado tem o v100.0.6.6.
    ///
    /// Este arquivo depende apenas de System.Device.Spi e System.Device.Gpio, que podem
    /// acompanhar as versões que o Wifi exige.
    ///
    /// ESCOPO: faz só o que o projeto precisa — dizer a versão do chip e ler o UID de
    /// cartões ISO 14443-A com UID de 4 bytes (Mifare Classic 1K, que é o cartão branco
    /// e o chaveiro dos kits). Não faz autenticação nem leitura de blocos.
    /// </summary>
    public class Rc522
    {
        // Registradores (datasheet MFRC522, seção 9.2)
        private const byte CommandReg = 0x01;
        private const byte ComIrqReg = 0x04;
        private const byte ErrorReg = 0x06;
        private const byte FifoDataReg = 0x09;
        private const byte FifoLevelReg = 0x0A;
        private const byte ControlReg = 0x0C;
        private const byte BitFramingReg = 0x0D;
        private const byte ModeReg = 0x11;
        private const byte TxControlReg = 0x14;
        private const byte TxAskReg = 0x15;
        private const byte TModeReg = 0x2A;
        private const byte TPrescalerReg = 0x2B;
        private const byte TReloadRegH = 0x2C;
        private const byte TReloadRegL = 0x2D;
        private const byte VersionReg = 0x37;

        // Comandos do MFRC522
        private const byte CmdIdle = 0x00;
        private const byte CmdTransceive = 0x0C;
        private const byte CmdSoftReset = 0x0F;

        // Comandos do cartão (PICC)
        private const byte PiccReqA = 0x26;
        private const byte PiccAnticoll1 = 0x93;

        private readonly SpiDevice _spi;
        private readonly GpioPin _reset;

        public Rc522(SpiDevice spi, GpioController gpio, int pinReset)
        {
            _spi = spi;

            _reset = gpio.OpenPin(pinReset, PinMode.Output);
            _reset.Write(PinValue.Low);
            Thread.Sleep(50);
            _reset.Write(PinValue.High);
            Thread.Sleep(50);

            Inicializar();
        }

        /// <summary>
        /// Versão do chip lida do VersionReg. 0x91 e 0x92 são os valores de fábrica.
        /// Clones baratos costumam devolver 0x00 ou 0xFF e ainda assim funcionar.
        /// </summary>
        public byte Versao
        {
            get { return LerRegistrador(VersionReg); }
        }

        /// <summary>
        /// Procura um cartão e devolve o UID, ou null se não houver nenhum no campo.
        /// Não bloqueia: uma passada leva poucos milissegundos.
        /// </summary>
        public byte[] LerUid()
        {
            // REQA — pergunta "tem alguém aí?". O 0x07 no BitFraming diz que o último
            // byte tem só 7 bits, que é como o REQA é definido no ISO 14443-3.
            EscreverRegistrador(BitFramingReg, 0x07);

            byte[] atqa = Transceive(new byte[] { PiccReqA });

            if (atqa == null || atqa.Length < 2)
            {
                return null;
            }

            // Anticolisão nível 1 — pede o UID. 0x00 no BitFraming: bytes completos.
            EscreverRegistrador(BitFramingReg, 0x00);

            byte[] resposta = Transceive(new byte[] { PiccAnticoll1, 0x20 });

            if (resposta == null || resposta.Length < 5)
            {
                return null;
            }

            // Os 4 primeiros bytes são o UID; o quinto é o BCC, um XOR de verificação.
            byte bcc = (byte)(resposta[0] ^ resposta[1] ^ resposta[2] ^ resposta[3]);

            if (bcc != resposta[4])
            {
                return null;   // leitura corrompida, provavelmente cartão saindo do campo
            }

            return new byte[] { resposta[0], resposta[1], resposta[2], resposta[3] };
        }

        // ------------------------------------------------------------------
        // Interno
        // ------------------------------------------------------------------

        private void Inicializar()
        {
            EscreverRegistrador(CommandReg, CmdSoftReset);
            Thread.Sleep(50);

            // Timer interno: prescaler e reload definem o timeout das operações.
            EscreverRegistrador(TModeReg, 0x8D);
            EscreverRegistrador(TPrescalerReg, 0x3E);
            EscreverRegistrador(TReloadRegL, 30);
            EscreverRegistrador(TReloadRegH, 0);

            EscreverRegistrador(TxAskReg, 0x40);   // modulação ASK 100%
            EscreverRegistrador(ModeReg, 0x3D);    // CRC preset 0x6363

            LigarAntena();
        }

        private void LigarAntena()
        {
            byte atual = LerRegistrador(TxControlReg);

            if ((atual & 0x03) != 0x03)
            {
                EscreverRegistrador(TxControlReg, (byte)(atual | 0x03));
            }
        }

        /// <summary>Envia bytes para o cartão e devolve a resposta, ou null.</summary>
        private byte[] Transceive(byte[] envio)
        {
            EscreverRegistrador(CommandReg, CmdIdle);
            EscreverRegistrador(ComIrqReg, 0x7F);          // limpa todas as interrupções
            EscreverRegistrador(FifoLevelReg, 0x80);       // esvazia a FIFO

            for (int i = 0; i < envio.Length; i++)
            {
                EscreverRegistrador(FifoDataReg, envio[i]);
            }

            EscreverRegistrador(CommandReg, CmdTransceive);

            // StartSend: manda a FIFO para o ar
            byte framing = LerRegistrador(BitFramingReg);
            EscreverRegistrador(BitFramingReg, (byte)(framing | 0x80));

            // Espera RxIRq (0x20) ou TimerIRq (0x01)
            bool recebeu = false;

            for (int tentativa = 0; tentativa < 40; tentativa++)
            {
                byte irq = LerRegistrador(ComIrqReg);

                if ((irq & 0x20) != 0)
                {
                    recebeu = true;
                    break;
                }

                if ((irq & 0x01) != 0)
                {
                    break;   // timer estourou: não tem cartão no campo
                }

                Thread.Sleep(1);
            }

            framing = LerRegistrador(BitFramingReg);
            EscreverRegistrador(BitFramingReg, (byte)(framing & 0x7F));

            if (!recebeu)
            {
                return null;
            }

            if ((LerRegistrador(ErrorReg) & 0x1B) != 0)
            {
                return null;   // erro de protocolo, paridade ou colisão
            }

            int quantidade = LerRegistrador(FifoLevelReg);

            if (quantidade == 0)
            {
                return null;
            }

            var resposta = new byte[quantidade];

            for (int i = 0; i < quantidade; i++)
            {
                resposta[i] = LerRegistrador(FifoDataReg);
            }

            return resposta;
        }

        /// <summary>
        /// Leitura SPI: o endereço vai deslocado um bit à esquerda, com o bit 7 em 1.
        /// É o formato definido no datasheet, seção 8.1.2.3.
        /// </summary>
        private byte LerRegistrador(byte registrador)
        {
            var escrita = new byte[] { (byte)(((registrador << 1) & 0x7E) | 0x80), 0x00 };
            var leitura = new byte[2];

            _spi.TransferFullDuplex(escrita, leitura);

            return leitura[1];
        }

        /// <summary>Escrita SPI: mesmo deslocamento, com o bit 7 em 0.</summary>
        private void EscreverRegistrador(byte registrador, byte valor)
        {
            _spi.Write(new byte[] { (byte)((registrador << 1) & 0x7E), valor });
        }
    }
}
