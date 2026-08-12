"use strict";

export class DriveSync {
    constructor(wasmReference) {
        Object.defineProperty(this, "WASM_REFERENCE", { value: wasmReference, writable: false });
        Object.defineProperty(this, "UPLOAD_MULTIPART_URL", { value: "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart", writable: false });
    }

    async NotifySyncSuccededAsync() {
        await this.WASM_REFERENCE.invokeMethodAsync('OnJSSyncSuccededAsync');
    }

    async NotifySaveFailedAsync(error) {
        await this.WASM_REFERENCE.invokeMethodAsync('OnJSSyncFailedAsync', error.message);
    }

    async saveAsync(token) {
        const fileContent = '[]';
        
        const metadata = {
            name: 'data.json',
            parents: ['appDataFolder']
        };
        
        const file = new Blob([fileContent], { type: 'application/json' });
        const metadataFile = new Blob([JSON.stringify(metadata)], { type: 'application/json' });

        const form = new FormData();
        form.append('metadata', metadataFile);
        form.append('file', file);

        try {
            let response = await fetch(this.UPLOAD_MULTIPART_URL, {
                method: 'POST',
                headers: new Headers({ 'Authorization': 'Bearer ' + token }),
                body: form
            });

            let content = await response.json();
            await this.NotifySyncSuccededAsync()

        } catch (error) {
            console.error('Error:', error);
            await this.NotifySaveFailedAsync(error);
        }
    }
}
