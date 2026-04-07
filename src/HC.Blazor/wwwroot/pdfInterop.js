window.pdfPick = (function () {
    const PATHS = {
        pdf: [
            '/lib/pdfjs/pdf.min.js',
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js'
        ],
        worker: [
            '/lib/pdfjs/pdf.worker.min.js',
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js'
        ]
    };

    function loadScriptOnce(src) {
        return new Promise((resolve, reject) => {
            const normalizedSrc = new URL(src, window.location.origin).href;
            // Already loaded?
            if ([...document.scripts].some(s => s.src === normalizedSrc || s.src === src)) return resolve();
            const s = document.createElement('script');
            s.src = src;
            s.async = true;
            s.onload = () => resolve();
            s.onerror = () => reject(new Error('Failed to load script ' + src));
            document.head.appendChild(s);
        });
    }

    async function loadFirstAvailableScript(candidates) {
        let lastError = null;

        for (const candidate of candidates) {
            try {
                await loadScriptOnce(candidate);
                return candidate;
            } catch (err) {
                lastError = err;
                console.warn('[pdfPick] Unable to load script:', candidate, err);
            }
        }

        throw lastError ?? new Error('No PDF.js script candidates available.');
    }

    async function ensurePdfJsLoaded() {
        if (window.pdfjsLib && window.pdfjsLib.GlobalWorkerOptions) {
            return;
        }

        const loadedPdfScript = await loadFirstAvailableScript(PATHS.pdf);
        // pdfjsLib globally available now
        if (!window.pdfjsLib) throw new Error('pdfjsLib not found after loading.');

        const loadedFromCdn = loadedPdfScript.startsWith('http');
        window.pdfjsLib.GlobalWorkerOptions.workerSrc = loadedFromCdn
            ? PATHS.worker[1]
            : PATHS.worker[0];
    }

    async function init(dotnetRef, url, containerId) {
        await ensurePdfJsLoaded();

        const container = document.getElementById(containerId);
        if (!container) throw new Error("pdf container not found: " + containerId);
        container.innerHTML = "";

        const loadingTask = pdfjsLib.getDocument(url);
        const pdf = await loadingTask.promise;
        const scale = 1;

        for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
            const page = await pdf.getPage(pageNum);
            const viewport = page.getViewport({ scale });

            // Wrapper that preserves size & margin between pages
            const pageWrap = document.createElement("div");
            pageWrap.style.position = "relative";
            pageWrap.style.margin = "12px auto";
            pageWrap.style.width = viewport.width + "px";

            // Canvas layer
            const canvas = document.createElement("canvas");
            const ctx = canvas.getContext("2d", { willReadFrequently: false });
            canvas.width = viewport.width;
            canvas.height = viewport.height;
            canvas.style.display = "block";
            canvas.style.maxWidth = "100%";
            canvas.style.height = "auto";

            pageWrap.appendChild(canvas);

            // Create overlay layer for markers
            const overlay = document.createElement("div");
            overlay.className = "marker-layer";
            overlay.style.position = "absolute";
            overlay.style.inset = "0";
            overlay.style.pointerEvents = "none";
            overlay.style.zIndex = "10";
            pageWrap.appendChild(overlay);

            container.appendChild(pageWrap);

            // Render PDF page into the canvas
            await page.render({ canvasContext: ctx, viewport }).promise;


            // Handle click => return both CSS coordinates & PDF points
            canvas.addEventListener("click", (ev) => {
                const rect = canvas.getBoundingClientRect();

                // Click position in CSS pixels on the displayed canvas
                const cssX = ev.clientX - rect.left;
                const cssY = ev.clientY - rect.top;

                // Remove all existing markers
                container.querySelectorAll(".marker").forEach(el => el.remove());

                // Create new marker
                const marker = document.createElement("div");
                marker.className = "marker";
                marker.style.position = "absolute";
                marker.style.left = cssX + "px";
                marker.style.top = cssY + "px";
                marker.style.width = "8px";
                marker.style.height = "8px";
                marker.style.background = "#e53935";
                marker.style.borderRadius = "50%";
                marker.style.boxShadow = "0 0 0 2px rgba(255,255,255,.95)";
                marker.style.transform = "translate(-50%, -50%)";
                marker.style.pointerEvents = "none";
                marker.style.zIndex = "11";

                overlay.appendChild(marker);


                // Compute according to display scaling (CSS may stretch the canvas)
                const scaleX = canvas.width / rect.width;   // convert CSS px -> internal canvas pixels
                const scaleY = canvas.height / rect.height;

                const internalX = cssX * scaleX;
                const internalY = cssY * scaleY;

                // Convert to PDF points (72 dpi) using viewport
                // Note: viewport.convertToPdfPoint expects coordinates in canvas drawing pixels
                // Here internalX/internalY already correspond to drawing pixels.
                const [pdfX, pdfY] = viewport.convertToPdfPoint(internalX, internalY);

                // Invoke back to .NET
                dotnetRef.invokeMethodAsync("OnPdfClick", pageNum, pdfX, pdfY, cssX, cssY)
                    .catch(err => console.error(err));
            }, { passive: true });
        }
    }

    return { init };
})();
