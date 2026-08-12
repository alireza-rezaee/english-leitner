export class GoogleAuth {
    constructor(clientId, scopes, wasmReference, existsToken) {
        this.clientId = clientId;
        this.scopes = scopes;
        this.wasmReference = wasmReference;
        this.tokenClient = null;
        this.accessToken = existsToken;
        this.expiresIn = 0;
        this.gisScriptSrc = 'https://accounts.google.com/gsi/client';
    }

    async NotifyUserLoginAsync() {
        await this.wasmReference.invokeMethodAsync('NotifyUserLoginAsync', this.accessToken, this.expiresIn);
    }

    async NotifyUserLogoutAsync() {
        await this.wasmReference.invokeMethodAsync('NotifyUserLogoutAsync');
    }

    async GooglePopupClosedAsync() {
        await this.wasmReference.invokeMethodAsync('GooglePopupClosedAsync');
    }

    loadGSILibraryAsync() {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.type = 'text/javascript';
            script.src = this.gisScriptSrc;
            script.onload = () => resolve(script);
            script.onerror = () => reject(new Error(`Failed to load script: ${url}`));
            document.head.appendChild(script);
        });
    }

    initTokenClient() {
        this.tokenClient = google.accounts.oauth2.initTokenClient({
            client_id: this.clientId,
            scope: this.scopes,
            callback: async (response) => {
                // docs: https://developers.google.com/identity/oauth2/web/reference/js-reference#TokenResponse
                this.accessToken = response.access_token;
                this.expiresIn = response.expires_in;
                await this.NotifyUserLoginAsync();
                await this.GooglePopupClosedAsync();
            },
            error_callback: async (response) => {
                // docs: https://developers.google.com/identity/oauth2/web/reference/js-reference#TokenClientConfig
                await this.GooglePopupClosedAsync();
            }
        });
    }

    authorize() {
        this.tokenClient.requestAccessToken({
            prompt: "",
        });
    }

    revoke() {
        google.accounts.oauth2.revoke(this.accessToken, async (response) => {
            // docs: https://developers.google.com/identity/oauth2/web/reference/js-reference#RevocationResponse
            this.expiresIn = 0;
            await this.NotifyUserLogoutAsync();
        });
    }
}
