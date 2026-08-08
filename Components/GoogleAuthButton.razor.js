export class GoogleAuth {
    constructor(clientId, callback) {
        this.clientId = clientId;

        google.accounts.id.initialize({
            client_id: clientId,
            auto_select: true,
            callback: callback
        });
    }

    renderButton(selector, isDarkMode) {
        const element = document.querySelector(selector);

        if (!element)
            return;

        google.accounts.id.prompt((notification) => {
            if (notification.isNotDisplayed() || notification.isSkippedMoment()) {
                // continue with another identity provider.
            }
        });

        google.accounts.id.renderButton(element, {
            type: "standard",
            shape: "pill",
            theme: isDarkMode ? "outline_dark" : "outline",
            size: "medium",
            text: "signin",
            locale: "en_US"
        });
    }

    refreshToken() {
        google.accounts.id.prompt((notification) => {
            if (notification.isNotDisplayed() || notification.isSkippedMoment()) {
                console.error("Silent token refresh skipped or not displayed.");
            }
        });
    }
}

export function createGoogleAuth(clientId, dotnetHelper) {
    const callback = (response) => {
        dotnetHelper.invokeMethodAsync('OnGoogleSignInAsync', response.credential);
    };

    return new GoogleAuth(clientId, callback);
}

export class ActivityTracker {
    constructor(callback, throttleDelay = 30000) {
        this.callback = callback;
        this.throttleDelay = throttleDelay;
        this.lastChecked = 0;

        this.handleActivity = this.handleActivity.bind(this);

        window.addEventListener('mousemove', this.handleActivity);
        window.addEventListener('keypress', this.handleActivity);
        window.addEventListener('click', this.handleActivity);
        window.addEventListener('scroll', this.handleActivity);
    }

    handleActivity() {
        const now = Date.now();
        const passedTime = now - this.lastChecked;
        
        if (passedTime < this.throttleDelay)
            return;

        this.lastChecked = now;

        if (typeof this.callback === 'function') {
            this.callback();
        }
    }

    dispose() {
        window.removeEventListener('mousemove', this.handleActivity);
        window.removeEventListener('keypress', this.handleActivity);
        window.removeEventListener('click', this.handleActivity);
        window.removeEventListener('scroll', this.handleActivity);
        this.callback = null;
    }
}

export function createActivityTracker(dotnetHelper, throttleDelay = 30000) {
    const callback = () => dotnetHelper.invokeMethodAsync('OnActivityAsync');
    return new ActivityTracker(callback, throttleDelay);
}
