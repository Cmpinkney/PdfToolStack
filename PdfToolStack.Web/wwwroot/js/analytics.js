(function () {
    const allowedEvents = new Set([
        'page_view',
        'tool_started',
        'tool_completed',
        'file_downloaded',
        'pricing_viewed'
    ]);

    const allowedParameterNames = new Set([
        'page_location',
        'page_path',
        'page_title',
        'tool_name'
    ]);

    function sanitizeParameters(parameters) {
        const sanitized = {};

        if (!parameters || typeof parameters !== 'object') {
            return sanitized;
        }

        Object.keys(parameters).forEach((key) => {
            if (!allowedParameterNames.has(key)) {
                return;
            }

            const value = parameters[key];
            if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
                sanitized[key] = value;
            }
        });

        return sanitized;
    }

    function track(eventName, parameters) {
        if (!allowedEvents.has(eventName) || typeof window.gtag !== 'function') {
            return;
        }

        window.gtag('event', eventName, sanitizeParameters(parameters));
    }

    window.pdfToolStackAnalytics = {
        track,
        trackPageView: function () {
            track('page_view', {
                page_location: window.location.origin + window.location.pathname,
                page_path: window.location.pathname,
                page_title: document.title
            });
        },
        trackToolStarted: function (toolName) {
            track('tool_started', { tool_name: toolName });
        },
        trackToolCompleted: function (toolName) {
            track('tool_completed', { tool_name: toolName });
        },
        trackFileDownloaded: function (toolName) {
            track('file_downloaded', { tool_name: toolName });
        },
        trackPricingViewed: function () {
            track('pricing_viewed', {
                page_location: window.location.origin + window.location.pathname,
                page_path: window.location.pathname,
                page_title: document.title
            });
        }
    };
})();
