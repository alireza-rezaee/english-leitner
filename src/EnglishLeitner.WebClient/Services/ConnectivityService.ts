import type { DotNet } from "@microsoft/dotnet-js-interop";

export class ConnectivityService {
    private readonly wasmReference: DotNet.DotNetObject;
    private readonly endpoint: string = "https://www.googleapis.com/generate_204";
    private readonly interval: number = 0;

    private intervalId: ReturnType<typeof setInterval> | undefined;
    private isListening = false;
    private isDisposed = false;

    private readonly connectionChangedHandler = async (): Promise<void> => {
        if (this.isDisposed)
            return;

        const status = await this.checkStatusAsync();

        if (!this.isDisposed)
            await this.notifyAsync(status);
    };

    constructor(
        wasmReference: DotNet.DotNetObject,
        interval: number = 30_000
    ) {
        this.wasmReference = wasmReference;
        this.interval = interval;
    }

    async listenAsync(): Promise<void> {
        if (this.isDisposed || this.isListening)
            return;

        this.isListening = true;
        this.intervalId = setInterval(
            this.connectionChangedHandler,
            this.interval
        );

        window.addEventListener(
            "online",
            this.connectionChangedHandler
        );

        window.addEventListener(
            "offline",
            this.connectionChangedHandler
        );
    }

    async checkStatusAsync(): Promise<ConnectivityStatus> {
        try {
            if (navigator.onLine) {
                try {
                    await fetch(this.endpoint, {
                        method: "GET",
                        mode: "no-cors",
                        cache: "no-store",
                    });

                    return ConnectivityStatus.Connected;
                } catch {
                    return ConnectivityStatus.NoInternetNetwork;
                }
            }

            return ConnectivityStatus.Offline;
        } catch (error: unknown) {
            const normalizedError =
                error instanceof Error
                    ? error
                    : new Error(String(error));

            console.error(
                "Internet Connectivity Error:",
                normalizedError
            );

            return ConnectivityStatus.Offline;
        }
    }

    async notifyAsync(status: ConnectivityStatus): Promise<void> {
        if (this.isDisposed)
            return;

        await this.wasmReference.invokeMethodAsync(
            "OnJSConnectionStatusChanged",
            status
        );
    }

    dispose(): void {
        if (this.isDisposed)
            return;

        this.isDisposed = true;
        this.isListening = false;

        if (this.intervalId !== undefined) {
            clearInterval(this.intervalId);
            this.intervalId = undefined;
        }

        window.removeEventListener(
            "online",
            this.connectionChangedHandler
        );

        window.removeEventListener(
            "offline",
            this.connectionChangedHandler
        );
    }
}

enum ConnectivityStatus {
    Offline = 0,
    NoInternetNetwork = 1,
    Connected = 2,
}
