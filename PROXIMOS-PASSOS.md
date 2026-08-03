# Onde paramos — 31/07/2026

> ✅ **C# rodando na placa.** LED azul (GPIO2) piscando com o `Etapa2Pisca`.
> Runtime confirmado: nanoCLR 1.17.0.335, alvo `ESP32_REV3`, sem PSRAM.

## ⚠️ Use SEMPRE o template "Blank Application (nanoFramework)"

Foi a causa de uma madrugada inteira perdida. O projeto `Etapa3Rfid` foi criado sem
querer com o template de **teste unitário** do nanoFramework — que não tem `Program.cs`
e não roda como aplicação. Dar F5 nele produz:

> The service provider has not been configured yet.

E o pior: o deploy reporta **"1 bem sucedida"**, então parece que tudo funcionou. O CLR
fica parado esperando um depurador que nunca anexa, e o código nunca executa. Sem
exceção, sem log, sem pista.

Como reconhecer que o projeto está errado: **não existe `Program.cs`.** Se o Gerenciador
de Soluções mostra `UnitTest1.cs` e `packages.config`, é o template errado — refaça.

## ✅ Etapas 6 e 7 validadas — a placa fala com a API

- **Etapa 6:** ESP32 conecta no Wi-Fi. Projeto `firmware/Etapa6Wifi`.
- **Etapa 7:** ESP32 faz `GET /api/categories` e recebe 200. Projeto `firmware/Etapa7Api`.

Pacotes usados: `nanoFramework.System.Device.Wifi`, `nanoFramework.System.Device.Gpio`
e `nanoFramework.System.Net.Http`.

### Rede: hotspot do iPhone

A rede em uso é o roteamento pessoal do celular, faixa `172.20.10.x`. Vantagem: é
2,4 GHz, então o problema clássico de a ESP32 não ver redes de 5 GHz não existe aqui.

⚠️ **O IP do PC muda a cada reconexão do hotspot.** Quando o teste parar de funcionar
sem motivo aparente, é a primeira coisa a conferir:

```powershell
ipconfig | Select-String "IPv4"
```

Use o `172.20.10.x`. O IP público que também aparece na lista é de um adaptador de VPN
— vale desligar a VPN durante os testes, porque ela captura o tráfego e pode impedir a
placa de alcançar o PC mesmo estando na mesma rede.

Trocar de rede exige atualizar três constantes no firmware: `Ssid`, `Senha` e `Url`.

### O que precisa estar de pé para a placa alcançar a API

1. Descobrir o IP da máquina e colocar na constante `Url` do programa:
   ```powershell
   ipconfig | Select-String "IPv4"
   ```
2. Liberar a porta no firewall, PowerShell **como administrador**, uma vez só:
   ```powershell
   New-NetFirewallRule -DisplayName "AutonomousStore API 5071" -Direction Inbound `
     -Protocol TCP -LocalPort 5071 -Action Allow -Profile Private
   ```
3. Rodar a API normalmente — **pelo perfil de solução "Novo Perfil"**, como sempre.

### Como o launchSettings ficou, e por quê

Os perfis `http` e `https` da WebApi agora são **idênticos**, os dois escutando em:

```
https://localhost:7167;http://0.0.0.0:5071
```

Dois endereços porque há dois tipos de cliente:

- **`https://localhost:7167`** — o que o AdminApp e o ClientApp chamam. A URL está fixa
  no código deles (`AdminAuthApiService`, por exemplo). Se a API não escutar aí, o login
  do AdminApp morre com `ERR_CONNECTION_REFUSED`.
- **`http://0.0.0.0:5071`** — `0.0.0.0` significa "todas as interfaces de rede", e é o
  que permite a ESP32 alcançar o PC. Em HTTP porque a placa recusa o certificado de
  desenvolvimento, que é autoassinado.

