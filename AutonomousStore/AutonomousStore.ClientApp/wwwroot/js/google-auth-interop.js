// Inicializa o Google Identity Services e desenha o botão "Entrar com Google".
// Quando o usuário confirma o login no popup do Google, chama de volta o componente
// Blazor (OnGoogleCredential) passando o id_token pra validação no servidor.
window.initGoogleSignIn = (dotNetRef, clientId, buttonElementId) => {
    if (typeof google === 'undefined' || !google.accounts) {
        // A lib do Google ainda não carregou (script async) — tenta de novo em breve.
        setTimeout(() => window.initGoogleSignIn(dotNetRef, clientId, buttonElementId), 300);
        return;
    }

    google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => {
            dotNetRef.invokeMethodAsync('OnGoogleCredential', response.credential);
        }
    });

    google.accounts.id.renderButton(document.getElementById(buttonElementId), {
        theme: 'outline',
        size: 'large',
        width: 300,
        text: 'continue_with'
    });
};
