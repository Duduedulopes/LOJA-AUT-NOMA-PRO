/* ============================================================
   Smart Store — leitor RFID da prateleira
   ESP32 + MFRC522 (RC522)
   ------------------------------------------------------------
   Lê a tag, envia o UID para o Worker, e o produto aparece no
   carrinho do cliente que está na loja.

   Bibliotecas necessárias (Gerenciador de Bibliotecas do Arduino):
     - MFRC522   (por GithubCommunity)
   O resto já vem com o core do ESP32.

   Ligação RC522 → ESP32
     SDA  (SS)  → GPIO 5
     SCK        → GPIO 18
     MOSI       → GPIO 23
     MISO       → GPIO 19
     RST        → GPIO 27
     GND        → GND
     3.3V       → 3.3V     ATENÇÃO: nunca 5V, queima o módulo

   Um LED no GPIO 2 (o embutido na maioria das placas) pisca a
   cada leitura: verde curto = enviado, três piscadas = erro.
   ============================================================ */

#include <WiFi.h>
#include <HTTPClient.h>
#include <SPI.h>
#include <MFRC522.h>

/* ---------- CONFIGURE AQUI ---------- */
const char* WIFI_NOME  = "SUA_REDE_WIFI";
const char* WIFI_SENHA = "SUA_SENHA";

const char* URL_WORKER = "https://smart-store-porta.contato-dudulopes.workers.dev/produto";

/* ---------- pinos ---------- */
#define PINO_SS   5
#define PINO_RST  27
#define PINO_LED  2

MFRC522 leitor(PINO_SS, PINO_RST);

/* evita reenviar a mesma tag se ela ficar encostada */
String ultimaTag = "";
unsigned long ultimaEm = 0;
const unsigned long ESPERA_MS = 3000;

/* ============================================================ */

void setup() {
  Serial.begin(115200);
  delay(400);

  pinMode(PINO_LED, OUTPUT);
  digitalWrite(PINO_LED, LOW);

  SPI.begin();
  leitor.PCD_Init();
  delay(50);

  Serial.println();
  Serial.println("=== Smart Store — leitor RFID ===");
  leitor.PCD_DumpVersionToSerial();

  conectarWiFi();
  Serial.println("Pronto. Aproxime uma tag.");
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    conectarWiFi();
    return;
  }

  /* nenhuma tag por perto */
  if (!leitor.PICC_IsNewCardPresent()) return;
  if (!leitor.PICC_ReadCardSerial())   return;

  String tag = uidParaTexto(leitor.uid.uidByte, leitor.uid.size);
  leitor.PICC_HaltA();
  leitor.PCD_StopCrypto1();

  /* mesma tag encostada há pouco? ignora */
  unsigned long agora = millis();
  if (tag == ultimaTag && (agora - ultimaEm) < ESPERA_MS) return;
  ultimaTag = tag;
  ultimaEm  = agora;

  Serial.println("Tag lida: " + tag);
  enviarTag(tag);
}

/* ============================================================ */

String uidParaTexto(byte* buffer, byte tamanho) {
  String s = "";
  for (byte i = 0; i < tamanho; i++) {
    if (buffer[i] < 0x10) s += "0";
    s += String(buffer[i], HEX);
  }
  s.toUpperCase();
  return s;
}

void conectarWiFi() {
  Serial.print("Conectando em ");
  Serial.print(WIFI_NOME);

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_NOME, WIFI_SENHA);

  int tentativas = 0;
  while (WiFi.status() != WL_CONNECTED && tentativas < 40) {
    delay(500);
    Serial.print(".");
    tentativas++;
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.println();
    Serial.print("Conectado. IP: ");
    Serial.println(WiFi.localIP());
    piscar(2, 120);
  } else {
    Serial.println();
    Serial.println("Falhou. Tentando de novo em 5s.");
    delay(5000);
  }
}

void enviarTag(String tag) {
  HTTPClient http;
  http.begin(URL_WORKER);
  http.addHeader("Content-Type", "application/json");
  http.setTimeout(8000);

  String corpo = "{\"tag\":\"" + tag + "\"}";
  int codigo = http.POST(corpo);
  String resposta = http.getString();
  http.end();

  Serial.print("HTTP ");
  Serial.print(codigo);
  Serial.print("  ");
  Serial.println(resposta);

  if (codigo == 200) {
    piscar(1, 90);                       /* enviado */
  } else if (codigo == 409) {
    Serial.println(">> Nenhum cliente na loja. Passe o QR na porta primeiro.");
    piscar(2, 300);
  } else {
    piscar(3, 100);                      /* erro */
  }
}

void piscar(int vezes, int ms) {
  for (int i = 0; i < vezes; i++) {
    digitalWrite(PINO_LED, HIGH);
    delay(ms);
    digitalWrite(PINO_LED, LOW);
    delay(ms);
  }
}