**E por que os dois perfis são iguais:** o arquivo `AutonomousStore.slnLaunch.user`
define um perfil de solução chamado **"Novo Perfil"** que sobe WebApi, ClientApp e
AdminApp juntos. Ele não especifica qual perfil da WebApi usar, então o Visual Studio
pega o **primeiro** da lista e ignora o que estiver selecionado no dropdown. Deixar os
dois equivalentes elimina essa pegadinha — qualquer caminho funciona.

Houve um perfil `http-rede (ESP32)` durante os testes. Foi removido: era redundante e
não era usado pelo perfil de solução, o que gerou meia hora de confusão.

**Teste que economiza tempo, quando algo parar de funcionar:** abra
`http://<IP-DO-PC>:5071/api/categories` num navegador de **outro aparelho** na mesma
rede. Se o outro aparelho vê e a placa não, o problema é firmware. Se nenhum dos dois
vê, é firewall, perfil da API ou IP errado — e não vale tocar no firmware.

## 🔐 Onde ficam os segredos agora

Nada de credencial no código versionado. Três lugares:

| O quê | Onde |
|---|---|
| Connection string, JWT, Gemini, SMTP, Google Client ID | `AutonomousStore.WebApi/appsettings.Development.json` — no `.gitignore` |
| Connection string para migrations do EF | variável de ambiente `AUTONOMOUSSTORE_CONNECTION` |
| Wi-Fi e IP da API no firmware | `firmware/*/Segredos.cs` — no `.gitignore` |

O `appsettings.json` versionado mantém a estrutura completa com placeholders, e cada
projeto de firmware tem um `Segredos.exemplo.cs` versionado como modelo.

⚠️ **Se algum projeto de firmware parar de compilar** reclamando da classe `Segredos`, é
porque o `Segredos.cs` não existe naquela pasta. Copie o `Segredos.exemplo.cs`.

⚠️ **Se os comandos de migration falharem**, é a variável de ambiente que não está
definida — e ela só é lida quando o Visual Studio inicia.

## 🔑 Dados úteis do ambiente

| O quê | Valor |
|---|---|
| Tag RFID do cartão branco | **`FA-B4-10-35`** |
| MAC da ESP32 | `70:4B:CA:6D:E0:08` |
| `customerId` do Eduardo | `f478b711-615e-4a7d-ba01-801966441483` |
| IP do PC no hotspot | `172.20.10.8` (muda a cada reconexão) |

### Como descobrir a tag de um cartão novo

O firmware não tem saída de texto utilizável, então o `AddItemByRfid` do
`SessionsController` imprime a tag recebida no **console da API**:

```
[RFID] tag recebida: "FA-B4-10-35"  (sessao ...)
[RFID] nenhum produto com essa tag. Cadastre "FA-B4-10-35" no AdminApp.
```

Passe o cartão novo pelo leitor com o `Etapa8Saida` rodando e leia o console.
Esses `Console.WriteLine` são de apoio ao desenvolvimento e podem sair quando o
leitor estiver estável.

### ⚠️ Dois tropeços que se repetem ao retomar o trabalho

**1. A API não inicia — processo órfão segurando as DLLs.**

O erro é `O processo não pode acessar o arquivo ... bloqueado por: "AutonomousStore.WebApi (NNNN)"`.
Sobrou uma instância antiga rodando. Mate e recompile:

```powershell
Get-Process AutonomousStore.WebApi -ErrorAction SilentlyContinue | Stop-Process -Force
```

**2. A sessão de ontem não existe mais — e isso é por design.**

O `StoreSession.TryExpire()` cancela sessões `Aberta` sem checkout depois de 60 minutos.
Toda sessão de teste morre sozinha. O sintoma na placa é o laço de **4 piscadas**
(`current-open` devolvendo 404). Basta criar uma sessão nova — a placa sai do laço na
próxima tentativa, sem precisar reconectar o USB.

**3. O IP do PC muda.** Sempre confirmar antes de culpar o firmware:

```powershell
ipconfig | Select-String "IPv4"
```

