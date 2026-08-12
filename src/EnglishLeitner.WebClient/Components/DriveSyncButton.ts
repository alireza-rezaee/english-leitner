import type { DotNet } from "@microsoft/dotnet-js-interop";
import type { drive_v3 } from "googleapis";

export class DriveSync {
    private readonly wasmReference: DotNet.DotNetObject;
    private readonly uploadMultipartUrl =
        "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart";

    constructor(wasmReference: DotNet.DotNetObject) {
        this.wasmReference = wasmReference;
    }

    async notifySyncSuccededAsync(): Promise<void> {
        await this.wasmReference.invokeMethodAsync('OnJSSyncSuccededAsync');
    }

    async notifySaveFailedAsync(error: Error): Promise<void> {
        await this.wasmReference.invokeMethodAsync(
            "OnJSSyncFailedAsync",
            error.message
        );
    }

    async saveAsync(token: string): Promise<void> {
        const fileContent = '[]';

        const metadata: Pick<drive_v3.Schema$File, "name" | "parents"> = {
            name: 'data.json',
            parents: ['appDataFolder']
        };

        const file = new Blob([fileContent], {
            type: 'application/json'
        });
        const metadataFile = new Blob([JSON.stringify(metadata)], {
            type: 'application/json'
        });

        const form = new FormData();
        form.append('metadata', metadataFile);
        form.append('file', file);
        
        try {
            const response = await fetch(this.uploadMultipartUrl, {
                method: 'POST',
                headers: new Headers({
                    'Authorization': 'Bearer ' + token
                }),
                body: form
            });

            if (!response.ok) {
                const responseText = await response.text();
                throw new Error(
                    `Upload failed (${response.status}): ${responseText}`
                );
            }

            // const content = (await response.json()) as drive_v3.Schema$File;
            await this.notifySyncSuccededAsync()

        } catch (error: unknown) {
            const normalizedError = error instanceof Error ? error : new Error(String(error));
            console.error('Error:', normalizedError);
            await this.notifySaveFailedAsync(normalizedError);
        }
    }
}
