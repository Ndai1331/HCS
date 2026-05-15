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





var VoloChatAvatarManager = {
	createCanvasForUser: function (canvas, username, name) {
	    if ((username == null || username == "") && (name == null || name == "")) {
            canvas.getContext("2d").clearRect(0, 0, canvas.width, canvas.height);
            return;
        }

        var colors = [
            {text: '#ffffff', background: '#3cb160'},
            {text: '#ffffff', background: '#c373cc'},
            {text: '#ffffff', background: '#2b78b3'},
            {text: '#ffffff', background: '#6ac79a'},
            {text: '#ffffff', background: '#aeb140'},
            {text: '#ffffff', background: '#b773c0'},
            {text: '#ffffff', background: '#e16d7a'},
            {text: '#ffffff', background: '#ffac2a'},
            {text: '#ffffff', background: '#21bbc7'},
            {text: '#ffffff', background: '#59ab95'}
        ];

        var generateFromString = function (str) {
            var hash = 0;
            for (var i = 0; i < str.length; i++) {
                hash = str.charCodeAt(i) + ((hash << 5) - hash);
            }
            return colors[Math.abs(hash % 10)];
        }

		var hashText;
		var text;

		if (name && name.length > 0) {
			hashText = name;

			var nameSplited = name.trim().split(" ");

			if (nameSplited.length >= 2) {
				var firstName = nameSplited[0];
				var lastName = nameSplited[nameSplited.length - 1];

				text = firstName.length >= 1 ? firstName.substring(0, 1) : firstName;
				text += lastName.length >= 1 ? lastName.substring(0, 1) : lastName;
			} else {
				text = name.length >= 2 ? name.substring(0, 2) : name;
			}
		} else {
			hashText = username;
			text = username && username.length >= 2 ? username.substring(0, 2) : username;
		}

		var colors = generateFromString(hashText);

		var ctx = canvas.getContext("2d");

		ctx.fillStyle = colors.background;
		ctx.fillRect(0, 0, canvas.width, canvas.height);

		ctx.font = "bold 15px Arial";
		ctx.fillStyle = colors.text;
		ctx.fillText(text.toUpperCase().substring(0, 2), canvas.width / 2 - 10, canvas.height / 2 + 5);
	},
    createCanvasForUserById: function (canvasId, username, name) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) {
            return;
        }
        this.createCanvasForUser(canvas, username, name);
    }
};