### Como criar uma sessão "Aberta" para testar

1. `POST /api/auth/login` → guardar o token
2. **Authorize** no Swagger, colar só a string do token (sem crases, sem aspas)
3. `POST /api/sessions` com `{ "customerId": "f478b711-..." }` → guardar o `qrCodeToken`
4. `POST /api/sessions/confirm-entry` com `{ "qrCodeToken": "..." }` — **em até 5 minutos**,
   que é a validade do QR. Se vencer: `POST /api/sessions/{id}/regenerate-qrcode`
5. `GET /api/sessions/current-open` tem que devolver 200

Reiniciar a API não derruba a sessão (ela está no SQL Server), mas derruba o Authorize
do Swagger.

## ✅ Etapa 8 validada — o leitor de saída funciona de ponta a ponta

**Cartão passado no RC522 → tag pelo Wi-Fi → API resolve o produto → item no carrinho
→ estoque baixado.** Projeto `firmware/Etapa8Saida`.

Funcionando junto com isso: a webcam com o Gemini (`VisionController`).

### Decisão de escopo do MVP

O RC522 fica **só na saída**, lendo a tag do produto para montar a compra e cobrar.
Não haverá leitor na entrada nem na prateleira nesta fase. A entrada continua pelo
QR code do app do cliente.

### A peça que resolveu o conflito de assembly

O pacote `nanoFramework.Iot.Device.Mfrc522` **não pode ser usado junto com o
`System.Device.Wifi`** — ver o comentário no topo de
`firmware/Etapa8Saida/Etapa8Saida/Rc522.cs`. O driver do RC522 é próprio, escrito à mão,
e depende só de `System.Device.Spi` e `System.Device.Gpio`.

Sintoma se alguém reinstalar o pacote: a placa reinicia em loop, repetindo as 3
piscadas de partida sem nunca chegar no Wi-Fi.

## 🔜 Onde retomar

Ordem sugerida, da maior para a menor entrega:

1. **Fechar a compra.** O carrinho já enche, mas ninguém fecha. Faltam
   `POST /api/sessions/{id}/checkout` e `confirm-payment` sendo chamados por alguém —
   provavelmente o ClientApp, não a placa.
2. **Texto no OLED.** Hoje a tela só acende. Para escrever "Produto adicionado — R$ X"
   é preciso resolver a fonte (ver o aviso sobre `BasicFont` mais abaixo). É o que
   transforma o LED azul em feedback de verdade para o cliente.
3. **Renumerar os códigos de pisca.** Os diagnósticos adicionados dentro de
   `BuscarSessaoAberta` (2, 3 e 4 piscadas) colidem com os sinais que já existiam para
   Wi-Fi, 404 e sessão ausente. Um padrão de piscadas não identifica mais a causa
   sozinho. Sugestão: usar 6, 7 e 8 para os novos.
4. **PIR.** Falta um jumper macho-fêmea para levar o VCC ao VIN. Código pronto em
   `referencia/Etapa5Pir-Program.cs`.
5. **Tirar os `Console.WriteLine` de `[RFID]`** do `SessionsController` quando não
   forem mais necessários para descobrir UIDs.

## ⚠️ "The service provider has not been configured yet" — o que é de verdade

Esse erro apareceu a noite toda e foi mal interpretado várias vezes. O que ele é:
**o serviço de dispositivo do nanoFramework não foi inicializado, e o deploy falha.**

Repare sempre no resumo da Saída:

> `Implantação: 0 bem sucedida, 1 com falha`

Quando é isso, **o programa não chegou na placa** — e o que roda ao reconectar o USB é
o programa **anterior**. Isso engana: parece que o código novo executou e se comportou
mal, quando na verdade ele nunca foi implantado.

**Causa provável nº 1: várias janelas do Visual Studio abertas.** Cada solução abre uma
janela, e cada janela tenta tomar a COM6 em **exclusivo**. A primeira ganha, as outras
falham com essa mensagem. Fechar todas menos a que está sendo trabalhada — e conferir
`devenv.exe` órfãos no Gerenciador de Tarefas.

