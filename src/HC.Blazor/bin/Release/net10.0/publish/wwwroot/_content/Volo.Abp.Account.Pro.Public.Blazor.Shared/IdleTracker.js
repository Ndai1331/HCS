var abp = abp || {};
(function () {
	abp.idleTracker = abp.idleTracker || {};

	abp.idleTracker.initialize = function(options){
		
		abp.dotnetInvoker = options.dotNetObjectRef;

		window.addEventListener('storage', function (e) {
			if(e.key === "abp-idle-settings-identifier"){
				abp.dotnetInvoker.invokeMethodAsync(options.onAccountIdleSettingsChangedCallbackMethod);
			}
		}, false);
	}
	
	abp.idleTracker.create = function (options) {
		options = options || {};
		var idleTracker = {
			timeout: options.timeout || 60 * 10000,
			onIdleCallbackMethod: options.onIdleCallbackMethod,
			onStorageCallbackMethod: options.onStorageCallbackMethod,
			callback: function (state) {
				if(abp.dotnetInvoker && this.onIdleCallbackMethod){
					abp.dotnetInvoker.invokeMethodAsync(idleTracker.onIdleCallbackMethod, state);
				}
			},
			storageCallback: function (state) {
				if(abp.dotnetInvoker && this.onStorageCallbackMethod){
					abp.dotnetInvoker.invokeMethodAsync(idleTracker.onStorageCallbackMethod, {
						Idle: state.idle
					});
				}
			},
			events: options.events || [
				'change',
				'keydown',
				'mousedown',
				'mousemove',
				'mouseup',
				'orientationchange',
				'resize',
				'scroll',
				'touchend',
				'touchmove',
				'touchstart',
				'visibilitychange'
			],
			throttleTime: options.throttle || 500,
			listeners: [],
			timer: null,
			state: {
				idle: false,
				lastActive: 0,
				paused: false
			},
			storageKey: options.storageKey || 'abp-idle-tracker',
			handleEvent: null
		};

		var handleStorageEvent = function (e) {
			if (e.key === idleTracker.storageKey && e.newValue) {
				var data = JSON.parse(e.newValue);
				if (!data.idle) {
					idleTracker.resetTimer();
				}

				idleTracker.storageCallback(data);
			}
		};
		
		var handleEvent = function (e) {
			if (idleTracker.state.paused) {
				return;
			}

			if (Date.now() - idleTracker.state.lastActive < idleTracker.throttleTime) {
				return;
			}

			if (e.type === 'mousemove' || e.type === 'touchmove') {
				idleTracker.resetTimer(e);
			}

			if (idleTracker.state.idle) {
				idleTracker.callback({ event: e, idle: false });
			}

			idleTracker.state.idle = false;
			idleTracker.resetTimer();

			idleTracker.syncState();
		};

		idleTracker.syncState = function () {
			idleTracker.state.lastActive = Date.now();
			localStorage.setItem(idleTracker.storageKey, JSON.stringify({
				idle: idleTracker.state.idle,
				lastActive: idleTracker.state.lastActive
			}));
		};

		idleTracker.start = function () {
			idleTracker.listeners = [];
			for (var i = 0; i < idleTracker.events.length; i++) {
				var eventName = idleTracker.events[i];
				document.addEventListener(eventName, handleEvent, false);
				idleTracker.listeners.push(eventName);
			}

			window.addEventListener('storage', handleStorageEvent, false);
			idleTracker.syncState();

			idleTracker.state.lastActive = Date.now();
			idleTracker.resetTimer();
		};

		idleTracker.resetTimer = function (e) {
			idleTracker.clearTimer();
			idleTracker.state.lastActive = Date.now();

			idleTracker.timer = setTimeout(function () {
				if (!idleTracker.state.idle) {
					idleTracker.callback({ event: e, idle: true});
				}
				idleTracker.state.idle = true;
				idleTracker.resetTimer(e);
			}, idleTracker.timeout);
		};

		idleTracker.clearTimer = function () {
			if (idleTracker.timer) {
				clearTimeout(idleTracker.timer);
				idleTracker.timer = null;
			}
		};

		idleTracker.pause = function () {
			idleTracker.state.paused = true;
		};

		idleTracker.resume = function () {
			idleTracker.state.paused = false;
			idleTracker.state.idle = false;
			idleTracker.syncState();
		};

		idleTracker.end = function () {
			for (var i = 0; i < idleTracker.listeners.length; i++) {
				var eventName = idleTracker.listeners[i];
				document.removeEventListener(eventName, handleEvent, false);
			}
			idleTracker.listeners = [];
			idleTracker.clearTimer();
			localStorage.setItem(idleTracker.storageKey, JSON.stringify(idleTracker.state));
			window.removeEventListener('storage', handleStorageEvent, false);
			localStorage.removeItem(idleTracker.storageKey);
		};
		
		return idleTracker;
	};
})();
