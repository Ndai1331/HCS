/* Your Global Scripts */
window.downloadFile = function (fileName, base64Content) {
    const link = document.createElement('a');
    link.href = 'data:application/octet-stream;base64,' + base64Content;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.copyToClipboard = function (text) {
    navigator.clipboard.writeText(text);
};

window.getDeviceType = () => {
    const ua = navigator.userAgent;

    if (/tablet|ipad|playbook|silk/i.test(ua)) {
        return "TABLET";
    }
    if (/mobile|iphone|ipod|android/i.test(ua)) {
        return "MOBILE";
    }
    return "DESKTOP";
};

// Hide Blazorise license banner (including shadow root content)
(function() {
    function hideLicenseBanner() {
        const el = document.querySelector("#blazorise-license-banner-host");
        if (el) {
            // Clear shadow root content
            if (el.shadowRoot) {
                el.shadowRoot.innerHTML = "";
            }
            
            // Also hide/remove the element itself
            el.style.display = 'none';
            el.style.visibility = 'hidden';
            el.style.height = '0';
            el.style.width = '0';
            el.style.overflow = 'hidden';
            el.style.opacity = '0';
            el.style.position = 'absolute';
            el.style.zIndex = '-9999';
        }
    }
    
    // Hide immediately if DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', hideLicenseBanner);
    } else {
        hideLicenseBanner();
    }
    
    // Also hide after delays to catch dynamically added banners
    setTimeout(hideLicenseBanner, 50);
    setTimeout(hideLicenseBanner, 100);
    setTimeout(hideLicenseBanner, 300);
    setTimeout(hideLicenseBanner, 500);
    setTimeout(hideLicenseBanner, 1000);
    setTimeout(hideLicenseBanner, 2000);
    
    // Use MutationObserver to hide banner if it's added dynamically
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            mutation.addedNodes.forEach(function(node) {
                if (node.nodeType === 1) { // Element node
                    if (node.id === 'blazorise-license-banner-host') {
                        hideLicenseBanner();
                    }
                    // Also check if banner is added as a child
                    const banner = node.querySelector && node.querySelector('#blazorise-license-banner-host');
                    if (banner) {
                        hideLicenseBanner();
                    }
                }
            });
        });
        hideLicenseBanner();
    });
    
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
    
    // Also observe document.documentElement for banner added at root level
    observer.observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();






/** Resize listener for Chat mobile layout — one DotNetObjectReference, disposed from Blazor */
window.HcChatMobileResize = {
    _handler: null,
    init: function (dotnetHelper) {
        this.dispose();
        var self = this;
        this._handler = function () {
            if (!dotnetHelper) {
                return;
            }
            var isMobile = window.innerWidth < 768;
            dotnetHelper.invokeMethodAsync('SetMobileMode', isMobile);
        };
        this._handler();
        window.addEventListener('resize', this._handler);
        this._dotnetHelper = dotnetHelper;
    },
    dispose: function () {
        if (this._handler) {
            window.removeEventListener('resize', this._handler);
        }
        this._handler = null;
        this._dotnetHelper = null;
    }
};

window.HcSurveyResultsLocationSelect2 = {
    init: function (selectId, dotnetHelper) {
        var $el = $("#" + selectId);
        if (!$el.length || typeof $el.select2 !== "function") {
            return;
        }

        if ($el.data("select2")) {
            $el.off(".hcSurveyResultsLocation");
            $el.select2("destroy");
        }

        $el.select2({
            width: "100%",
            allowClear: true,
            placeholder: "Chọn vị trí khảo sát",
            language: "vi"
        });

        $el.on("change.hcSurveyResultsLocation", function () {
            var value = $el.val();
            var normalized = Array.isArray(value) ? (value[0] || "") : (value || "");
            dotnetHelper.invokeMethodAsync("OnSurveyLocationSelect2Changed", normalized);
        });
    },

    setValue: function (selectId, value) {
        var $el = $("#" + selectId);
        if (!$el.length) {
            return;
        }

        $el.val(value || "").trigger("change.select2");
    },

    destroy: function (selectId) {
        var $el = $("#" + selectId);
        if (!$el.length) {
            return;
        }

        $el.off(".hcSurveyResultsLocation");
        if ($el.data("select2")) {
            $el.select2("destroy");
        }
    }
};