**Causa provável nº 2: o Device Explorer não foi aberto na sessão.** O serviço só
inicializa quando aquela janela abre.

**Sequência que evita o problema:**

1. Uma única janela do Visual Studio aberta
2. `Exibir > Outras Janelas > Device Explorer`
3. Esperar a placa aparecer e **clicar nela** para selecionar
4. `Compilar > Implantar Solução` — conferir "1 bem sucedida"
5. Desconectar e reconectar o USB

## O ciclo de trabalho — sempre reconectar o USB

Com o template correto o F5 **não dá mais erro**, e a saída chega até:

> Ready. The nanoDevice runtime is loading the application assemblies and starting execution.

Mas, na prática observada, **o programa não roda sob o F5** — nem o LED nem o OLED
reagem. Só executa depois de um boot limpo. Então o ciclo é:

1. `Compilar > Implantar Solução`
2. **Desconectar e reconectar o cabo USB**
3. Olhar a placa

Consequência: **o `Debug.WriteLine` não é confiável como diagnóstico.** Todo programa em
`firmware/referencia` sinaliza pelo LED azul do GPIO2, com tabela de sinais comentada no
fim do arquivo. Continue nesse padrão.

Fica em aberto por que o F5 não executa apesar de anunciar que vai. Não vale gastar tempo
nisso enquanto o ciclo acima funciona.

Para verificar o que está na placa a qualquer momento (com o VS fechado — ele toma a
COM6 em exclusivo; finalize `devenv.exe` no Gerenciador de Tarefas):

```powershell
nanoff --nanodevice --serialport COM6 --devicedetails
```

A seção `Assemblies:` mostra o programa implantado.

## 🔧 Pendência de hardware: o LED da protoboard não acende

Independente do software. O LED do GPIO17 não acende **nem ligado direto nos trilhos,
sem resistor, com dois LEDs diferentes e nas duas orientações**. Ao mesmo tempo o RC522
e o LED de alimentação do ESP32 estão acesos, então há 3,3 V em algum lugar.

Hipóteses ainda não descartadas:

- os **dois pares** de trilhos da protoboard (o de cima e o de baixo) não se conectam
  entre si — o LED pode estar num par que não recebe alimentação
- em protoboards grandes cada trilho é **cortado ao meio**; só a metade onde chega o
  fio da Etapa 1 tem energia
- as duas pernas do LED podem estar caindo na **mesma coluna** (curto) ou uma delas na
  metade A–E em vez de F–J

Teste que elimina a dúvida: perna longa direto no pino **3V3** do ESP32 e perna curta
direto no **GND**, sem usar trilho nenhum.

Esse LED é só ferramenta de diagnóstico, não faz parte do produto — não bloqueia o
avanço para o RC522.

## Situação do hardware

A montagem na protoboard está feita até a **Etapa 2**.

| Etapa | Status | Observação |
|---|---|---|
| 1 — Alimentação (2 fios) | ✅ feita | RC522 acende a luz dele = 3,3 V e GND chegando |
| 2 — LED + resistor | montada, não testada | LED **apagado** sem código = correto |
| 3 — RC522 (5 fios de dados) | ✅ **validada** | versão 2.0, lê cartão e chaveiro |
| 4 — OLED (2 fios de dados) | ✅ **validada** | responde em 0x3C, tela acende |
| 5 — PIR (1 fio) | **desmontado** | falta jumper macho-fêmea para o VIN |

Diagrama de referência: `diagrama/montagem-por-etapas.pdf` (uma página por etapa).
Os outros PDFs dessa pasta são versões antigas — podem ser apagados.

