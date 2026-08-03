// Em desenvolvimento (dotnet run), este service worker não faz cache de nada,
// pra você sempre ver a versão mais recente do app. O arquivo service-worker.published.js
// é o que entra em ação quando você publica o app (aí sim com cache offline).
self.addEventListener('fetch', () => { });
