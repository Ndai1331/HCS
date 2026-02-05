/* Your Global Scripts */

//append style to body
const style = document.createElement('style');
style.textContent = `
    .lpx-login-bg{
        background-image: url('https://dev.benhvien199.vn/images/login-pages/login-bg-img-light.svg') !important;
    }
`;
document.body.appendChild(style);