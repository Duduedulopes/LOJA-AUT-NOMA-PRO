// ---------------------------------------------------------------------------
// MODELO — copie este arquivo para Segredos.cs e preencha com os seus valores.
//
// O Segredos.cs está no .gitignore e não vai para o repositório. Este arquivo
// de exemplo é versionado só para documentar o que precisa ser configurado.
//
// No Visual Studio, o Segredos.cs precisa estar incluído no projeto — se ele não
// aparecer no Gerenciador de Soluções, use "Adicionar > Item Existente".
// ---------------------------------------------------------------------------

namespace Etapa8Saida
{
    internal static class Segredos
    {
        /// <summary>Nome da rede Wi-Fi. A ESP32 só enxerga redes de 2,4 GHz.</summary>
        public const string Ssid = "NOME_DA_SUA_REDE";

        /// <summary>Senha da rede Wi-Fi.</summary>
        public const string SenhaWifi = "SUA_SENHA";

        /// <summary>
        /// Endereço da API na rede local, sem barra no final.
        /// Descobrir o IP do PC com:  ipconfig | Select-String "IPv4"
        /// </summary>
        public const string BaseUrl = "http://192.168.1.100:5071";
    }
}
