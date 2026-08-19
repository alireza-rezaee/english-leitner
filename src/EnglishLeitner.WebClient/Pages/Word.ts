export class Word {
    private sounds: Record<string, HTMLAudioElement>;

    constructor() {
        this.sounds = {};
    }

    public play(src: string): void {
        if (!this.sounds[src]) {
            const audio = new Audio(src);
            audio.preload = "auto";
            this.sounds[src] = audio;
        }

        const audio = this.sounds[src];

        audio.currentTime = 0;

        audio.play().catch((error: unknown) => {
            console.error(`Audio could not be played: ${src}`, error);
        });
    }
}
