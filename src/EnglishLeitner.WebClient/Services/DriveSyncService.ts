import type { DotNet } from "@microsoft/dotnet-js-interop";
import type { drive_v3 } from "googleapis";

export class DriveSyncService {
    private readonly filename: string = 'data.json';
    private readonly mimeType: string = 'application/json';
    private readonly wasmReference: DotNet.DotNetObject;
    private fileId: string | null | undefined = null;

    constructor(wasmReference: DotNet.DotNetObject) {
        this.wasmReference = wasmReference;
    }

    public async getRemoteCheckSumAsync(token: string): Promise<string | null> {
        this.fileId ??= await this.findFileIdByName(this.filename, token);
        const endpoint = `https://www.googleapis.com/drive/v3/files/${this.fileId}?fields=sha256Checksum`;
        const response = await fetch(endpoint, {
            headers: new Headers({
                'Authorization': `Bearer ${token}`
            })
        });

        if (!response.ok)
            throw new Error(await response.text());

        const responseBody = await response.json();
        const checksum = responseBody?.sha256Checksum as string | null;

        return checksum;
    }

    public async getRemoteDataAsync(token: string): Promise<string | null> {
        try {
            this.fileId ??= await this.findFileIdByName(this.filename, token);

            if (!this.fileId) {
                // await this.notifySyncFailedAsync("fileId is missed!");
                return null;
            }

            const endpoint = `https://www.googleapis.com/drive/v3/files/${this.fileId}?alt=media`;
            const response = await fetch(endpoint, {
                headers: new Headers({
                    'Authorization': `Bearer ${token}`
                })
            });

            if (!response.ok)
                throw new Error(await response.text());

            const responseText = await response.text();
            return responseText;
        } catch (error: unknown) {
            const normalizedError = error instanceof Error ? error : new Error(String(error));
            await this.notifySyncErrorAsync(normalizedError.message);
            return null;
        }
    }

    public async setRemoteDataAsync(fileText: string, token: string): Promise<string | null | undefined> {
        try {
            this.fileId ??= await this.findFileIdByName(this.filename, token);

            let checksum: string | null | undefined;
            if (this.fileId) {
                const file = await DriveSyncService.updateTextFileAsync(this.fileId, fileText, this.mimeType, token);
                checksum = file?.sha256Checksum;
            }
            else {
                const filename = 'data.json';
                const file = await DriveSyncService.createTextFileAsync(fileText, this.mimeType, filename, token);
                this.fileId = file?.id;
                checksum = file?.sha256Checksum;
            }

            return checksum;

        } catch (error: unknown) {
            const normalizedError = error instanceof Error ? error : new Error(String(error));
            console.error('Error:', normalizedError);
            await this.notifySyncErrorAsync(normalizedError.message);
            return null;
        }
    }

    private static async createTextFileAsync(fileText: string, mimeType: string, filename: string, token: string): Promise<drive_v3.Schema$File | null> {
        const metadata: Pick<drive_v3.Schema$File, "name" | "parents"> = {
            name: filename,
            parents: ['appDataFolder']
        };

        const file = new Blob([fileText], { type: mimeType });
        const metadataFile = new Blob([JSON.stringify(metadata)], { type: 'application/json' });

        const form = new FormData();
        form.append('metadata', metadataFile);
        form.append('file', file);

        const endpoint: string = 'https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,sha256Checksum';
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: new Headers({
                'Authorization': `Bearer ${token}`
            }),
            body: form
        });

        if (!response.ok) {
            const responseText = await response.text();
            throw new Error(`Upload failed (${response.status}): ${responseText}`);
        }

        const content = (await response.json()) as drive_v3.Schema$File;
        return content;
    }

    private static async updateTextFileAsync(fileId: string, fileText: string, mimeType: string, token: string): Promise<drive_v3.Schema$File | null> {
        const endpoint = `https://www.googleapis.com/upload/drive/v3/files/${fileId}?uploadType=media&fields=id,name,sha256Checksum`;
        const response = await fetch(endpoint, {
            method: "PATCH",
            headers: new Headers({
                'Authorization': `Bearer ${token}`,
                'Content-Type': `${mimeType}; charset=UTF-8`
            }),
            body: fileText
        });

        if (!response.ok) {
            throw new Error(await response.text());
        }

        const content = (await response.json()) as drive_v3.Schema$File;
        return content;
    }

    private async findFileIdByName(filename: string, token: string): Promise<string | null> {
        const safeName = filename
            .replace(/\\/g, "\\\\")
            .replace(/'/g, "\\'");

        const params = new URLSearchParams({
            spaces: "appDataFolder",
            q: `name = '${safeName}' and trashed = false`,
            fields: "files(id,name,mimeType)",
            pageSize: "1"
        });

        const response = await fetch(
            `https://www.googleapis.com/drive/v3/files?${params}`,
            {
                headers: new Headers({
                    'Authorization': `Bearer ${token}`
                })
            }
        );

        if (!response.ok)
            throw new Error(await response.text());

        const responseBody = await response.json();
        const files = responseBody.files as drive_v3.Schema$File[] | null;

        return files?.[0]?.id ?? null;
    }

    private async notifySyncErrorAsync(message: string): Promise<void> {
        await this.wasmReference.invokeMethodAsync(
            "OnJSSyncErrorAsync",
            message
        );
    }
}
