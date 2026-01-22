// Manejo de login con Google
window.loginWithGoogle = async function () {
    try {
        const client = google.accounts.oauth2.initTokenClient({
            client_id: 'TU_CLIENT_ID_GOOGLE',
            scope: 'email profile',
            callback: async (response) => {
                if (response.access_token) {
                    // Obtener información del usuario
                    const userInfo = await getUserInfo(response.access_token);

                    // Enviar a Blazor
                    DotNet.invokeMethodAsync('SpiderHood', 'HandleGoogleLogin', {
                        email: userInfo.email,
                        name: userInfo.name,
                        token: response.access_token
                    });
                }
            }
        });

        client.requestAccessToken();
    } catch (error) {
        console.error('Google login error:', error);
        DotNet.invokeMethodAsync('SpiderHood', 'HandleSocialLoginError', 'Error al iniciar sesión con Google');
    }
};

async function getUserInfo(accessToken) {
    const response = await fetch('https://www.googleapis.com/oauth2/v3/userinfo', {
        headers: {
            'Authorization': `Bearer ${accessToken}`
        }
    });

    if (!response.ok) {
        throw new Error('Failed to get user info');
    }

    return await response.json();
}

// Manejo de login con Facebook
window.loginWithFacebook = function () {
    try {
        FB.login(function (response) {
            if (response.authResponse) {
                // Obtener información del usuario
                FB.api('/me', { fields: 'name,email' }, function (userInfo) {
                    DotNet.invokeMethodAsync('SpiderHood', 'HandleFacebookLogin', {
                        email: userInfo.email,
                        name: userInfo.name,
                        token: response.authResponse.accessToken
                    });
                });
            } else {
                DotNet.invokeMethodAsync('SpiderHood', 'HandleSocialLoginError', 'El usuario canceló el login de Facebook');
            }
        }, { scope: 'email' });
    } catch (error) {
        console.error('Facebook login error:', error);
        DotNet.invokeMethodAsync('SpiderHood', 'HandleSocialLoginError', 'Error al iniciar sesión con Facebook');
    }
};

// Mostrar mensajes de éxito
window.showSuccessMessage = function (title, message) {
    alert(`${title}\n\n${message}`);
};