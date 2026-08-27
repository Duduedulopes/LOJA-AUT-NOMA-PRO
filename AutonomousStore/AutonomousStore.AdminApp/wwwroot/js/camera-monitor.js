// Liga a webcam, compara quadros periodicamente, e avisa o Blazor (via dotNetRef)
// só quando detecta uma mudança de verdade — evita gastar chamadas ao Gemini à toa.
window.shelfMonitor = {
    video: null,
    canvas: null,
    ctx: null,
    lastFrameData: null,
    lastFrameBase64: null,
    intervalId: null,
    dotNetRef: null,
    busy: false,

    // As câmeras que este navegador enxerga. O rótulo só vem depois que o
    // usuário deu permissão ao site — antes disso a lista existe mas é anônima,
    // e por isso a página mostra "Câmera 1", "Câmera 2" até alguém permitir.
    async listar() {
        try {
            if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
                return [];
            }
            const todos = await navigator.mediaDevices.enumerateDevices();
            return todos
                .filter(d => d.kind === 'videoinput')
                .map((d, i) => ({ id: d.deviceId, nome: d.label || ('Câmera ' + (i + 1)) }));
        } catch {
            return [];
        }
    },

    // Devolve null quando deu certo, ou uma frase dizendo o que impediu.
    //
    // Antes isto lançava exceção, e exceção vinda do JS chega no Blazor como
    // "Ocorreu um erro inesperado" — a tela mais inútil que existe, porque
    // esconde justamente a informação que o navegador já sabia.
    async start(videoId, canvasId, dotNetRef, intervalMs, deviceId) {
        try {
            // SEMPRE largar o que ficou de antes. Uma tentativa que falhou no
            // meio deixava a camera aberta e sem ninguem para fechar — e a
            // tentativa seguinte disputava o dispositivo com ela mesma.
            // O sintoma enganava: parecia "outro programa esta usando".
            this.stop();

            this.video = document.getElementById(videoId);
            this.canvas = document.getElementById(canvasId);
            if (!this.video || !this.canvas) {
                return "Não encontrei o elemento de vídeo na página.";
            }

            // O navegador só entrega câmera em página segura. Fora disso
            // navigator.mediaDevices nem sequer existe, e o erro que aparece
            // é um "undefined" que não ajuda ninguém a descobrir o motivo.
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                if (!window.isSecureContext) {
                    return "A câmera só funciona em HTTPS ou em localhost. "
                        + "Esta página foi aberta em " + window.location.origin
                        + ". Abra pelo endereço localhost, ou publique em HTTPS.";
                }
                return "Este navegador não oferece acesso à câmera.";
            }

            this.ctx = this.canvas.getContext('2d', { willReadFrequently: true });
            this.dotNetRef = dotNetRef;
            this.lastFrameData = null;
            this.lastFrameBase64 = null;
            this.busy = false;

            // COM deviceId, abre a camera ESCOLHIDA. Sem ele, abre a padrao
            // do sistema — que nem sempre e a que serve. Medido em 20/08:
            // das seis cameras deste PC, a padrao era a unica que falhava.
            const restricao = deviceId ? { deviceId: { exact: deviceId } } : true;

            let stream;
            try {
                stream = await navigator.mediaDevices.getUserMedia({ video: restricao });
            } catch (err) {
                return this.explicar(err);
            }

            this.video.srcObject = stream;
            await this.video.play();

            this.intervalId = setInterval(() => this.checkFrame(), intervalMs || 2500);
            return null;
        } catch (err) {
            return "Falha ao iniciar a câmera: " + (err && err.message ? err.message : err);
        }
    },

    // O navegador já classificou a falha em `err.name`. Traduzir isso custa
    // dez linhas e economiza uma tarde de tentativa e erro.
    explicar(err) {
        const nome = err && err.name ? err.name : "";
        switch (nome) {
            case "NotAllowedError":
            case "PermissionDeniedError":
                return "Permissão de câmera negada. Clique no cadeado ao lado do "
                     + "endereço e libere a câmera para este site.";
            case "NotFoundError":
            case "DevicesNotFoundError":
                return "Nenhuma câmera encontrada neste computador.";
            case "NotReadableError":
            case "TrackStartError":
                return "A câmera existe mas está ocupada por outro programa. "
                     + "Feche quem estiver usando ela (Teams, Zoom, OBS, ou outro "
                     + "programa de visão) e tente de novo.";
            case "OverconstrainedError":
                return "Nenhuma câmera atende ao formato pedido.";
            case "SecurityError":
                return "O navegador bloqueou o acesso à câmera por segurança.";
            default:
                return "Não consegui abrir a câmera (" + (nome || "erro desconhecido") + ").";
        }
    },

    checkFrame() {
        if (this.busy || !this.video || this.video.readyState < 2) return;

        this.canvas.width = this.video.videoWidth;
        this.canvas.height = this.video.videoHeight;
        this.ctx.drawImage(this.video, 0, 0, this.canvas.width, this.canvas.height);

        const currentData = this.ctx.getImageData(0, 0, this.canvas.width, this.canvas.height);
        const currentBase64 = this.canvas.toDataURL('image/jpeg', 0.7);

        if (this.lastFrameData) {
            const diffRatio = this.computeDiff(this.lastFrameData.data, currentData.data);

            if (diffRatio > 0.035) {
                this.busy = true;
                const before = this.lastFrameBase64;
                this.dotNetRef.invokeMethodAsync('OnShelfChangeDetected', before, currentBase64)
                    .finally(() => { this.busy = false; });
            }
        }

        this.lastFrameData = currentData;
        this.lastFrameBase64 = currentBase64;
    },

    computeDiff(a, b) {
        let diffCount = 0;
        const totalPixels = a.length / 4;
        // Amostra 1 a cada 4 pixels pra performance, sem perder sensibilidade real.
        for (let i = 0; i < a.length; i += 16) {
            const dr = Math.abs(a[i] - b[i]);
            const dg = Math.abs(a[i + 1] - b[i + 1]);
            const db = Math.abs(a[i + 2] - b[i + 2]);
            if ((dr + dg + db) > 60) diffCount++;
        }
        return diffCount / (totalPixels / 4);
    },

    stop() {
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
        if (this.video && this.video.srcObject) {
            this.video.srcObject.getTracks().forEach(t => t.stop());
            this.video.srcObject = null;
        }
        this.lastFrameData = null;
        this.lastFrameBase64 = null;
    }
};