// Currency Formatting Functions
window.currencyFormatter = {
    // Format number with thousand separators (Vietnamese format)
    formatNumber: function(num) {
        if (!num && num !== 0) return '';
        
        // Convert to number if string
        const number = typeof num === 'string' ? parseFloat(num.replace(/[^\d.-]/g, '')) : num;
        
        // Check if valid number
        if (isNaN(number)) return '';
        
        // Format with Vietnamese locale (dot as thousand separator)
        return number.toLocaleString('vi-VN');
    },

    // Parse formatted currency string back to number
    parseCurrency: function(str) {
        if (!str) return null;
        
        // Remove thousand separators and convert comma to dot
        const cleaned = str.toString().replace(/\./g, '').replace(',', '.');
        const number = parseFloat(cleaned);
        
        return isNaN(number) ? null : number;
    },

    // Format currency input field in real-time
    formatCurrencyInput: function(input) {
        if (!input) return;
        
        const element = typeof input === 'string' ? document.querySelector(input) : input;
        if (!element) return;
        
        const cursorPosition = element.selectionStart;
        const value = element.value;
        
        // Remove all non-digit characters except decimal point
        const cleanValue = value.replace(/[^\d]/g, '');
        
        if (cleanValue) {
            const number = parseInt(cleanValue);
            const formatted = this.formatNumber(number);
            
            // Update value without triggering events
            element.value = formatted;
            
            // Restore cursor position (approximate)
            const newPosition = Math.min(cursorPosition, formatted.length);
            element.setSelectionRange(newPosition, newPosition);
        }
    },

    // Real-time formatting for immediate response
    formatCurrencyInputRealTime: function(element) {
        const input = typeof element === 'string' ? 
            document.querySelector(`input[value="${element}"]`) || 
            document.activeElement : element;
            
        if (!input || !input.classList.contains('currency-input')) return;
        
        const cursorPosition = input.selectionStart;
        const value = input.value;
        
        // Remove all non-digit characters
        const cleanValue = value.replace(/[^\d]/g, '');
        
        if (cleanValue) {
            const number = parseInt(cleanValue);
            const formatted = this.formatNumber(number);
            
            // Only update if different to avoid infinite loops
            if (input.value !== formatted) {
                input.value = formatted;
                
                // Calculate new cursor position
                const originalLength = value.length;
                const newLength = formatted.length;
                const diff = newLength - originalLength;
                const newPosition = Math.min(cursorPosition + diff, formatted.length);
                
                // Restore cursor position
                setTimeout(() => {
                    input.setSelectionRange(newPosition, newPosition);
                }, 0);
            }
        } else if (value === '') {
            input.value = '';
        }
    },

    // Initialize currency inputs on page load
    initializeCurrencyInputs: function() {
        const currencyInputs = document.querySelectorAll('.currency-input');
        
        currencyInputs.forEach(input => {
            // Real-time formatting on every keystroke
            input.addEventListener('input', (e) => {
                this.formatCurrencyInputRealTime(e.target);
            });
            
            // Format on focus out
            input.addEventListener('blur', (e) => {
                this.formatCurrencyInput(e.target);
            });
            
            // Format on paste
            input.addEventListener('paste', (e) => {
                setTimeout(() => {
                    this.formatCurrencyInputRealTime(e.target);
                }, 0);
            });
            
            // Allow only numbers and basic navigation keys
            input.addEventListener('keydown', (e) => {
                // Allow: backspace, delete, tab, escape, enter
                if ([46, 8, 9, 27, 13].indexOf(e.keyCode) !== -1 ||
                    // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X, Ctrl+Z
                    (e.keyCode === 65 && e.ctrlKey === true) ||
                    (e.keyCode === 67 && e.ctrlKey === true) ||
                    (e.keyCode === 86 && e.ctrlKey === true) ||
                    (e.keyCode === 88 && e.ctrlKey === true) ||
                    (e.keyCode === 90 && e.ctrlKey === true) ||
                    // Allow: home, end, left, right, down, up
                    (e.keyCode >= 35 && e.keyCode <= 40)) {
                    return;
                }
                
                // Ensure that it is a number and stop the keypress
                if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && (e.keyCode < 96 || e.keyCode > 105)) {
                    e.preventDefault();
                }
            });
        });
    }
};

// Global function for Blazor to call
window.formatCurrencyInput = function(value) {
    // This function can be called from Blazor C#
    console.log('Formatting currency input:', value);
};

// Global function for real-time formatting from Blazor
window.formatCurrencyInputRealTime = function(value) {
    const activeElement = document.activeElement;
    if (activeElement && activeElement.classList.contains('currency-input')) {
        window.currencyFormatter.formatCurrencyInputRealTime(activeElement);
    }
};

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.currencyFormatter.initializeCurrencyInputs();
});

// Re-initialize when content changes (for dynamic content)
window.reinitializeCurrencyInputs = function() {
    window.currencyFormatter.initializeCurrencyInputs();
};