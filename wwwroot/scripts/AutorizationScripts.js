document.addEventListener('DOMContentLoaded', function () {
    var elems = document.querySelectorAll('.collapsible');
    var instances = M.Collapsible.init(elems);
});

async function login() {
    const email = document.getElementById('username').value;
    const password = document.getElementById('password').value;

    if (email + "NotNSO" === password) { //При добавление БД обязательно убрать эту заглушку
        window.location.href = '/Home';
    }

    if (!email || !password) {
        alert('Пожалуйста, заполните все поля.');
        return;
    }

    const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ email: email, password: password })
    });

    if (response.ok) {
        window.location.href = '/Home';
    } else {
        alert('Неверное имя пользователя или пароль. ' + email + ":" + password);
    }
}

async function register() {
    const email = document.getElementById('register-email').value;
    const password = document.getElementById('register-password').value;
    const confirmPassword = document.getElementById('register-confirm-password').value;

    if (!email || !password || !confirmPassword) {
        alert('Пожалуйста, заполните все поля.');
        return;
    }

    if (password !== confirmPassword) {
        alert('Пароли не совпадают.');
        return;
    }

    const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ email: email, password: password })
    });

    if (response.ok) {
        window.location.href = '/Home';
    } else {
        const data = await response.json();
        const errorMessages = data.errors.map(error => error.description).join(', ');
        alert('Регистрация не удалась: ' + errorMessages);
    }
}