⚠️ **Os números de coluna do diagrama NÃO correspondem à montagem real.** O diagrama
pressupõe a placa a cavalo sobre a canaleta central, com as duas fileiras de pinos
caindo em furos. Na montagem real o ESP32 está na **borda** da protoboard: uma fileira
entra nos furos e a outra fica de fora, com jumpers fêmea encaixados direto nos pinos.

**Use os nomes dos pinos (D18, D21, VIN...), nunca os números de coluna.** A tabela de
pinagem abaixo é a fonte de verdade; o diagrama serve só como referência visual de
quais componentes vão onde.

O pino **`VIN`** fica no topo da fileira oposta ao `3V3` — os dois são os primeiros
pinos de cada lado, um de frente para o outro. Ele entrega os 5 V do USB sem passar
pelo regulador de 3,3 V.

## Situação do software

- **Driver CP2102**: instalado. A placa aparece em **COM6**.
- **Arduino IDE**: instalado, mas **abandonado** — ver abaixo.
- **Pacote esp32 (Espressif)**: ❌ falhou duas vezes por falta de espaço em disco.

### Decisão de 31/07: sair do Arduino e ir de .NET nanoFramework

O core esp32 3.3.11 baixa as bibliotecas das nove variantes de chip (C3, C5, C6, H2,
P4, P4-ES, S2, S3 e a clássica) e mantém três cópias simultâneas — compactado em
`staging`, extraído em `tmp` e final em `packages`. Passa de 9 GB durante a instalação.
Não cabe no disco atual.

O nanoFramework roda **C# direto na ESP32**, usa o Visual Studio que já está instalado
e ocupa cerca de 250 MB. Combina com o resto do projeto, que já é .NET.
Contrapartida honesta: comunidade pequena, pouco material em português.

## O próximo passo, exatamente

### 1. Limpar o Arduino (recupera vários GB)

Feche o Arduino IDE. `Windows + R` → `%LOCALAPPDATA%\Arduino15` → apague as pastas
`tmp`, `staging` e `packages`. **Esvazie a Lixeira**, senão o espaço não volta.
Se não for mais usar o Arduino IDE, desinstale pelo Painel de Controle.

### 2. Liberar mais espaço, se necessário

PowerShell **como administrador**:

```powershell
powercfg /h off
```

Apaga o `hiberfil.sys` (mais ou menos o tamanho da RAM, tipicamente 8–16 GB).
Perde-se a hibernação; a suspensão continua funcionando.

### 3. Instalar as ferramentas do nanoFramework

No Visual Studio: `Extensões > Gerenciar Extensões`, buscar **nanoFramework** e instalar
a *.NET nanoFramework Extension*. Reiniciar o VS.

Depois, no PowerShell:

```powershell
dotnet tool install -g nanoff
```

### 4. Gravar o firmware nanoCLR na placa

```powershell
nanoff --update --platform esp32 --serialport COM6
```

⚠️ **Use `--platform esp32`, nunca um `--target` escolhido à mão.** A documentação afirma
que `ESP32_PSRAM_REV0` serve para qualquer variante da ESP32 — **nesta placa não serve.**

Gravar essa imagem numa ESP32-D0WD-V3 revisão 3.1 com PSRAM "undetermined" produz um
runtime que não sobe. E falha de um jeito traiçoeiro: o deploy do Visual Studio reporta
"1 bem sucedida", o depurador morre com *"The service provider has not been configured
yet"*, e o código simplesmente nunca executa — sem exceção, sem log, sem pista.

Com `--platform esp32` o nanoff detecta o chip e escolhe sozinho. Aqui ele escolheu
**`ESP32_REV3`**, versão 1.17.0.335.

Para conferir se há runtime vivo (feche o Visual Studio antes, ele toma a COM6 em
exclusivo):

```powershell
nanoff --nanodevice --serialport COM6 --devicedetails
```

O `--nanodevice` é obrigatório. Sem ele, o nanoff fala com o bootloader de fábrica e só
mostra as características do chip, o que não diz nada sobre o firmware.

Se travar conectando, segure o botão **BOOT** até a gravação começar.

