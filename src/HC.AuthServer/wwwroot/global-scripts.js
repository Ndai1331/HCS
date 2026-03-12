/* Your Global Scripts */

//append style to body
const style = document.createElement('style');
style.textContent = `
    .lpx-login-bg{
        background-image: url('/images/login-pages/login-bg-img-light.svg') !important;
    }
        
    .lpx-login-area  .card-body {
        box-shadow: var(--bs-box-shadow) !important;
    }
`;
document.body.appendChild(style);