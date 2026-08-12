import type { DotNet } from "@microsoft/dotnet-js-interop";

export class GoogleAuth {
    private readonly clientId: string;
    private readonly scopes: string;
    private readonly wasmReference: DotNet.DotNetObject;
    private tokenClient: google.accounts.oauth2.TokenClient | null;
    private accessToken: string;
    private expiresIn: number;
    private readonly gisScriptSrc = "https://accounts.google.com/gsi/client";

    constructor(
        clientId: string,
        scopes: string,
        wasmReference: DotNet.DotNetObject,
        existsToken: string
    ) {
        this.clientId = clientId;
        this.scopes = scopes;
        this.wasmReference = wasmReference;
        this.tokenClient = null;
        this.accessToken = existsToken;
        this.expiresIn = 0;
    }

    async notifyUserLoginAsync(): Promise<void> {
        await this.wasmReference.invokeMethodAsync(
            "OnJSUserLoginAsync",
            this.accessToken,
            this.expiresIn
        );
    }

    async notifyUserLogoutAsync(): Promise<void> {
        await this.wasmReference.invokeMethodAsync("OnJSUserLogoutAsync");
    }

    async googlePopupClosedAsync(): Promise<void> {
        await this.wasmReference.invokeMethodAsync("OnJSGooglePopupClosedAsync");
    }

    loadGSILibraryAsync(): Promise<HTMLScriptElement> {
        return new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.type = "text/javascript";
            script.src = this.gisScriptSrc;
            script.onload = () => resolve(script);
            script.onerror = () =>
                reject(new Error(`Failed to load script: ${this.gisScriptSrc}`));
            document.head.appendChild(script);
        });
    }

    initTokenClient(): void {
        this.tokenClient = google.accounts.oauth2.initTokenClient({
            client_id: this.clientId,
            scope: this.scopes,
            callback: async (response) => {
                this.accessToken = response.access_token;
                this.expiresIn = parseInt(response.expires_in);
                await this.notifyUserLoginAsync();
                await this.googlePopupClosedAsync();
            },
            error_callback: async () => {
                await this.googlePopupClosedAsync();
            },
        });
    }

    authorize(): void {
        this.tokenClient?.requestAccessToken({
            prompt: "",
        });
    }

    revoke(): void {
        google.accounts.oauth2.revoke(this.accessToken, async () => {
            this.expiresIn = 0;
            await this.notifyUserLogoutAsync();
        });
    }
}