Isso é feito **uma vez**. Depois, o Visual Studio implanta o código sozinho.

### 5. Criar o projeto da Etapa 2

`Arquivo > Novo > Projeto`, template **Blank Application (nanoFramework)**, nome
`Etapa2Pisca`, local `LOJA AUTÔNOMA PRO\firmware`.

Adicione o pacote NuGet **`nanoFramework.System.Device.Gpio`** — sem ele o
`GpioController` não existe.

Substitua o `Program.cs` gerado pelo conteúdo de
`firmware/referencia/Etapa2Pisca-Program.cs`.

Confira em `Exibir > Other Windows > Device Explorer` se a placa aparece. Então F5.

**Resultado esperado:** os dois LEDs piscando juntos (o azul da placa e o da protoboard),
meio segundo cada, e as mensagens aparecendo na janela de Saída do Visual Studio.
A tabela de diagnóstico está comentada no final do próprio `Program.cs`.

### Como está organizada a pasta `firmware`

| Pasta | O que é |
|---|---|
| `Etapa2Pisca` | pisca o LED azul (GPIO2) — o "hello world" da placa |
| `Etapa3Rfid2` | RC522 lendo cartão. O `2` no nome é porque o primeiro foi criado com o template errado |
| `Etapa4Oled` | acende e apaga a tela do OLED |
| `referencia` | **os `Program.cs` de todas as etapas, inclusive as apagadas** |

A pasta `referencia` é a fonte de verdade. Cada arquivo lá tem, comentada no fim, a
tabela de sinais do LED azul para diagnóstico — o que cada padrão de pisca significa e
em que ordem suspeitar dos fios. Se um projeto se perder, recriar é questão de criar um
Blank Application, instalar os pacotes e colar o arquivo.

Projetos que existiram e foram apagados, e podem ser recriados a partir da `referencia`:

- **`Etapa5Pir`** — o PIR foi desmontado por falta de jumper macho-fêmea.
  O código está em `referencia/Etapa5Pir-Program.cs`, já com o aviso do VIN.
- **`etapa2_pisca`** — o `.ino` do caminho Arduino, abandonado.
- **`Etapa3Rfid`** — criado com o template de teste unitário por engano.

## Pinagem (para consulta rápida)

| Componente | Pino | ESP32 |
|---|---|---|
| RC522 | SDA / SS | D21 (GPIO21) |
| RC522 | SCK | D18 (GPIO18) |
| RC522 | MOSI | D23 (GPIO23) |
| RC522 | MISO | D19 (GPIO19) |
| RC522 | RST | D22 (GPIO22) |
| RC522 | IRQ | não conecta |
| OLED | SDA | D4 (GPIO4) |
| OLED | SCL | D5 (GPIO5) |
| PIR | OUT | RX2 (GPIO16) |
| LED | anodo | TX2 (GPIO17) |

⚠️ **O OLED está em GPIO4/GPIO5**, não no 21/22 dos exemplos da internet — o 21 e o 22
foram para o RC522. É obrigatório chamar `Wire.begin(4, 5);` antes de inicializar o display.

⚠️ **PIR em 3,3 V**: o HC-SR501 pede 4,5 V ou mais. Se não disparar, mover o VCC dele do
trilho `+` para o pino **VIN** do ESP32.

⚠️ **No nanoFramework a pinagem não é automática.** O SPI1 e o I2C1 têm pinos padrão
diferentes dos nossos. Antes de abrir o barramento, é obrigatório remapear com
`nanoFramework.Hardware.Esp32`:

```csharp
using nanoFramework.Hardware.Esp32;

// RC522 — SPI1
Configuration.SetPinFunction(18, DeviceFunction.SPI1_CLOCK);
Configuration.SetPinFunction(23, DeviceFunction.SPI1_MOSI);
Configuration.SetPinFunction(19, DeviceFunction.SPI1_MISO);
// SS (21) e RST (22) são GPIO comum, tratados pelo driver

// OLED — I2C1
Configuration.SetPinFunction(4, DeviceFunction.I2C1_DATA);
Configuration.SetPinFunction(5, DeviceFunction.I2C1_CLOCK);
```

