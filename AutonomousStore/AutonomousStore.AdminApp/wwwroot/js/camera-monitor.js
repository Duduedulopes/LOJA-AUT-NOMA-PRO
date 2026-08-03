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

    async start(videoId, canvasId, dotNetRef, intervalMs) {
        this.video = document.getElementById(videoId);
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d', { willReadFrequently: true });
        this.dotNetRef = dotNetRef;
        this.lastFrameData = null;
        this.lastFrameBase64 = null;
        this.busy = false;

        const stream = await navigator.mediaDevices.getUserMedia({ video: true });
        this.video.srcObject = stream;
        await this.video.play();

        this.intervalId = setInterval(() => this.checkFrame(), intervalMs || 2500);
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