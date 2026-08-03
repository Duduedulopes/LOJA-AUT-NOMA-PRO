// Renderiza um QR code dentro do elemento com o id informado, usando a lib qrcode.js (carregada no index.html).
window.renderQrCode = (elementId, text) => {
    const container = document.getElementById(elementId);
    if (!container) return;

    container.innerHTML = '';
    new QRCode(container, {
        text: text,
        width: 220,
        height: 220
    });
};