É o equivalente ao `Wire.begin(4, 5)` do Arduino, só que vale também para o SPI.
Chamar **antes** de instanciar `SpiDevice` ou `I2cDevice` — depois não tem efeito.

Pacotes NuGet: `nanoFramework.Iot.Device.Mfrc522` (RC522) e
`nanoFramework.Iot.Device.Ssd13xx` (OLED). Sempre junto com `nanoFramework.Hardware.Esp32`.

⚠️ **Texto no OLED exige um arquivo de fonte que não vem no pacote.** A classe
`BasicFont` que aparece nos exemplos mora no projeto de amostra da biblioteca
(`Iot.Device.Ssd13xx.Samples`), não na DLL do NuGet. Para escrever texto será preciso
copiar `BasicFont.cs` do repositório para o projeto, ou gerar uma fonte com a
ferramenta `IotByteFont`. Sem isso, só desenho — `DrawFilledRectangle`, `DrawPixel`.

⚠️ **Instale sempre pelo pacote de mais alto nível e deixe as dependências vindo
sozinhas.** Instalar `System.Device.Gpio` ou `System.Device.Spi` à mão traz versões mais
novas que as usadas pelos bindings, e o nanoFramework resolve assembly por versão exata.

## O que já foi alterado no projeto .NET

Tudo já compilado e funcionando:

- **QR code**: validade reduzida de 10 para **5 minutos** (`StoreSession.QrCodeValidityMinutes`)
- **Sessão abandonada**: novo `StoreSession.TryExpire()` — sessão `Aberta` sem checkout por
  60 min vira `Cancelada` sozinha. Foi o que causava o "você já está dentro da loja" ao logar.
- **Confirmação de entrada**: agora é `POST /api/sessions/confirm-entry` recebendo
  `{ "qrCodeToken": "..." }`. O endpoint antigo por Id **não existe mais** — saber o Id
  não abre mais a porta.
- **AdminApp**: nova coluna **Tag RFID** na tela de Produtos, ligada ao
  `PATCH /api/products/{id}/rfid-tag`. Com trava contra vincular o mesmo chip a dois produtos.

## Pendências conhecidas

- Limpar o `Arduino15` e instalar as ferramentas do nanoFramework
- Comprar 3 jumpers macho-fêmea para o PIR
- ~~A API só escuta em `localhost`~~ — **metade resolvida.** Foi criado o perfil
  **`http-rede (ESP32)`** no `launchSettings.json`, escutando em `http://0.0.0.0:5071`.
  Os perfis `http` e `https` continuam intactos para o uso normal; escolha o novo no
  dropdown ao lado do botão de play quando for testar com a placa.

  Falta ainda, e só precisa ser feito uma vez:

  ```powershell
  # PowerShell como ADMINISTRADOR
  New-NetFirewallRule -DisplayName "AutonomousStore API 5071" -Direction Inbound `
    -Protocol TCP -LocalPort 5071 -Action Allow -Profile Private
  ```

  E descobrir o IP da máquina na rede, que é o endereço que o ESP32 vai usar:

  ```powershell
  ipconfig | Select-String "IPv4"
  ```

  Sempre por **HTTP**, nunca HTTPS: o certificado de desenvolvimento é autoassinado e
  o ESP32 recusa. O `-Profile Private` na regra de firewall vale só para redes
  domésticas — se o Windows classificar sua Wi-Fi como pública, a regra não pega.
- Espaço em disco: manter pelo menos 15 GB livres no C:. Abaixo disso o Windows não
  consegue expandir o arquivo de paginação, e isso aparece como `OutOfMemoryException`
  no Visual Studio — foi o que derrubou a WebApi mais cedo.
